using UnityEngine;
using System.Collections.Generic;

public enum CSGAssetType { Sword = 0, Character = 1, SwordCharacter = 2 }

[ExecuteAlways]
public class SdfCsgTreeRootInstance : MonoBehaviour
{
    public CSGAssetType assetType = CSGAssetType.Character;
    [HideInInspector] public int globalBufferStartIndex = -1;

    public List<SdfCsgNode> flattenedPrimitives = new List<SdfCsgNode>();
    private readonly List<SdfOctreeNode> occupiedOctreeNodes = new List<SdfOctreeNode>();

    private Vector3 lastPosition;
    private Quaternion lastRotation;

    void Start()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        
        InitializeHierarchy();
        RecalculateOctreePresence();
    }

    void OnEnable()
    {
        SdfOctreeManager.RegisterCsgInstance(this);
    }

    void OnDisable()
    {
        SdfOctreeManager.UnregisterCsgInstance(this);
        CleanUp();
    }

    void LateUpdate()
    {
        if (transform.position != lastPosition || transform.rotation != lastRotation)
        {
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            
            RecalculateOctreePresence();
        }
    }

    public void InitializeHierarchy()
    {
        flattenedPrimitives.Clear();
        SdfCsgNode rootNode = GetComponent<SdfCsgNode>();
        if (rootNode != null)
        {
            rootNode.GetFlattenedSubtree(flattenedPrimitives);
        }

        foreach (var node in flattenedPrimitives)
        {
            if (!node.isGroupNode && node.primitiveSubscriber != null)
            {
                node.primitiveSubscriber.isPartOfCsgTree = true;
            }
        }
    }

    public int PackDataIntoBuffers(Matrix4x4[] globalMatrices, Vector4[] globalData, int startBufferPtr, int maxBufferLimit)
    {
        if (flattenedPrimitives.Count == 0)
        {
            InitializeHierarchy();
        }

        int idx = startBufferPtr;

        foreach (var node in flattenedPrimitives)
        {
            if (node.isGroupNode || node.primitiveSubscriber == null) continue;
            if (idx >= maxBufferLimit) break;

            var prim = node.primitiveSubscriber;
            Transform t = prim.transform;

            if (prim.IsCube())
            {
                globalMatrices[idx] = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;
                Vector3 halfExtents = t.lossyScale * 0.5f;
                globalData[idx] = new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0f);
            }
            else if (prim.IsSphere())
            {
                globalMatrices[idx] = Matrix4x4.TRS(t.position, Quaternion.identity, Vector3.one).inverse;
                float radius = Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z)) * 0.5f;
                globalData[idx] = new Vector4(radius, 0f, 0f, 1.0f);
            }
            else if (prim.IsCapsule())
            {
                prim.GetCapsuleLocalDimensions(out float r, out float h, out int dir);
                Vector3 ls = t.lossyScale;
                globalMatrices[idx] = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;

                float worldRadius = r * (dir == 1 ? Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)) : (dir == 0 ? Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)) : Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y))));
                float worldHeight = h * (dir == 0 ? Mathf.Abs(ls.x) : (dir == 1 ? Mathf.Abs(ls.y) : Mathf.Abs(ls.z)));
                globalData[idx] = new Vector4(worldRadius, worldHeight, (float)dir, 1.0f);
            }
            idx++;
        }

        return idx; 
    }

    [ContextMenu("🔍 Debug Print Flat Hierarchy")]
    public void DebugPrintHierarchy()
    {
        var testList = new List<SdfCsgNode>();
        
        SdfCsgNode rootNode = GetComponent<SdfCsgNode>();
        if (rootNode == null)
        {
            Debug.LogError("Can't print: No SdfCsgNode found on this root GameObject!");
            return;
        }

        rootNode.GetFlattenedSubtree(testList);

        Debug.Log($"====== CSG TREE FLAT HIERARCHY ({testList.Count} elements) ======");
        
        for (int i = 0; i < testList.Count; i++)
        {
            var node = testList[i];
            if (node.isGroupNode)
            {
                Debug.Log($"[{i}] GROUP OPERATOR: {node.groupOperation} | GameObject: {node.name}");
            }
            else
            {
                string primType = node.primitiveSubscriber != null 
                    ? node.primitiveSubscriber.shapeType.ToString() 
                    : "NULL (MISSING REFERENCE!)";
                    
                Debug.Log($"[{i}] PRIMITIVE LEAF: {primType} | GameObject: {node.name}");
            }
        }
        Debug.Log("==================================================");
    }

    public void RecalculateOctreePresence()
    {
        foreach (var leaf in occupiedOctreeNodes)
        {
            leaf.RemovePrimitiveDirectly(transform);
        }
        occupiedOctreeNodes.Clear();

        if (SdfOctreeManager.Instance == null) return;

        Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);
        int validBoundsCount = 0;

        foreach (var node in flattenedPrimitives)
        {
            if (!node.isGroupNode && node.primitiveSubscriber != null)
            {
                if (validBoundsCount == 0)
                    combinedBounds = node.primitiveSubscriber.GetWorldBounds();
                else
                    combinedBounds.Encapsulate(node.primitiveSubscriber.GetWorldBounds());
                
                validBoundsCount++;
            }
        }

        if (validBoundsCount == 0) return;

        List<SdfOctreeNode> overlappingLeaves = new List<SdfOctreeNode>();
        SdfOctreeManager.Instance.FindAllLeafNodesOverlapping(combinedBounds, overlappingLeaves);

        foreach (var leaf in overlappingLeaves)
        {
            occupiedOctreeNodes.Add(leaf);
            leaf.AddPrimitiveDirectly(transform); 
        }

        SdfOctreeManager.Instance.TriggerCollapseCheck();
    }

    void OnDestroy() => CleanUp();

    private void CleanUp()
    {
        foreach (var node in occupiedOctreeNodes)
        {
            node.RemovePrimitiveDirectly(transform);
        }
        occupiedOctreeNodes.Clear();
        if (SdfOctreeManager.Instance != null) SdfOctreeManager.Instance.TriggerCollapseCheck();
    }
}