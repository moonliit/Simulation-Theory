using UnityEngine;
using UnityEngine.InputSystem;
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
        // Register this entire object structure to the global manager
        SdfOctreeManager.RegisterCsgInstance(this);
    }

    void OnDisable()
    {
        SdfOctreeManager.UnregisterCsgInstance(this);
        CleanUp();
    }

    void LateUpdate()
    {
        // If the character moves or rotates, we must update both our internal shape matrices 
        // and its registration inside the world octree partitions
        //if (transform.position != lastPosition || transform.rotation != lastRotation)
        //{
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            
            RecalculateOctreePresence();
        //}
    }

    public void InitializeHierarchy()
    {
        flattenedPrimitives.Clear();
        SdfCsgNode rootNode = GetComponent<SdfCsgNode>();
        if (rootNode != null)
        {
            rootNode.GetFlattenedSubtree(flattenedPrimitives);
        }

        // Force-flag all underlying sub-primitives to passive state so they don't leak into the global scope
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
            if (idx >= maxBufferLimit) break; // Use the manager's maximum constant limit guard

            var prim = node.primitiveSubscriber;
            Transform t = prim.transform;

            // Populate the MANAGER'S flat global array directly
            if (prim.IsCube())
            {
                globalMatrices[idx] = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;
                Vector3 halfExtents = t.lossyScale * 0.5f;
                globalData[idx] = new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0f); // Type = 0
            }
            else if (prim.IsSphere())
            {
                globalMatrices[idx] = Matrix4x4.TRS(t.position, Quaternion.identity, Vector3.one).inverse;
                float radius = Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z)) * 0.5f;
                globalData[idx] = new Vector4(radius, 0f, 0f, 1.0f); // Type = 1
            }
            else if (prim.IsCapsule())
            {
                prim.GetCapsuleLocalDimensions(out float r, out float h, out int dir);
                Vector3 ls = t.lossyScale;
                globalMatrices[idx] = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;

                float worldRadius = r * (dir == 1 ? Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)) : (dir == 0 ? Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)) : Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y))));
                float worldHeight = h * (dir == 0 ? Mathf.Abs(ls.x) : (dir == 1 ? Mathf.Abs(ls.y) : Mathf.Abs(ls.z)));
                globalData[idx] = new Vector4(worldRadius, worldHeight, (float)dir, 1.0f); // Type = 2
            }
            idx++;
        }

        // Return the new pointer position so the manager knows how many spots we consumed!
        return idx; 
    }

    [ContextMenu("🔍 Debug Print Flat Hierarchy")]
    public void DebugPrintHierarchy()
    {
        var testList = new List<SdfCsgNode>();
        
        SdfCsgNode rootNode = GetComponent<SdfCsgNode>();
        if (rootNode == null)
        {
            Debug.LogError("❌ Can't print: No SdfCsgNode found on this root GameObject!");
            return;
        }

        // Run the actual flattening logic used by your baker and manager
        rootNode.GetFlattenedSubtree(testList);

        Debug.Log($"====== 🌳 CSG TREE FLAT HIERARCHY ({testList.Count} elements) ======");
        
        for (int i = 0; i < testList.Count; i++)
        {
            var node = testList[i];
            if (node.isGroupNode)
            {
                Debug.Log($"[{i}] 📂 GROUP OPERATOR: {node.groupOperation} | GameObject: {node.name}");
            }
            else
            {
                string primType = node.primitiveSubscriber != null 
                    ? node.primitiveSubscriber.shapeType.ToString() 
                    : "NULL (MISSING REFERENCE!)";
                    
                Debug.Log($"[{i}] ┖ 📦 PRIMITIVE LEAF: {primType} | GameObject: {node.name}");
            }
        }
        Debug.Log("==================================================");
    }

    private void BulkMarkForRebake()
    {
        if (SDFCacheManager.Instance == null) return;

        foreach (var node in flattenedPrimitives)
        {
            // obtain AABB, and for each brick it spans, mark brick for rebake
            if (!node.isGroupNode && node.primitiveSubscriber != null)
            {
                Bounds worldBounds = node.primitiveSubscriber.GetWorldBounds();
                SDFCacheManager.Instance.MarkRegionForRebake(worldBounds);
            }
        }
    }

    public void RecalculateOctreePresence()
    {
        // 1. Clear out old sector allocations
        foreach (var leaf in occupiedOctreeNodes)
        {
            leaf.RemovePrimitiveDirectly(transform); // Registers the entire root transform instead of individual pieces
        }
        occupiedOctreeNodes.Clear();

        if (SdfOctreeManager.Instance == null) return;

        // 2. Encapsulate ALL child nodes to compute a single compound bounding box for the whole guy
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

        // Tell the cache manager to update this volume space
        BulkMarkForRebake();

        // 3. Query our octree manager to discover which sectors intersect our compound character volume
        List<SdfOctreeNode> overlappingLeaves = new List<SdfOctreeNode>();
        SdfOctreeManager.Instance.FindAllLeafNodesOverlapping(combinedBounds, overlappingLeaves);

        // 4. Bind our master character root directly to those specific sub-regions
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