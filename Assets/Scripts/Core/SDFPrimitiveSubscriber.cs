using UnityEngine;

public class SDFPrimitiveSubscriber : MonoBehaviour
{
    public enum PrimitiveType { Cube, Sphere }
    
    [Header("Primitive Settings")]
    [SerializeField] public PrimitiveType type = PrimitiveType.Cube;

    // The current fixed structural octree leaf this object physically occupies
    private SDFOctreeNode currentLeafNode;
    
    private Vector3 lastPosition;
    private Vector3 lastScale;

    void Start()
    {
        lastPosition = transform.position;
        lastScale = transform.lossyScale;

        // Register to the Octree Manager if spawned at runtime or initialized late
        if (currentLeafNode == null && SDFOctreeManager.Instance != null)
        {
            MigrateToNewLeaf();
        }
    }

    public void SetCurrentLeafNode(SDFOctreeNode node)
    {
        currentLeafNode = node;
    }

    void Update()
    {
        // Optimization: Only process spatial updates if we actually moved or scaled
        if (transform.position != lastPosition || transform.lossyScale != lastScale)
        {
            lastPosition = transform.position;
            lastScale = transform.lossyScale;

            if (currentLeafNode != null)
            {
                // Check if we physically broke through the boundaries of our current leaf sector
                if (!currentLeafNode.Bounds.Contains(transform.position))
                {
                    MigrateToNewLeaf();
                }
                else
                {
                    // Still inside the same leaf, but we moved/scaled—flag the local shader container to adjust
                    currentLeafNode.UpdateClusterLifecycle();
                }
            }
            else if (SDFOctreeManager.Instance != null)
            {
                // If we floated outside the world boundary earlier, try to find a valid home node again
                MigrateToNewLeaf();
            }
        }
    }

    private void MigrateToNewLeaf()
    {
        if (currentLeafNode != null)
        {
            currentLeafNode.RemovePrimitiveDirectly(transform);
            
            // Structural validation call: see if the room we just abandoned can collapse now
            SDFOctreeManager.Instance.TriggerCollapseCheck();
        }

        SDFOctreeNode newHomeLeaf = SDFOctreeManager.Instance.FindLeafNodeForPosition(transform.position);
        if (newHomeLeaf != null)
        {
            newHomeLeaf.AddPrimitiveDirectly(transform);
        }
        else
        {
            currentLeafNode = null;
        }
    }

    void OnDisable()
    {
        CleanUpReferences();
    }

    void OnDestroy()
    {
        CleanUpReferences();
    }

    private void CleanUpReferences()
    {
        if (currentLeafNode != null)
        {
            currentLeafNode.RemovePrimitiveDirectly(transform);
            currentLeafNode = null;
        }
    }

    // Public utilities for the Octree lifecycle checks
    public bool IsSphere() => type == PrimitiveType.Sphere;
}