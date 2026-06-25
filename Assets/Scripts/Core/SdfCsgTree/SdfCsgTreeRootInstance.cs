using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class SdfCsgTreeRootInstance : MonoBehaviour
{
    public int primitiveCount = 4;
    [HideInInspector] public int globalBufferStartIndex = -1;

    public List<SdfCsgNode> flattenedPrimitives = new List<SdfCsgNode>();
    private readonly List<SdfOctreeNode> occupiedOctreeNodes = new List<SdfOctreeNode>();

    // Dedicated runtime parameter blocks sent over to our specific custom character shader instance
    private readonly Matrix4x4[] charMatrices = new Matrix4x4[16];
    private readonly Vector4[] charData = new Vector4[16];

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
    }

    void LateUpdate()
    {
        // If the character moves or rotates, we must update both our internal shape matrices 
        // and its registration inside the world octree partitions
        if (transform.position != lastPosition || transform.rotation != lastRotation)
        {
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            
            //UpdateInternalArrays();
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

        // Force-flag all underlying sub-primitives to passive state so they don't leak into the global scope
        foreach (var node in flattenedPrimitives)
        {
            if (!node.isGroupNode && node.primitiveSubscriber != null)
            {
                node.primitiveSubscriber.isPartOfCsgTree = true;
            }
        }

        UpdateInternalArrays();
    }

    [ContextMenu("fuck shaders")]
    private void UpdateInternalArrays()
    {
        // 1. (Keep your matrix and vector data packaging logic exactly the same...)
        int idx = 0;
        for(int i = 0; i < 16; i++)
        {
            charMatrices[i] = Matrix4x4.identity;
            charData[i] = new Vector4(0f, 0f, 0f, -1f); 
        }

        if (flattenedPrimitives.Count == 0)
        {
            InitializeHierarchy();
        }

        foreach (var node in flattenedPrimitives)
        {

            if (node.isGroupNode || node.primitiveSubscriber == null) continue;
            if (idx >= 16) break;

            Debug.Log($"{idx}-th primitive: {node.name}");

            var prim = node.primitiveSubscriber;
            Transform t = prim.transform;

            if (prim.IsCube())
            {
                charMatrices[idx] = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;
                Vector3 halfExtents = t.lossyScale * 0.5f;
                charData[idx] = new Vector4(halfExtents.x, halfExtents.y, halfExtents.z, 0f);
            }
            else if (prim.IsSphere())
            {
                charMatrices[idx] = Matrix4x4.TRS(t.position, Quaternion.identity, Vector3.one).inverse;
                float radius = Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z)) * 0.5f;
                charData[idx] = new Vector4(radius, 0f, 0f, 1.0f);
            }
            else if (prim.IsCapsule())
            {
                prim.GetCapsuleLocalDimensions(out float r, out float h, out int dir);
                Vector3 ls = t.lossyScale;
                charMatrices[idx] = Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;

                float worldRadius = r * (dir == 1 ? Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)) : (dir == 0 ? Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)) : Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y))));
                float worldHeight = h * (dir == 0 ? Mathf.Abs(ls.x) : (dir == 1 ? Mathf.Abs(ls.y) : Mathf.Abs(ls.z)));
                charData[idx] = new Vector4(worldRadius, worldHeight, (float)dir, 1.0f);
            }
            idx++;
        }

        // 1. Resolve our manager instance reference safely whether playing or editing
        var manager = SdfOctreeManager.Instance;
        
        #if UNITY_EDITOR
        if (manager == null)
        {
            // Fallback for editor mode when Awake() hasn't run yet
            manager = FindFirstObjectByType<SdfOctreeManager>();
        }
        #endif

        if (manager == null)
        {
            Debug.LogWarning("⚠️ SdfOctreeManager could not be found anywhere in the active scene! Floated data directly to global registers.");
            return;
        }

        Debug.Log("printing debug info");
        Debug.Log($"matrices len: {idx}");
        
        Shader.SetGlobalMatrixArray("_CharMatrices", charMatrices);
        Shader.SetGlobalVectorArray("_CharData", charData);
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

    [ContextMenu("Force Isolate and Purge Child Subscribers")]
    public void ForcePurgeChildSubscribers()
    {
        // Find every child subscriber, even if deep in the hierarchy
        var childPrimitives = GetComponentsInChildren<SdfPrimitiveSubscriber>(true);
        
        foreach (var prim in childPrimitives)
        {
            // 1. Permanently flag them as isolated tree components
            prim.isPartOfCsgTree = true;
            
            // 2. Forcefully strip them from any octree node they might be polluting
            prim.RecalculateTreePresence(); // Calling this while isPartOfCsgTree is handled above won't clear, let's clear directly:
        }
        
        // Explicitly wipe out registrations manually to be 100% safe
        InitializeHierarchy();
        RecalculateOctreePresence();
        
        Debug.Log("Cleaned up and isolated all sub-limbs successfully!");
    }

    //void OnDisable() => CleanUp();
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