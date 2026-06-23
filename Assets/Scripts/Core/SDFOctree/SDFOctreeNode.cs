using UnityEngine;
using System.Collections.Generic;

public class SDFOctreeNode
{
    public Bounds Bounds { get; private set; }
    private readonly float minNodeSize;
    private readonly int maxObjectsPerNode;
    private readonly GameObject containerPrefab;

    public List<Transform> localPrimitives = new List<Transform>();
    public SDFOctreeNode[] childNodes;
    
    private GameObject renderClusterInstance;
    private SDFSceneManager clusterManager;

    public SDFOctreeNode(Bounds bounds, float minSize, int maxObjects, GameObject prefab)
    {
        Bounds = bounds;
        minNodeSize = minSize;
        maxObjectsPerNode = maxObjects;
        containerPrefab = prefab;
    }

    public void BuildInitialTree(List<Transform> initialPopulation)
    {
        foreach (var p in initialPopulation)
        {
            if (Bounds.Contains(p.position)) localPrimitives.Add(p);
        }

        if (localPrimitives.Count > maxObjectsPerNode && Bounds.size.x > minNodeSize)
        {
            Subdivide();
            foreach (var child in childNodes) child.BuildInitialTree(localPrimitives);
            localPrimitives.Clear();
        }
        else
        {
            AssignPrimitivesToThisLeaf();
            UpdateClusterLifecycle();
        }
    }

    private void Subdivide()
    {
        childNodes = new SDFOctreeNode[8];
        Vector3 subSize = Bounds.size * 0.5f;

        int idx = 0;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 newCenter = Bounds.center + new Vector3(subSize.x * x * 0.5f, subSize.y * y * 0.5f, subSize.z * z * 0.5f);
                    childNodes[idx++] = new SDFOctreeNode(new Bounds(newCenter, subSize), minNodeSize, maxObjectsPerNode, containerPrefab);
                }
            }
        }
        
        // When partitioning a leaf into a branch, its local rendering box must be cleared
        ClearCluster();
    }

    // --- 📈 RUNTIME DYNAMIC SUBDIVISION ---
    public void AddPrimitiveDirectly(Transform primitive)
    {
        // If this node is actually a branch, route the primitive further down
        if (childNodes != null)
        {
            RouteToChildNode(primitive);
            return;
        }

        if (!localPrimitives.Contains(primitive))
        {
            localPrimitives.Add(primitive);
            
            // Re-verify if this room is now overcrowded at runtime!
            if (localPrimitives.Count > maxObjectsPerNode && Bounds.size.x > minNodeSize)
            {
                Subdivide();
                
                // Push all current roommates down into the new sub-rooms
                foreach (var p in localPrimitives)
                {
                    RouteToChildNode(p);
                }
                localPrimitives.Clear();
            }
            else
            {
                var subscriber = primitive.GetComponent<SDFPrimitiveSubscriber>();
                if (subscriber != null) subscriber.SetCurrentLeafNode(this);
                UpdateClusterLifecycle();
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
    public bool CheckAndCollapse()
    {
        if (childNodes == null) return localPrimitives.Count == 0;

        bool allChildrenAreLeaves = true;
        int totalObjectsInChildren = 0;

        foreach (var child in childNodes)
        {
            if (child.childNodes != null) allChildrenAreLeaves = false;
            totalObjectsInChildren += child.localPrimitives.Count;
        }

        // If children are empty or their combined grouping fits comfortably into a single room, collapse them!
        if (allChildrenAreLeaves && totalObjectsInChildren <= maxObjectsPerNode)
        {
            // Pull elements back up to this parent room
            foreach (var child in childNodes)
            {
                foreach (var p in child.localPrimitives)
                {
                    if (p != null)
                    {
                        localPrimitives.Add(p);
                        var sub = p.GetComponent<SDFPrimitiveSubscriber>();
                        if (sub != null) sub.SetCurrentLeafNode(this);
                    }
                }
                child.ClearCluster();
            }

            childNodes = null; // Delete child partitions
            UpdateClusterLifecycle();
            return false;
        }

        return false;
    }

    private void RouteToChildNode(Transform primitive)
    {
        foreach (var child in childNodes)
        {
            if (child.Bounds.Contains(primitive.position))
            {
                child.AddPrimitiveDirectly(primitive);
                break;
            }
        }
    }

    private void AssignPrimitivesToThisLeaf()
    {
        foreach (var p in localPrimitives)
        {
            var subscriber = p.GetComponent<SDFPrimitiveSubscriber>();
            if (subscriber != null) subscriber.SetCurrentLeafNode(this);
        }
    }

    // --- (Keep your exact UpdateClusterLifecycle and ClearCluster methods here...)
    // Manages the creation, removal, parenting, and sizing of the volume rendering container
    private SDFRigidBodyWrapper clusterWrapper;

    public void UpdateClusterLifecycle()
    {
        if (childNodes != null) return; 

        if (localPrimitives.Count == 0)
        {
            ClearCluster();
            return;
        }

        // 1. Spawn the SDF_Object container prefab if a primitive just entered an empty zone
        if (renderClusterInstance == null && containerPrefab != null)
        {
            renderClusterInstance = Object.Instantiate(containerPrefab, Bounds.center, Quaternion.identity);
            renderClusterInstance.name = $"SDF_LeafCluster_Size{Bounds.size.x}";

            if (SDFOctreeManager.Instance != null)
                renderClusterInstance.transform.SetParent(SDFOctreeManager.Instance.transform, true);

            // Cache both components sitting on the prefab root
            clusterManager = renderClusterInstance.GetComponent<SDFSceneManager>();
            clusterWrapper = renderClusterInstance.GetComponent<SDFRigidBodyWrapper>();
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
                    var sub = p.GetComponent<SDFPrimitiveSubscriber>();
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
            Object.Destroy(renderClusterInstance);
            renderClusterInstance = null;
            clusterManager = null;
        }
    }

    // Add this method anywhere inside your SDFOctreeNode class
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