using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public class SDFOctreeManager : MonoBehaviour
{
    public static SDFOctreeManager Instance { get; private set; }

    [Header("Octree Segment Rules")]
    [SerializeField] private float minNodeSize = 4f;
    [SerializeField] private int maxObjectsPerNode = 4;
    [SerializeField] private GameObject sdfContainerPrefab;

    private SDFOctreeNode rootNode;
    private BoxCollider boundaryCollider;

    void Awake()
    {
        Instance = this;
        boundaryCollider = GetComponent<BoxCollider>();
        boundaryCollider.isTrigger = true; // Turn off rigid collisions
    }

    void Start()
    {
        // 1. Gather all baseline pre-existing elements in the scene hierarchy 
        var baselineObjects = FindObjectsByType<SDFPrimitiveSubscriber>(FindObjectsSortMode.None);
        List<Transform> population = new List<Transform>();
        foreach (var obj in baselineObjects) population.Add(obj.transform);

        // 2. Drive structural dimensions cleanly from our editor box geometry configuration
        Bounds totalWorkspaceBounds = new Bounds(boundaryCollider.bounds.center, boundaryCollider.bounds.size);
        
        // 3. Kick off structural partition generation loop
        rootNode = new SDFOctreeNode(totalWorkspaceBounds, minNodeSize, maxObjectsPerNode, sdfContainerPrefab);
        rootNode.BuildInitialTree(population);
    }

    // Core lookup router called by moving inhabitants
    public SDFOctreeNode FindLeafNodeForPosition(Vector3 position)
    {
        return SearchNodeRecursive(rootNode, position);
    }

    private SDFOctreeNode SearchNodeRecursive(SDFOctreeNode currentNode, Vector3 targetPos)
    {
        if (currentNode == null || !currentNode.Bounds.Contains(targetPos)) return null;

        // If this is a structural branch, query the children sectors
        if (currentNode.childNodes != null)
        {
            foreach (var child in currentNode.childNodes)
            {
                SDFOctreeNode result = SearchNodeRecursive(child, targetPos);
                if (result != null) return result;
            }
        }

        // Found the exact containing leaf node
        return currentNode;
    }

    // Add this method to SDFOctreeManager.cs to check for room collapses
    public void TriggerCollapseCheck()
    {
        if (rootNode != null)
        {
            CheckCollapseRecursive(rootNode);
        }
    }

    private bool CheckCollapseRecursive(SDFOctreeNode node)
    {
        if (node == null) return true;
        if (node.childNodes == null) return node.localPrimitives.Count == 0;

        bool balanced = true;
        foreach (var child in node.childNodes)
        {
            if (!CheckCollapseRecursive(child)) balanced = false;
        }

        if (balanced)
        {
            return node.CheckAndCollapse();
        }

        return false;
    }

    // Update or add the OnDrawGizmos method inside your SDFOctreeManager MonoBehaviour
    void OnDrawGizmos()
    {
        // 1. Draw the master boundary box limit (Cyan)
        if (boundaryCollider != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(boundaryCollider.bounds.center, boundaryCollider.bounds.size);
        }

        // 2. Safely trigger the recursive grid outline cascading down from the root node
        if (Application.isPlaying && rootNode != null)
        {
            rootNode.DrawNodeGizmos();
        }
    }
}