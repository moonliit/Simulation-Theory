using UnityEngine;
using System.Collections.Generic;

public class SdfOctreeNode
{
    public Bounds Bounds { get; private set; }
    private readonly float minNodeSize;
    private readonly int maxObjectsPerNode;
    private readonly GameObject containerPrefab;

    public List<Transform> localPrimitives = new List<Transform>();
    public SdfOctreeNode[] childNodes;
    
    private GameObject renderClusterInstance;
    private SdfSceneManager clusterManager;

    public SdfOctreeNode(Bounds bounds, float minSize, int maxObjects, GameObject prefab)
    {
        Bounds = bounds;
        minNodeSize = minSize;
        maxObjectsPerNode = maxObjects;
        containerPrefab = prefab;
    }

    // Structural initialization: Subdivides using intersections instead of points
    public void BuildInitialTree(List<Transform> initialPopulation)
    {
        localPrimitives.Clear();

        // FILTER USING BOUNDING BOX OVERLAPS
        foreach (var p in initialPopulation)
        {
            if (p == null) continue;
            
            // Generate a bounding box context for the startup layout pass
            Bounds primitiveBounds = new Bounds(p.position, p.lossyScale);
            
            if (Bounds.Intersects(primitiveBounds))
            {
                localPrimitives.Add(p);
            }
        }

        // Subdivide if we exceed the limit and are still larger than the min size limit
        if (localPrimitives.Count > maxObjectsPerNode && Bounds.size.x > minNodeSize)
        {
            Subdivide();
            
            foreach (var child in childNodes)
            {
                child.BuildInitialTree(localPrimitives);
            }
            localPrimitives.Clear();
        }
        else
        {
            UpdateClusterLifecycle();
        }
    }

