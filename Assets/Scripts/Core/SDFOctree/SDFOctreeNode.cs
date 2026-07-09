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

    public void BuildInitialTree(List<Transform> initialPopulation)
    {
        localPrimitives.Clear();

        foreach (var p in initialPopulation)
        {
            if (p == null) continue;
            
            Bounds primitiveBounds = new Bounds(p.position, p.lossyScale);
            
            if (Bounds.Intersects(primitiveBounds))
            {
                localPrimitives.Add(p);
            }
        }

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

        ClearCluster();
    }

    public void AddPrimitiveDirectly(Transform primitive)
    {
        if (childNodes != null)
        {
            RouteToChildNode(primitive);
            return;
        }

        if (!localPrimitives.Contains(primitive))
        {
            localPrimitives.Add(primitive);

            if (localPrimitives.Count > maxObjectsPerNode && Bounds.size.x > minNodeSize)
            {
                Subdivide();

                foreach (var p in localPrimitives)
                {
                    RouteToChildNode(p);
                }
                localPrimitives.Clear();
            }
            else
            {
                UpdateClusterLifecycle();
            }
        }
    }

    private void RouteToChildNode(Transform primitive)
    {
        if (primitive == null) return;

        Bounds primBounds = new Bounds(primitive.position, primitive.lossyScale);

        foreach (var child in childNodes)
        {
            if (child.Bounds.Intersects(primBounds))
            {
                child.AddPrimitiveDirectly(primitive);
            }
        }
    }

    public void RemovePrimitiveDirectly(Transform primitive)
    {
        if (childNodes != null) return;

        if (localPrimitives.Contains(primitive))
        {
            localPrimitives.Remove(primitive);
            UpdateClusterLifecycle();
        }
    }

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

            foreach (var p in child.localPrimitives)
            {
                if (p != null) collapseEvaluationSet.Add(p);
            }
        }

        if (allChildrenAreLeaves && collapseEvaluationSet.Count <= maxObjectsPerNode)
        {
            foreach (var p in collapseEvaluationSet)
            {
                if (!localPrimitives.Contains(p))
                {
                    localPrimitives.Add(p);
                }
            }

            foreach (var child in childNodes)
            {
                child.localPrimitives.Clear();
                child.ClearCluster();
            }

            childNodes = null;
            
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

        if (renderClusterInstance == null && containerPrefab != null)
        {
            renderClusterInstance = Object.Instantiate(containerPrefab, Bounds.center, Quaternion.identity);
            renderClusterInstance.name = $"Sdf_LeafCluster_Size{Bounds.size.x}";

            if (SdfOctreeManager.Instance != null)
                renderClusterInstance.transform.SetParent(SdfOctreeManager.Instance.transform, true);

            clusterManager = renderClusterInstance.GetComponent<SdfSceneManager>();
            clusterWrapper = renderClusterInstance.GetComponent<SdfRigidBodyWrapper>();
        }

        if (renderClusterInstance != null)
        {
            if (clusterWrapper != null)
                clusterWrapper.ConfigureVolumeBounds(Bounds.center, Bounds.size * 1.05f);

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

                clusterManager.MarkTransformDirty();
            }
        }
    }

    private void ClearCluster()
    {
        if (renderClusterInstance != null)
        {

            if (SdfOctreeManager.Instance != null && SdfOctreeManager.IsShuttingDown)
            {
                renderClusterInstance = null;
                clusterManager = null;
                return;
            }

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

    public void DrawNodeGizmos()
    {
        if (childNodes != null)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);

            foreach (var child in childNodes)
            {
                if (child != null) child.DrawNodeGizmos();
            }
        }
        else
        {
            if (localPrimitives.Count > 0)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.4f);
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);

                Gizmos.DrawSphere(Bounds.center, 0.1f);
            }
            else
            {
                Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.08f);
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            }
        }
    }
}