using UnityEngine;
using System.Text;
using System.IO;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SdfCsgTreeBaker : MonoBehaviour
{
    public string characterFunctionName = "GetCustomCharacterDist";

    [ContextMenu("Bake Tree to HLSL File")]
    public void BakeToShaderFile()
    {
        SdfCsgNode rootNode = GetComponent<SdfCsgNode>();
        if (rootNode == null)
        {
            Debug.LogError("No SdfCsgNode found on this root object!");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// --- AUTOMATICALLY GENERATED CSG TREE CODE - DO NOT EDIT MANUALLY ---");
        sb.AppendLine($"float {characterFunctionName}(float3 p, int baseIdx)");
        sb.AppendLine("{");

        int primitiveCounter = 0;
        int stepCounter = 0;
        
        string finalResultVariable = ProcessNodeString(rootNode, sb, ref primitiveCounter, ref stepCounter);

        sb.AppendLine();
        sb.AppendLine($"    return {finalResultVariable};");
        sb.AppendLine("}");

        // Save directly to your Assets folder so the shader can see it immediately
        string filePath = Path.Combine(Application.dataPath, "Shaders/CustomCharacterSdf.hlsl");
        
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

            // Pull the clean, matching matrices and data layouts from the primitive tracking structure
            sb.AppendLine($"    float3 localP_{primCount} = mul(_CharMatrices[baseIdx + {primCount}], float4(p, 1.0)).xyz;");

            if (prim.IsCube())
            {
                sb.AppendLine($"    float prim_{primCount} = DistanceToBoxLocal(localP_{primCount}, _CharData[baseIdx + {primCount}].xyz);");
            }
            else if (prim.IsSphere())
            {
                // Spheres pull their calculated radius value from vector configuration data
                sb.AppendLine($"    float {primVar} = length(localP_{primCount}) - _CharData[baseIdx + {primCount}].x;");
            }
            else if (prim.IsCapsule())
            {
                // Capsules get their unwarped local spaces, world radius, world height, and axis direction index natively!
                sb.AppendLine($"    float {primVar} = DistanceToCapsuleLocal(localP_{primCount}, _CharData[baseIdx + {primCount}].x, _CharData[baseIdx + {primCount}].y, (int)_CharData[baseIdx + {primCount}].z);");
            }

            primCount++;
            return primVar;
        }

        // (Rest of the cascading group operator evaluation logic remains identical...)
        List<string> childVariables = new List<string>();
        for (int i = 0; i < node.transform.childCount; i++)
        {
            var childNode = node.transform.GetChild(i).GetComponent<SdfCsgNode>();
            if (childNode != null && childNode.gameObject.activeInHierarchy)
            {
                childVariables.Add(ProcessNodeString(childNode, sb, ref primCount, ref stepCount));
            }
        }

        if (childVariables.Count == 0) return "10000.0";
        if (childVariables.Count == 1) return childVariables[0];

        string currentAccumulator = childVariables[0];
        for (int j = 1; j < childVariables.Count; j++)
        {
            string nextStepVar = $"step_{stepCount++}";
            if (node.groupOperation == CSGGroupOp.Subtraction)
                sb.AppendLine($"    float {nextStepVar} = max({currentAccumulator}, -({childVariables[j]}));");
            else if (node.groupOperation == CSGGroupOp.Intersection)
                sb.AppendLine($"    float {nextStepVar} = max({currentAccumulator}, {childVariables[j]});");
            else
                sb.AppendLine($"    float {nextStepVar} = min({currentAccumulator}, {childVariables[j]});");
            
            currentAccumulator = nextStepVar;
        }

        return currentAccumulator;
    }
}