    private void Subdivide()
    {
        childNodes = new SdfOctreeNode[8];
        Vector3 subSize = Bounds.size * 0.5f;

        int idx = 0;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 newCenter = Bounds.center + new Vector3(subSize.x * x * 0.5f, subSize.y * y * 0.5f, subSize.z * z * 0.5f);
                    childNodes[idx++] = new SdfOctreeNode(new Bounds(newCenter, subSize), minNodeSize, maxObjectsPerNode, containerPrefab);
                }
            }
        }
        
        // When partitioning a leaf into a branch, its local rendering box must be cleared
        ClearCluster();
    }

    // --- 📈 RUNTIME DYNAMIC SUBDIVISION ---
    // --- 📈 RUNTIME DYNAMIC SUBDIVISION (VOLUME AWARE) ---
    public void AddPrimitiveDirectly(Transform primitive)
    {
        // If this node is a branch, route the primitive's full volume down to all overlapping children
        if (childNodes != null)
        {
            RouteToChildNode(primitive);
            return;
        }

        if (!localPrimitives.Contains(primitive))
        {
            localPrimitives.Add(primitive);
            
            // Re-verify if this leaf room is overcrowded at runtime
            if (localPrimitives.Count > maxObjectsPerNode && Bounds.size.x > minNodeSize)
            {
                Subdivide();
                
                // Redistribute current roommates down into the new sub-rooms based on intersection
                foreach (var p in localPrimitives)
                {
                    RouteToChildNode(p);
                }
                localPrimitives.Clear();
            }
            else
            {
                // CLEANED UP: We no longer call SetCurrentLeafNode(this) here!
                // The subscriber handles its own list tracking inside RecalculateTreePresence()
                UpdateClusterLifecycle();
            }
        }
    }

    private void RouteToChildNode(Transform primitive)
    {
        if (primitive == null) return;
        
        // Form the bounding box of the primitive
        Bounds primBounds = new Bounds(primitive.position, primitive.lossyScale);

        // Push it into EVERY child node it touches
        foreach (var child in childNodes)
        {
            if (child.Bounds.Intersects(primBounds))
            {
                child.AddPrimitiveDirectly(primitive);
            }
        }
    }

    // --- 📉 RUNTIME DYNAMIC COLLAPSING ---
    public void RemovePrimitiveDirectly(Transform primitive)
    {
        if (childNodes != null) return; // Branches don't hold list elements directly

        if (localPrimitives.Contains(primitive))
        {
            localPrimitives.Remove(primitive);
            UpdateClusterLifecycle();
        }
    }

    // Checks if all sibling nodes under a branch are empty, allowing a collapse merge
    // Cache a unique collection list to avoid runtime GC allocation passes
    private readonly static HashSet<Transform> collapseEvaluationSet = new HashSet<Transform>();

    public bool CheckAndCollapse()
    {
        if (childNodes == null) return localPrimitives.Count == 0;

        bool allChildrenAreLeaves = true;
        collapseEvaluationSet.Clear();

        foreach (var child in childNodes)
        {
            if (child.childNodes != null) 
                allChildrenAreLeaves = false;

            // Gather only unique primitive references across all sibling sectors
            foreach (var p in child.localPrimitives)
            {
                if (p != null) collapseEvaluationSet.Add(p);
            }
        }

        // Collapse if children are deep-end leaves and their unique overlapping grouping 
        // fits comfortably within a single parent room's capacity limits
        if (allChildrenAreLeaves && collapseEvaluationSet.Count <= maxObjectsPerNode)
        {
            // Pull the unique elements back up into this parent room
            foreach (var p in collapseEvaluationSet)
            {
                if (!localPrimitives.Contains(p))
                {
                    localPrimitives.Add(p);
                }
            }

            // Clean up the trailing child structures completely
            foreach (var child in childNodes)
            {
                child.localPrimitives.Clear();
                child.ClearCluster();
            }

            childNodes = null; // Dissolve child partitions
            
            // CLEANED UP: Force all inhabitants to find their actual new leaf arrangements 
            // across the newly simplified node grid layout.
            foreach (var p in localPrimitives)
            {
                var sub = p.GetComponent<SdfPrimitiveSubscriber>();
                if (sub != null) sub.RecalculateTreePresence();
            }

            UpdateClusterLifecycle();
            return true;
        }

        return false;
    }

    // CLEANED UP: Completely removed the redundant single-assignment AssignPrimitivesToThisLeaf() method.
    // Primitives handle tracking via their own occupiedNodes list mapping now.

    // --- (Keep your exact UpdateClusterLifecycle and ClearCluster methods here...)
    // Manages the creation, removal, parenting, and sizing of the volume rendering container
    private SdfRigidBodyWrapper clusterWrapper;

    public void UpdateClusterLifecycle()
    {
        if (childNodes != null) return; 

        if (localPrimitives.Count == 0)
        {
            ClearCluster();
            return;
        }

        if (SdfOctreeManager.IsShuttingDown)
        {
            return;
        }

        // 1. Spawn the Sdf_Object container prefab if a primitive just entered an empty zone
        if (renderClusterInstance == null && containerPrefab != null)
        {
            renderClusterInstance = Object.Instantiate(containerPrefab, Bounds.center, Quaternion.identity);
            renderClusterInstance.name = $"Sdf_LeafCluster_Size{Bounds.size.x}";

            if (SdfOctreeManager.Instance != null)
                renderClusterInstance.transform.SetParent(SdfOctreeManager.Instance.transform, true);

            // Cache both components sitting on the prefab root
            clusterManager = renderClusterInstance.GetComponent<SdfSceneManager>();
            clusterWrapper = renderClusterInstance.GetComponent<SdfRigidBodyWrapper>();
        }

        if (renderClusterInstance != null)
        {
            // 2. Shape the hidden raymarching bounds mesh to fill the node boundaries
            if (clusterWrapper != null)
                clusterWrapper.ConfigureVolumeBounds(Bounds.center, Bounds.size * 1.05f);

            // 3. Clear and hand over the list of shapes directly to the local Scene Manager
            if (clusterManager != null)
            {
                clusterManager.localCubes.Clear();
                clusterManager.localSpheres.Clear();

                foreach (var p in localPrimitives)
                {
                    if (p == null) continue;
                    var sub = p.GetComponent<SdfPrimitiveSubscriber>();
                    if (sub == null) continue;

                    if (sub.IsSphere())
                        clusterManager.localSpheres.Add(p);
                    else
                        clusterManager.localCubes.Add(p);
                }

                // Tell the local scene manager its datasets changed and need a GPU re-upload pass
                clusterManager.MarkTransformDirty();
            }
        }
    }

    private void ClearCluster()
    {
        if (renderClusterInstance != null)
        {
            // SAFETY CHECK: If the game is shutting down, let Unity handle cleanup naturally
            if (SdfOctreeManager.Instance != null && SdfOctreeManager.IsShuttingDown)
            {
                renderClusterInstance = null;
                clusterManager = null;
                return;
            }

            // Normal runtime destruction
            if (Application.isPlaying)
            {
                Object.Destroy(renderClusterInstance);
            }
            else
            {
                Object.DestroyImmediate(renderClusterInstance);
            }

            renderClusterInstance = null;
            clusterManager = null;
        }
    }

    // Add this method anywhere inside your SdfOctreeNode class
    public void DrawNodeGizmos()
    {
        if (childNodes != null)
        {
            // This is a branch node—color it a subtle, thin gray and pass down to children
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);

            foreach (var child in childNodes)
            {
                if (child != null) child.DrawNodeGizmos();
            }
        }
        else
        {
            // This is a leaf node! 
            if (localPrimitives.Count > 0)
            {
                // If it contains active carving shapes, highlight it in bright solid green
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.4f);
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);
                
                // Optional: Draw a tiny solid dot at the center of populated zones
                Gizmos.DrawSphere(Bounds.center, 0.1f);
            }
            else
            {
                // Completely empty leaf node—draw a faint, thin blue wire box
                Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.08f);
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            }
        }
    }
}