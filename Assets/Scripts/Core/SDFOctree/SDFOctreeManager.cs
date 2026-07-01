using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class SdfOctreeManager : MonoBehaviour
{
    private const int MAX_STANDALONE_PRIMITIVES = 64;
    private const int MAX_CSG_INSTANCES = 32;
    private const int MAX_TOTAL_CSG_NODES = 128;

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

    private static readonly int CharMatricesID = Shader.PropertyToID("_CharMatrices");
    private static readonly int CharDataID = Shader.PropertyToID("_CharData");
    private static readonly int CsgInstanceOffsetsID = Shader.PropertyToID("_CsgInstanceOffsets");
    private static readonly int CsgAssetTypesID = Shader.PropertyToID("_CsgAssetTypes");
    private static readonly int ActiveCsgInstanceCountID = Shader.PropertyToID("_ActiveCsgInstanceCount");

    // Reusable fixed array cache for GPU flushing
    private readonly Matrix4x4[] gCubeMatrices = new Matrix4x4[MAX_STANDALONE_PRIMITIVES];
    private readonly Vector4[] gCubeData = new Vector4[MAX_STANDALONE_PRIMITIVES];
    private readonly Vector4[] gSpherePositions = new Vector4[MAX_STANDALONE_PRIMITIVES];
    private readonly Vector4[] gSphereRadii = new Vector4[MAX_STANDALONE_PRIMITIVES];
    private readonly Matrix4x4[] gCapsuleMatrices = new Matrix4x4[MAX_STANDALONE_PRIMITIVES];
    private readonly Vector4[] gCapsuleData = new Vector4[MAX_STANDALONE_PRIMITIVES];

    private readonly Matrix4x4[] charMatrices = new Matrix4x4[MAX_TOTAL_CSG_NODES];
    private readonly Vector4[] charData = new Vector4[MAX_TOTAL_CSG_NODES];
    private readonly float[] csgInstanceOffsets = new float[MAX_CSG_INSTANCES];
    private readonly float[] csgAssetTypes = new float[MAX_CSG_INSTANCES];

    private List<SdfPrimitiveSubscriber> activeCubes = new List<SdfPrimitiveSubscriber>();
    private List<SdfPrimitiveSubscriber> activeSpheres = new List<SdfPrimitiveSubscriber>();
    private List<SdfPrimitiveSubscriber> activeCapsules = new List<SdfPrimitiveSubscriber>();
    private static List<SdfCsgTreeRootInstance> activeInstances = new List<SdfCsgTreeRootInstance>();

    public static void RegisterCsgInstance(SdfCsgTreeRootInstance instance)
    {
        if (!activeInstances.Contains(instance)) activeInstances.Add(instance);
    }

    public static void UnregisterCsgInstance(SdfCsgTreeRootInstance instance)
    {
        activeInstances.Remove(instance);
    }

    private SDFCacheManager cacheManager;

    void Awake()
    {
        Instance = this;
        boundaryCollider = GetComponent<BoxCollider>();
        boundaryCollider.isTrigger = true;

        cacheManager = FindFirstObjectByType<SDFCacheManager>(); 
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
        activeCapsules.Clear();

        foreach (var obj in baselineObjects) 
        {
            if (obj.isPartOfCsgTree) continue;

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
        //if (!Application.isPlaying)
        //{
            RefreshEditorPrimitiveBuffers();
            UpdateCsgBuffers();
        //}
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
            if (obj.isPartOfCsgTree) continue;
            
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
        int cubeCount = Mathf.Min(activeCubes.Count, MAX_STANDALONE_PRIMITIVES);
        for (int i = 0; i < cubeCount; i++)
        {
            if (activeCubes[i] != null)
            {
                Transform t = activeCubes[i].transform;
                Matrix4x4 rigidMatrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                gCubeMatrices[i] = rigidMatrix.inverse;
                Vector3 worldHalfExtents = t.lossyScale * 0.5f;
                gCubeData[i] = new Vector4(worldHalfExtents.x, worldHalfExtents.y, worldHalfExtents.z, 0);
            }
        }

        // update spheres
        int sphereCount = Mathf.Min(activeSpheres.Count, MAX_STANDALONE_PRIMITIVES);
        for (int j = 0; j < sphereCount; j++)
        {
            if (activeSpheres[j] != null)
            {
                gSpherePositions[j] = activeSpheres[j].transform.position;
                float radius = Mathf.Max(
                    activeSpheres[j].transform.lossyScale.x,
                    Mathf.Max(activeSpheres[j].transform.lossyScale.y, activeSpheres[j].transform.lossyScale.z)
                ) * 0.5f;
                gSphereRadii[j] = new Vector4(radius, 0, 0, 0);
            }
        }
        
        // update capsules
        int capsuleCount = Mathf.Min(activeCapsules.Count, MAX_STANDALONE_PRIMITIVES);
        for (int k = 0; k < capsuleCount; k++)
        {
            if (activeCapsules[k] != null)
            {
                Transform t = activeCapsules[k].transform;
                Matrix4x4 rigidMatrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                gCapsuleMatrices[k] = rigidMatrix.inverse;

                activeCapsules[k].GetCapsuleLocalDimensions(out float radius, out float height, out int direction);

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

                gCapsuleData[k] = new Vector4(worldRadius, worldHeight, (float)direction, 0);
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

    void UpdateCsgBuffers()
    {
        int currentBufferPtr = 0;
        int instanceCount = Mathf.Min(activeInstances.Count, MAX_CSG_INSTANCES);

        // Initialize clean fallbacks for the entire global block buffer
        for (int i = 0; i < MAX_CSG_INSTANCES; i++)
        {
            charMatrices[i] = Matrix4x4.identity;
            charData[i] = new Vector4(0f, 0f, 0f, -1f); 
        }

        for (int instanceIdx = 0; instanceIdx < instanceCount; instanceIdx++)
        {
            var instance = activeInstances[instanceIdx];

            // Track exactly where this specific instance starts in our global heap
            instance.globalBufferStartIndex = currentBufferPtr;
            csgInstanceOffsets[instanceIdx] = (float)currentBufferPtr;
            csgAssetTypes[instanceIdx] = (float)instance.assetType;
            currentBufferPtr = instance.PackDataIntoBuffers(charMatrices, charData, currentBufferPtr, MAX_TOTAL_CSG_NODES);
        }

        // Send everything packed together down to the shader uniform locations
        Shader.SetGlobalMatrixArray(CharMatricesID, charMatrices);
        Shader.SetGlobalVectorArray(CharDataID, charData);
        Shader.SetGlobalFloatArray(CsgInstanceOffsetsID, csgInstanceOffsets);
        Shader.SetGlobalFloatArray(CsgAssetTypesID, csgAssetTypes);
        Shader.SetGlobalInt(ActiveCsgInstanceCountID, instanceCount);
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