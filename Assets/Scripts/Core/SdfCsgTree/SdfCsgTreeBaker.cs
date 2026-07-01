using UnityEngine;
using System.Text;
using System.IO;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SdfCsgTreeBaker : MonoBehaviour
{
    public string assetName = "CustomCharacter";

    public void BakeToShaderFile()
    {
        SdfCsgNode rootNode = GetComponent<SdfCsgNode>();
        if (rootNode == null)
        {
            Debug.LogError("No SdfCsgNode found on this root object!");
            return;
        }

        string functionName = $"Get{assetName}Dist";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// --- AUTOMATICALLY GENERATED CSG TREE CODE - DO NOT EDIT MANUALLY ---");
        sb.AppendLine($"float {functionName}(float3 p, int baseIdx, float smoothness)");
        sb.AppendLine("{");

        int primitiveCounter = 0;
        int stepCounter = 0;
        
        string finalResultVariable = ProcessNodeString(rootNode, sb, ref primitiveCounter, ref stepCounter);

        sb.AppendLine();
        sb.AppendLine($"    return {finalResultVariable};");
        sb.AppendLine("}");

        // Save directly to your Assets folder so the shader can see it immediately
        string filePath = Path.Combine(Application.dataPath, $"Shaders/Generated/{assetName}.hlsl");
        
        // Ensure directory exists
        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, sb.ToString());
        
#if UNITY_EDITOR
        AssetDatabase.Refresh(); // Tells Unity a shader component file changed
#endif
        Debug.Log($"Successfully baked character tree to: {filePath}");
    }

    private string ProcessNodeString(SdfCsgNode node, StringBuilder sb, ref int primCount, ref int stepCount)
    {
        if (!node.isGroupNode)
        {
            var prim = node.primitiveSubscriber;
            if (prim == null)
            {
                Debug.LogError($"[Baker Error] Leaf node '{node.gameObject.name}' is missing its Primitive Subscriber reference!", node);
                return "10000.0";
            }

            string primVar = $"prim_{primCount}";
            sb.AppendLine($"    float3 localP_{primCount} = mul(_CharMatrices[baseIdx + {primCount}], float4(p, 1.0)).xyz;");

            if (prim.IsCube())
                sb.AppendLine($"    float prim_{primCount} = DistanceToBoxLocal(localP_{primCount}, _CharData[baseIdx + {primCount}].xyz);");
            else if (prim.IsSphere())
                sb.AppendLine($"    float {primVar} = length(localP_{primCount}) - _CharData[baseIdx + {primCount}].x;");
            else if (prim.IsCapsule())
                sb.AppendLine($"    float {primVar} = DistanceToCapsuleLocal(localP_{primCount}, _CharData[baseIdx + {primCount}].x, _CharData[baseIdx + {primCount}].y, (int)_CharData[baseIdx + {primCount}].z);");

            primCount++;
            return primVar;
        }

        // Gather all operational CSG children, jumping through raw bones seamlessly
        List<SdfCsgNode> validCsgChildren = new List<SdfCsgNode>();
        GatherCsgChildren(node.transform, validCsgChildren);

        List<string> childVariables = new List<string>();
        foreach (var childNode in validCsgChildren)
        {
            childVariables.Add(ProcessNodeString(childNode, sb, ref primCount, ref stepCount));
        }

        if (childVariables.Count == 0) return "10000.0";
        if (childVariables.Count == 1) return childVariables[0];

        string currentAccumulator = childVariables[0];
        for (int j = 1; j < childVariables.Count; j++)
        {
            string nextStepVar = $"step_{stepCount++}";
            if (node.groupOperation == CSGGroupOp.Subtraction)
                sb.AppendLine($"    float {nextStepVar} = smax({currentAccumulator}, -({childVariables[j]}), smoothness);");
            else if (node.groupOperation == CSGGroupOp.Intersection)
                sb.AppendLine($"    float {nextStepVar} = smax({currentAccumulator}, {childVariables[j]}, smoothness);");
            else
                sb.AppendLine($"    float {nextStepVar} = smin({currentAccumulator}, {childVariables[j]}, smoothness);");
            
            currentAccumulator = nextStepVar;
        }

        return currentAccumulator;
    }

    // 🛠️ Helper to dive deep past bones to collect the immediate operational nodes below this group
    private void GatherCsgChildren(Transform parentTransform, List<SdfCsgNode> results)
    {
        for (int i = 0; i < parentTransform.childCount; i++)
        {
            Transform child = parentTransform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            var csgNode = child.GetComponent<SdfCsgNode>();
            if (csgNode != null)
            {
                // Found a valid operational node boundary! Add it and stop digging down this specific branch
                results.Add(csgNode);
            }
            else
            {
                // It's a raw bone or model wrapper container—keep looking deeper down its hierarchy
                GatherCsgChildren(child, results);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SdfCsgTreeBaker))]
public class SdfCsgTreeBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector variables (like assetName string)
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        
        // Change button color to make it visually distinct
        GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f); 

        // 🚀 Render the explicit button layout
        if (GUILayout.Button("Bake Tree to HLSL File", GUILayout.Height(35)))
        {
            // Cast the target source object safely
            SdfCsgTreeBaker baker = (SdfCsgTreeBaker)target;
            
            // Execute the processing engine immediately on click
            baker.BakeToShaderFile();
        }
        
        // Reset color space configuration back to default state
        GUI.backgroundColor = Color.white;
    }
}
#endif