using UnityEngine;
using System.Collections.Generic;

public class SDFPrimitiveSubscriber : MonoBehaviour
{
    public enum PrimitiveType { Cube, Sphere, Capsule }
    public enum OperationType { Union, Intersection, Subtraction }
    
    [Header("Primitive Settings")]
    [SerializeField] public PrimitiveType shapeType = PrimitiveType.Cube;
    [SerializeField] public OperationType operationType = OperationType.Union;

    // Track ALL nodes that this primitive physically intersects with
    private readonly List<SDFOctreeNode> occupiedNodes = new List<SDFOctreeNode>();
    
    private Vector3 lastPosition;
    private Vector3 lastScale;

    void Start()
    {
        var mesh = GetComponent<MeshRenderer>();
        mesh.enabled = false;

        lastPosition = transform.position;
        lastScale = transform.lossyScale;
        RecalculateTreePresence();
    }

    void Update()
    {
        if (transform.position != lastPosition || transform.lossyScale != lastScale)
        {
            lastPosition = transform.position;
            lastScale = transform.lossyScale;

            RecalculateTreePresence();
        }
    }

    private void Reset()
    {
        if (GetComponent<SphereCollider>() != null) shapeType = PrimitiveType.Sphere;
        else if (GetComponent<CapsuleCollider>() != null) shapeType = PrimitiveType.Capsule;
        else shapeType = PrimitiveType.Cube;
    }

    public void RecalculateTreePresence()
    {
        // 1. Tell all our old node sectors to remove us
        for (int i = occupiedNodes.Count - 1; i >= 0; i--)
        {
            occupiedNodes[i].RemovePrimitiveDirectly(transform);
        }
        occupiedNodes.Clear();

        if (SDFOctreeManager.Instance == null) return;

        // 2. Fetch the true physical world-space bounds of this primitive
        Bounds myBounds = GetWorldBounds();

        // 3. Query the tree to find every leaf node overlapping our volume
        List<SDFOctreeNode> overlappingLeaves = new List<SDFOctreeNode>();
        SDFOctreeManager.Instance.FindAllLeafNodesOverlapping(myBounds, overlappingLeaves);

        // 4. Register to all overlapping leaves
        foreach (var leaf in overlappingLeaves)
        {
            occupiedNodes.Add(leaf);
            leaf.AddPrimitiveDirectly(transform);
        }

        // 5. Trigger a structural merge/collapse pass over the engine if we abandoned empty spots
        SDFOctreeManager.Instance.TriggerCollapseCheck();
    }

    public Bounds GetWorldBounds()
    {
        // Drive boundaries from lossy scale or an attached collider if available
        return new Bounds(transform.position, transform.lossyScale);
    }

    public void GetCapsuleLocalDimensions(out float radius, out float height, out int direction)
    {
        CapsuleCollider cc = GetComponent<CapsuleCollider>();
        if (cc == null)
        {
            radius = 0.5f;
            height = 2f;
            direction = 1; // Default to Y-Axis
            return;
        }

        radius = cc.radius;
        height = cc.height;
        direction = cc.direction; // 0 = X, 1 = Y, 2 = Z
    }

    void OnDisable() => CleanUp();
    void OnDestroy() => CleanUp();

    private void CleanUp()
    {
        foreach (var node in occupiedNodes)
        {
            node.RemovePrimitiveDirectly(transform);
        }
        occupiedNodes.Clear();
        
        if (SDFOctreeManager.Instance != null)
            SDFOctreeManager.Instance.TriggerCollapseCheck();
    }

    void OnDrawGizmos()
    {
        // Only draw this subtle outline if we are working inside the Editor viewport
        if (!Application.isPlaying)
        {
            // Give subtractive objects a reddish hue, additive ones a soft white/gray hue
            //Gizmos.color = isSubtractive ? new Color(1f, 0.3f, 0.3f, 0.25f) : new Color(1f, 1f, 1f, 0.15f);
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);

            Gizmos.matrix = transform.localToWorldMatrix;

            if (shapeType == PrimitiveType.Cube)
            {
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }
            else if (shapeType == PrimitiveType.Sphere)
            {
                Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            }
            else if (shapeType == PrimitiveType.Capsule)
            {
                // Simple wire representation for capsules inside the editor view canvas
                Gizmos.DrawWireSphere(Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawWireSphere(Vector3.down * 0.5f, 0.5f);
            }
        }
    }

    public bool IsCube() => shapeType == PrimitiveType.Cube;
    public bool IsSphere() => shapeType == PrimitiveType.Sphere;
    public bool IsCapsule() => shapeType == PrimitiveType.Capsule;

    public bool IsUnion() => operationType == OperationType.Union;
    public bool IsIntersection() => operationType == OperationType.Intersection;
    public bool IsSubtraction() => operationType == OperationType.Subtraction;
}