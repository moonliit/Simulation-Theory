using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class SDFCacheManager : MonoBehaviour
{
    public static SDFCacheManager Instance { get; private set; }

    [Header("Volume Configuration")]
    public Vector3 volumeCenter = Vector3.zero;
    public Vector3 volumeSize = new Vector3(20f, 20f, 20f);

    [Header("Resolution Settings")]
    // Low-res pointer layout (e.g. 16x16x16 bricks spanning space)
    public Vector3Int indirectionResolution = new Vector3Int(16, 16, 16);
    // Real size of a single voxel block inside the pool allocation atlas
    public const int BRICK_SIZE = 8; 
    // Max number of bricks our texture atlas can hold (e.g., 512 blocks)
    public int maxAtlasBricks = 512; 

    [Header("Shaders & Materials")]
    public ComputeShader sdfBakeCompute;
    public Material raymarchMaterial;

    // GPU Resource Targets
    private RenderTexture indirectionGridTex;
    private RenderTexture brickAtlasTex;

    // System Components
    private BrickAllocationPool allocationPool;
    private int clearGridKernelIdx;
    private int bakeBrickKernelIdx;
    private bool isCacheDirty = true;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        allocationPool = new BrickAllocationPool(maxAtlasBricks);

        InitializeCacheTextures();
        
        clearGridKernelIdx = sdfBakeCompute.FindKernel("ClearIndirectionGrid");
        bakeBrickKernelIdx = sdfBakeCompute.FindKernel("BakeBrick");

        UpdateGlobalShaderUniforms();
    }

    private void Update()
    {
        // Keep textures valid and update boundaries live while scrubbing vectors inside the editor
        if (!Application.isPlaying)
        {
            InitializeCacheTextures();
        }
    }

    void InitializeCacheTextures()
    {
        // Setup Indirection Grid (3D Texture, Single Channel 32-bit Integer)
        RenderTextureDescriptor indirectionDesc = new RenderTextureDescriptor(
            indirectionResolution.x, 
            indirectionResolution.y, 
            RenderTextureFormat.RFloat, 
            0 
        );
        indirectionDesc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        indirectionDesc.volumeDepth = indirectionResolution.z; 
        indirectionDesc.enableRandomWrite = true; 

        indirectionGridTex = new RenderTexture(indirectionDesc);
        indirectionGridTex.Create();

        // Calculate a 3D grid layout that holds exactly our max atlas blocks.
        // E.g., An 8x8x8 layout of bricks = 512 total bricks. 
        // 8 blocks * 8 texels per block = 64 texels per axis.
        int bricksPerAxis = Mathf.RoundToInt(Mathf.Pow(maxAtlasBricks, 1f / 3f));
        int atlasWidth = bricksPerAxis * BRICK_SIZE;   
        int atlasHeight = bricksPerAxis * BRICK_SIZE;  
        int atlasDepth = bricksPerAxis * BRICK_SIZE;   

        RenderTextureDescriptor atlasDesc = new RenderTextureDescriptor(
            atlasWidth, 
            atlasHeight, 
            RenderTextureFormat.RHalf, // 16-bit precision float for ultra-light weights
            0 
        );
        atlasDesc.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        atlasDesc.volumeDepth = atlasDepth; 
        atlasDesc.enableRandomWrite = true; 

        brickAtlasTex = new RenderTexture(atlasDesc);
        brickAtlasTex.Create();

        // Bind resource textures globally so both Compute and Fragment shaders access them instantly
        Shader.SetGlobalTexture("_SDFIndirectionGrid", indirectionGridTex);
        Shader.SetGlobalTexture("_SDFBrickAtlas", brickAtlasTex);
    }

    public void BakeRegion(Bounds worldBounds)
    {
        // Calculate cell spacing based on your volume configurations
        Vector3 totalMinBounds = volumeCenter - (volumeSize * 0.5f);
        Vector3 cellSizeWS = new Vector3(
            volumeSize.x / indirectionResolution.x,
            volumeSize.y / indirectionResolution.y,
            volumeSize.z / indirectionResolution.z
        );

        // Convert the world space bounding box edges into grid index coordinates
        Vector3 minLocal = worldBounds.min - totalMinBounds;
        Vector3 maxLocal = worldBounds.max - totalMinBounds;

        int minX = Mathf.Clamp(Mathf.FloorToInt(minLocal.x / cellSizeWS.x), 0, indirectionResolution.x - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(minLocal.y / cellSizeWS.y), 0, indirectionResolution.y - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt(minLocal.z / cellSizeWS.z), 0, indirectionResolution.z - 1);

        int maxX = Mathf.Clamp(Mathf.FloorToInt(maxLocal.x / cellSizeWS.x), 0, indirectionResolution.x - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt(maxLocal.y / cellSizeWS.y), 0, indirectionResolution.y - 1);
        int maxZ = Mathf.Clamp(Mathf.FloorToInt(maxLocal.z / cellSizeWS.z), 0, indirectionResolution.z - 1);

        // Loop ONLY through the grid blocks that this object actually touches!
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3Int gridCoords = new Vector3Int(x, y, z);
                    
                    Vector3 cellMinWS = totalMinBounds + Vector3.Scale(new Vector3(x, y, z), cellSizeWS);
                    Vector3 cellMaxWS = cellMinWS + cellSizeWS;

                    // Fire the compute dispatch for just this block
                    AllocateAndBakeBrickGPU(gridCoords, cellMinWS, cellMaxWS);
                }
            }
        }
    }

    /// <summary>
    /// Pure GPU pass. Clear out the grid on the GPU using lightweight compute kernels.
    /// </summary>
    public void ClearCachePipeline()
    {
        allocationPool.ResetPool();

        sdfBakeCompute.SetTexture(clearGridKernelIdx, "_WritableIndirectionGrid", indirectionGridTex);
        sdfBakeCompute.SetInt("_InvalidBrickValue", (int)BrickAllocationPool.INVALID_BRICK_INDEX);

        int threadGroupsX = Mathf.CeilToInt(indirectionResolution.x / 8f);
        int threadGroupsY = Mathf.CeilToInt(indirectionResolution.y / 8f);
        int threadGroupsZ = Mathf.CeilToInt(indirectionResolution.z / 8f);
        
        // This is practically instantaneous on the GPU (negligible execution cost)
        sdfBakeCompute.Dispatch(clearGridKernelIdx, threadGroupsX, threadGroupsY, threadGroupsZ);
    }

    /// <summary>
    /// Allocates single brick maps on-demand via the CPU coordinator list
    /// and invokes rendering bakes without freezing frames.
    /// </summary>
    public void AllocateAndBakeBrickGPU(Vector3Int gridCoords, Vector3 cellMinWS, Vector3 cellMaxWS)
    {
        if (indirectionGridTex == null || brickAtlasTex == null || sdfBakeCompute == null) return;
        if (allocationPool == null) allocationPool = new BrickAllocationPool(maxAtlasBricks);
        if (!allocationPool.TryAllocate(out uint targetBrickID)) return;

        CommandBuffer cmd = new CommandBuffer();
        cmd.name = $"BakeBrick_{targetBrickID}";

        // 1. Update the coordinate grid mapping index directly on the GPU texture layer
        sdfBakeCompute.SetTexture(bakeBrickKernelIdx, "_WritableIndirectionGrid", indirectionGridTex);
        sdfBakeCompute.SetInts("_TargetGridCoords", new int[] { gridCoords.x, gridCoords.y, gridCoords.z });
        
        // 2. Set the data blocks for the texture atlas payload update
        sdfBakeCompute.SetTexture(bakeBrickKernelIdx, "_WritableBrickAtlas", brickAtlasTex);
        sdfBakeCompute.SetInt("_TargetBrickIndex", (int)targetBrickID);
        sdfBakeCompute.SetVector("_BrickMinWS", cellMinWS);
        sdfBakeCompute.SetVector("_BrickMaxWS", cellMaxWS);

        // Dispatch 2x2x2 groups of 4x4x4 threads to cleanly evaluate all 8x8x8 voxels
        sdfBakeCompute.Dispatch(bakeBrickKernelIdx, 1, 1, 1);

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();
    }

    public void UpdateGlobalShaderUniforms()
    {
        Vector3 minBounds = volumeCenter - (volumeSize * 0.5f);
        Vector3 maxBounds = volumeCenter + (volumeSize * 0.5f);

        Shader.SetGlobalVector("_VolumeBoundsMin", minBounds);
        Shader.SetGlobalVector("_VolumeBoundsMax", maxBounds);
        Shader.SetGlobalVector("_IndirectionRes", new Vector4(indirectionResolution.x, indirectionResolution.y, indirectionResolution.z, 0));
        Shader.SetGlobalVector("_AtlasRes", new Vector4(brickAtlasTex.width, brickAtlasTex.height, brickAtlasTex.depth, 0));
    }

    private bool IsGridCellIntersectingShapes(Bounds cellBounds)
    {
        // TODO:
        // For our baseline, return true everywhere to force bake the entire scene context container.
        // Once confirmed working, we will connect your SdfSceneManager parameters here to only bake cells 
        // containing actual items!
        return true; 
    }

    void OnDestroy()
    {
        if (indirectionGridTex != null) indirectionGridTex.Release();
        if (brickAtlasTex != null) brickAtlasTex.Release();
    }

    // Quick visualization widget inside the Unity Editor scene tab
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(volumeCenter, volumeSize);
    }
}