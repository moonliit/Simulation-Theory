using UnityEngine;
using System.Collections.Generic;

public class SdfPrimitiveSubscriber : MonoBehaviour
{
    public enum PrimitiveType { Cube, Sphere, Capsule, Torus }

    [Header("Primitive Settings")]
    [SerializeField] public PrimitiveType shapeType = PrimitiveType.Cube;

    [Header("CSG Operation")]
    [Tooltip("Si está activo, esta primitiva RESTA (carve) de la escena en vez de sumar. Úsalo para el corte de espada.")]
    public bool isSubtractive = false;

    [Header("Hierarchy Coupling")]
    [Tooltip("Si true, esta primitiva no toca al Octree Manager directamente. Un SdfCsgTreeRootInstance padre maneja sus bounds y datos.")]
    public bool isPartOfCsgTree = false;

    public static readonly List<SdfPrimitiveSubscriber> All = new List<SdfPrimitiveSubscriber>();

    private readonly List<SdfOctreeNode> occupiedNodes = new List<SdfOctreeNode>();

    private Vector3 lastPosition;
    private Vector3 lastScale;

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    void Start()
    {
        var mesh = GetComponent<MeshRenderer>();
        if (mesh != null) mesh.enabled = false;

        lastPosition = transform.position;
        lastScale = transform.lossyScale;

        if (GetComponent<SdfCsgNode>() != null || GetComponentInParent<SdfCsgNode>() != null)
        {
            isPartOfCsgTree = true;
        }

        if (!isPartOfCsgTree)
        {
            RecalculateTreePresence();
        }
    }

    void Update()
    {
        if (isPartOfCsgTree) return;

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
        for (int i = occupiedNodes.Count - 1; i >= 0; i--)
        {
            occupiedNodes[i].RemovePrimitiveDirectly(transform);
        }
        occupiedNodes.Clear();

        if (SdfOctreeManager.Instance == null) return;

        Bounds myBounds = GetWorldBounds();

        List<SdfOctreeNode> overlappingLeaves = new List<SdfOctreeNode>();
        SdfOctreeManager.Instance.FindAllLeafNodesOverlapping(myBounds, overlappingLeaves);

        foreach (var leaf in overlappingLeaves)
        {
            occupiedNodes.Add(leaf);
            leaf.AddPrimitiveDirectly(transform);
        }

        SdfOctreeManager.Instance.TriggerCollapseCheck();
    }

    public Bounds GetWorldBounds()
    {
        return new Bounds(transform.position, transform.lossyScale);
    }

    public void GetCapsuleLocalDimensions(out float radius, out float height, out int direction)
    {
        CapsuleCollider cc = GetComponent<CapsuleCollider>();
        if (cc == null)
        {
            radius = 0.5f;
            height = 2f;
            direction = 1;
            return;
        }

        radius = cc.radius;
        height = cc.height;
        direction = cc.direction;
    }

    void OnDisable()
    {
        All.Remove(this);
        CleanUp();
    }

    void OnDestroy() => CleanUp();

    private void CleanUp()
    {
        foreach (var node in occupiedNodes)
        {
            node.RemovePrimitiveDirectly(transform);
        }
        occupiedNodes.Clear();

        if (SdfOctreeManager.Instance != null)
            SdfOctreeManager.Instance.TriggerCollapseCheck();
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = isSubtractive ? new Color(1f, 0.3f, 0.3f, 0.25f) : new Color(1f, 1f, 1f, 0.15f);
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
                Gizmos.DrawWireSphere(Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawWireSphere(Vector3.down * 0.5f, 0.5f);
            }
        }
    }

    public bool IsCube() => shapeType == PrimitiveType.Cube;
    public bool IsSphere() => shapeType == PrimitiveType.Sphere;
    public bool IsCapsule() => shapeType == PrimitiveType.Capsule;

    public bool IsTorus() => shapeType == PrimitiveType.Torus;
}