using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class SdfOctreeManager : MonoBehaviour
{
    public static SdfOctreeManager Instance { get; private set; }
    public static bool IsShuttingDown { get; private set; } = false;

    [Header("Octree Segment Rules")]
    [SerializeField] private float minNodeSize = 4f;
    [SerializeField] private int maxObjectsPerNode = 4;
    [SerializeField] private GameObject sdfContainerPrefab;

    private SdfOctreeNode rootNode;
    private BoxCollider boundaryCollider;

    // ========================================================
    // 🌐 GLOBAL SHADER VECTOR IDS
    // ========================================================
    private static readonly int GlobalCubeMatricesID = Shader.PropertyToID("_GlobalCubeMatrices");
    private static readonly int GlobalCubeDataID = Shader.PropertyToID("_GlobalCubeData");
    private static readonly int GlobalCubeCountID = Shader.PropertyToID("_GlobalCubeCount");

    private static readonly int GlobalSpherePositionsID = Shader.PropertyToID("_GlobalSpherePositions");
    private static readonly int GlobalSphereRadiiID = Shader.PropertyToID("_GlobalSphereRadii");
    private static readonly int GlobalSphereCountID = Shader.PropertyToID("_GlobalSphereCount");

    private static readonly int GlobalCapsuleMatricesID = Shader.PropertyToID("_GlobalCapsuleMatrices");
    private static readonly int GlobalCapsuleDataID = Shader.PropertyToID("_GlobalCapsuleData");
    private static readonly int GlobalCapsuleCountID = Shader.PropertyToID("_GlobalCapsuleCount");

    // Reusable fixed array cache for GPU flushing
    private readonly Matrix4x4[] gCubeMatrices = new Matrix4x4[64];
    private readonly Vector4[] gCubeData = new Vector4[64];
    private readonly Vector4[] gSpherePositions = new Vector4[64];
    private readonly Vector4[] gSphereRadii = new Vector4[64];
    private readonly Matrix4x4[] gCapsuleMatrices = new Matrix4x4[64];
    private readonly Vector4[] gCapsuleData = new Vector4[64];

    private List<SdfPrimitiveSubscriber> activeCubes = new List<SdfPrimitiveSubscriber>();
    private List<SdfPrimitiveSubscriber> activeSpheres = new List<SdfPrimitiveSubscriber>();
    private List<SdfPrimitiveSubscriber> activeCapsules = new List<SdfPrimitiveSubscriber>();

    void Awake()
    {
        Instance = this;
        boundaryCollider = GetComponent<BoxCollider>();
        boundaryCollider.isTrigger = true; 
    }

    void OnApplicationQuit()
    {
        IsShuttingDown = true;
    }

    void Start()
    {
        var baselineObjects = FindObjectsByType<SdfPrimitiveSubscriber>(FindObjectsSortMode.None);
        List<Transform> population = new List<Transform>();
        
        activeCubes.Clear();
        activeSpheres.Clear();

        foreach (var obj in baselineObjects) 
        {
            population.Add(obj.transform);
            
            // Segregate by shape properties automatically
            if (obj.IsSphere())
                activeSpheres.Add(obj);
            else if (obj.IsCapsule())
                activeCapsules.Add(obj);
            else
                activeCubes.Add(obj);
        }

        Bounds totalWorkspaceBounds = new Bounds(boundaryCollider.bounds.center, boundaryCollider.bounds.size);
        rootNode = new SdfOctreeNode(totalWorkspaceBounds, minNodeSize, maxObjectsPerNode, sdfContainerPrefab);
        rootNode.BuildInitialTree(population);
    }

    void LateUpdate()
    {
        // Every frame, flush the global positions of all tracked objects directly down to the graphics card
        UpdateGlobalShaderBuffers();
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            RefreshEditorPrimitiveBuffers();
        }
    }

    private void RefreshEditorPrimitiveBuffers()
    {
        var baselineObjects = FindObjectsByType<SdfPrimitiveSubscriber>(FindObjectsSortMode.None);
        
        activeCubes.Clear();
        activeSpheres.Clear();
        activeCapsules.Clear();

        foreach (var obj in baselineObjects) 
        {
            if (obj == null || !obj.gameObject.activeInHierarchy) continue;
            
            if (obj.IsSphere())
                activeSpheres.Add(obj);
            else if (obj.IsCapsule())
                activeCapsules.Add(obj);
            else
                activeCubes.Add(obj);
        }

        UpdateGlobalShaderBuffers();
    }

    void UpdateGlobalShaderBuffers()
    {
        // update cubes
        int cubeCount = Mathf.Min(activeCubes.Count, 64);
        for (int i = 0; i < cubeCount; i++)
        {
            if (activeCubes[i] != null)
            {
                gCubeMatrices[i] = activeCubes[i].transform.worldToLocalMatrix;
                float opValue = (float)activeCubes[i].operationType;
                gCubeData[i] = new Vector4(opValue, 0, 0, 0);
            }
        }

        // update spheres
        int sphereCount = Mathf.Min(activeSpheres.Count, 64);
        for (int j = 0; j < sphereCount; j++)
        {
            if (activeSpheres[j] != null)
            {
                gSpherePositions[j] = activeSpheres[j].transform.position;
                float radius = Mathf.Max(
                    activeSpheres[j].transform.lossyScale.x,
                    Mathf.Max(activeSpheres[j].transform.lossyScale.y, activeSpheres[j].transform.lossyScale.z)
                ) * 0.5f;
                float opValue = (float)activeCubes[j].operationType;
                gSphereRadii[j] = new Vector4(radius, opValue, 0, 0);
            }
        }
        
        // update capsules
        int capsuleCount = Mathf.Min(activeCapsules.Count, 64);
        for (int k = 0; k < capsuleCount; k++)
        {
            if (activeCapsules[k] != null)
            {
                Transform t = activeCapsules[k].transform;

                // 💡 FIX: Pass a rigid matrix containing ONLY translation and rotation (no scale warp!)
                Matrix4x4 rigidMatrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                gCapsuleMatrices[k] = rigidMatrix.inverse;

                activeCapsules[k].GetCapsuleLocalDimensions(out float radius, out float height, out int direction);

                // 💡 FIX: Compute true world-space dimensions matching Unity's CapsuleCollider behavior
                Vector3 lossyScale = t.lossyScale;
                float worldRadius = radius;
                float worldHeight = height;

                if (direction == 0) // X-Aligned
                {
                    worldRadius = radius * Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
                    worldHeight = height * Mathf.Abs(lossyScale.x);
                }
                else if (direction == 1) // Y-Aligned
                {
                    worldRadius = radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
                    worldHeight = height * Mathf.Abs(lossyScale.y);
                }
                else // Z-Aligned
                {
                    worldRadius = radius * Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
                    worldHeight = height * Mathf.Abs(lossyScale.z);
                }

                float opValue = (float)activeCubes[k].operationType;
                gCapsuleData[k] = new Vector4(worldRadius, worldHeight, (float)direction, opValue);
            }
        }

        // Push global states to the GPU card architecture
        Shader.SetGlobalMatrixArray(GlobalCubeMatricesID, gCubeMatrices);
        Shader.SetGlobalVectorArray(GlobalCubeDataID, gCubeData);
        Shader.SetGlobalInt(GlobalCubeCountID, cubeCount);

        Shader.SetGlobalVectorArray(GlobalSpherePositionsID, gSpherePositions);
        Shader.SetGlobalVectorArray(GlobalSphereRadiiID, gSphereRadii);
        Shader.SetGlobalInt(GlobalSphereCountID, sphereCount);

        Shader.SetGlobalMatrixArray(GlobalCapsuleMatricesID, gCapsuleMatrices);
        Shader.SetGlobalVectorArray(GlobalCapsuleDataID, gCapsuleData);
        Shader.SetGlobalInt(GlobalCapsuleCountID, capsuleCount);
    }

    public SdfOctreeNode FindLeafNodeForPosition(Vector3 position)
    {
        return SearchNodeRecursive(rootNode, position);
    }

    private SdfOctreeNode SearchNodeRecursive(SdfOctreeNode currentNode, Vector3 targetPos)
    {
        if (currentNode == null || !currentNode.Bounds.Contains(targetPos)) return null;

        if (currentNode.childNodes != null)
        {
            foreach (var child in currentNode.childNodes)
            {
                SdfOctreeNode result = SearchNodeRecursive(child, targetPos);
                if (result != null) return result;
            }
        }

        return currentNode;
    }

    public void TriggerCollapseCheck()
    {
        if (rootNode != null) CheckCollapseRecursive(rootNode);
    }

    private bool CheckCollapseRecursive(SdfOctreeNode node)
    {
        if (node == null) return true;
        if (node.childNodes == null) return node.localPrimitives.Count == 0;

        bool balanced = true;
        foreach (var child in node.childNodes)
        {
            if (!CheckCollapseRecursive(child)) balanced = false;
        }

        if (balanced) return node.CheckAndCollapse();
        return false;
    }

    public void FindAllLeafNodesOverlapping(Bounds targetBounds, List<SdfOctreeNode> results)
    {
        if (rootNode != null) SearchOverlappingNodesRecursive(rootNode, targetBounds, results);
    }

    private void SearchOverlappingNodesRecursive(SdfOctreeNode currentNode, Bounds searchBounds, List<SdfOctreeNode> results)
    {
        if (!currentNode.Bounds.Intersects(searchBounds)) return;

        if (currentNode.childNodes != null)
        {
            foreach (var child in currentNode.childNodes)
            {
                if (child != null) SearchOverlappingNodesRecursive(child, searchBounds, results);
            }
        }
        else
        {
            results.Add(currentNode);
        }
    }

    void OnDrawGizmos()
    {
        if (boundaryCollider != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(boundaryCollider.bounds.center, boundaryCollider.bounds.size);
        }

        if (Application.isPlaying && rootNode != null)
        {
            rootNode.DrawNodeGizmos();
        }
    }

    #if UNITY_EDITOR
    void OnRenderObject()
    {
        if (!Application.isPlaying)
        {
            // Ensures the Scene view updates smoothly as you pan/rotate the editor camera
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
    }
    #endif
}