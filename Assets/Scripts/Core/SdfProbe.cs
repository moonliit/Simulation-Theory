using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SDFGPUProbe : MonoBehaviour
{
    [Header("GPU System Links")]
    [Tooltip("Drop your mini compute shader here")]
    [SerializeField] private ComputeShader textureReaderCS;
    
    // Replace 'YourSDFCacheManager' with the actual class name of your system manager instance
    // or link it here via inspector.
    // [SerializeField] private YourSDFCacheManager cacheManager; 

    [Header("Manual Setup (If not using Cache Manager references directly)")]
    [SerializeField] private Transform volumeContainer; 
    [SerializeField] private Vector3Int indirectionRes = new Vector3Int(16, 16, 16);
    [SerializeField] private Vector3Int atlasRes = new Vector3Int(64, 64, 64);
    
    [Tooltip("The actual live GPU textures being written to by your system")]
    [SerializeField] private RenderTexture indirectionGridRT;
    [SerializeField] private RenderTexture brickAtlasRT;

    public void RequestGPUValue()
    {
        if (textureReaderCS == null)
        {
            Debug.LogError("[GPU Probe] Missing the SDFTextureReader compute shader component!");
            return;
        }

        // --- STEP 1: RESOLVE CONTAINER PARAMETERS ---
        // If your cache manager holds these values, grab them dynamically:
        // Vector3 volumeMin = cacheManager.VolumeMin; 
        // RenderTexture indirectionGridRT = cacheManager.IndirectionGridTexture;
        
        if (volumeContainer == null || indirectionGridRT == null || brickAtlasRT == null)
        {
            Debug.LogError("[GPU Probe] Missing inspector assignments for container or RenderTextures!");
            return;
        }

        Vector3 p = transform.position;
        
        // Calculate Bounding Box bounds from your container object (assuming a BoxCollider or MeshFilter defines it)
        Bounds bounds = volumeContainer.GetComponent<Collider>().bounds;
        Vector3 volumeMin = bounds.min;
        Vector3 volumeMax = bounds.max;
        Vector3 volumeSizes = volumeMax - volumeMin;

        // Boundary safety check
        if (p.x < volumeMin.x || p.y < volumeMin.y || p.z < volumeMin.z ||
            p.x > volumeMax.x || p.y > volumeMax.y || p.z > volumeMax.z)
        {
            Debug.LogWarning($"[GPU Probe] Position {p} is completely outside the volume boundaries.");
            return;
        }

        // --- STEP 2: CALCULATE INDIRECTION COORDINATES ---
        Vector3 volumeUV = new Vector3(
            (p.x - volumeMin.x) / volumeSizes.x,
            (p.y - volumeMin.y) / volumeSizes.y,
            (p.z - volumeMin.z) / volumeSizes.z
        );

        int indirectionX = Mathf.Clamp(Mathf.FloorToInt(volumeUV.x * indirectionRes.x), 0, indirectionRes.x - 1);
        int indirectionY = Mathf.Clamp(Mathf.FloorToInt(volumeUV.y * indirectionRes.y), 0, indirectionRes.y - 1);
        int indirectionZ = Mathf.Clamp(Mathf.FloorToInt(volumeUV.z * indirectionRes.z), 0, indirectionRes.z - 1);

        // --- STEP 3: CALCULATE LOCAL VOXEL ATLAS COORDINATES ---
        float cellWidth = volumeSizes.x / Mathf.Max(indirectionRes.x, 1.0f);
        Vector3 voxelMin = volumeMin + new Vector3(indirectionX, indirectionY, indirectionZ) * cellWidth;
        
        Vector3 localVolumeUV = new Vector3(
            Mathf.Clamp01((p.x - voxelMin.x) / cellWidth),
            Mathf.Clamp01((p.y - voxelMin.y) / cellWidth),
            Mathf.Clamp01((p.z - voxelMin.z) / cellWidth)
        );

        // --- STEP 4: FETCH DIRECTLY FROM THE GPU VIA COMPUTE BUFFER ---
        // Create an output array to accept results [0 = brickIndex, 1 = rawDistance]
        float[] gpuResults = new float[2];
        ComputeBuffer resultBuffer = new ComputeBuffer(2, sizeof(float));
        resultBuffer.SetData(gpuResults);

        int kernel = textureReaderCS.FindKernel("ReadIndirectionAndAtlas");
        
        // Bind targets
        textureReaderCS.SetTexture(kernel, "_SDFIndirectionGrid", indirectionGridRT);
        textureReaderCS.SetTexture(kernel, "_SDFBrickAtlas", brickAtlasRT);
        textureReaderCS.SetBuffer(kernel, "_OutputBuffer", resultBuffer);
        
        // Pass spatial tracking data
        textureReaderCS.SetInts("_IndirectionCoords", new int[] { indirectionX, indirectionY, indirectionZ });

        // We temporarily set atlas coordinates to zero just to fetch the brick index first
        textureReaderCS.SetInts("_AtlasCoords", new int[] { 0, 0, 0 });

        // Run the 1-pixel GPU probe execution pass
        textureReaderCS.Dispatch(kernel, 1, 1, 1);
        
        // Pull back intermediate brick allocations directly from VRAM
        resultBuffer.GetData(gpuResults);
        uint brickIndex = (uint)Mathf.RoundToInt(gpuResults[0]);

        if (brickIndex == 0)
        {
            Debug.Log($"<color=#00FFFF>[GPU Probe]</color> Position: {p} -> Indirection: [{indirectionX}, {indirectionY}, {indirectionZ}] -> <b>INACTIVE VOXEL</b> (No Brick Allocated)");
            resultBuffer.Release();
            return;
        }

        // Calculate brick layout properties inside atlas based on retrieved index
        uint bricksPerAxis = (uint)(atlasRes.x / 8);
        uint bricksPerSlice = bricksPerAxis * bricksPerAxis;

        Vector3 brickOriginInAtlas = new Vector3(
            (brickIndex % bricksPerAxis) * 8.0f,
            ((brickIndex / bricksPerAxis) % bricksPerAxis) * 8.0f,
            (brickIndex / bricksPerSlice) * 8.0f
        );

        // Target voxel offset layout matching debugMode 6 shader code
        Vector3 voxelOffset = localVolumeUV * 7.0f; 
        Vector3 atlasUVW_Pixels = brickOriginInAtlas + voxelOffset;

        int atlasX = Mathf.Clamp(Mathf.RoundToInt(atlasUVW_Pixels.x), 0, atlasRes.x - 1);
        int atlasY = Mathf.Clamp(Mathf.RoundToInt(atlasUVW_Pixels.y), 0, atlasRes.y - 1);
        int atlasZ = Mathf.Clamp(Mathf.RoundToInt(atlasUVW_Pixels.z), 0, atlasRes.z - 1);

        // Update atlas lookup coordinates on GPU and re-dispatch
        textureReaderCS.SetInts("_AtlasCoords", new int[] { atlasX, atlasY, atlasZ });
        textureReaderCS.Dispatch(kernel, 1, 1, 1);
        
        // Retrieve finalized structural values
        resultBuffer.GetData(gpuResults);
        float rawDistance = gpuResults[1];
        float decodedDistance = (rawDistance - 0.5f) * 2.0f; // Shows you your normalization offset layout

        Debug.Log($"<color=#00FF00>[GPU VRAM Probe Success]</color>\n" +
                  $"World Position Evaluated: {p}\n" +
                  $"Indirection Cell: [{indirectionX}, {indirectionY}, {indirectionZ}] | <color=yellow>Brick Index: {brickIndex}</color>\n" +
                  $"Atlas Texel Address: [{atlasX}, {atlasY}, {atlasZ}]\n" +
                  $"GPU Stored Value (Raw R): <color=orange>{rawDistance:F5}</color> | Decoded Distance: <color=white>{decodedDistance:F5}</color>");

        // Always clear compute buffer allocations from memory bounds immediately
        resultBuffer.Release();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SDFGPUProbe))]
public class SDFGPUProbeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        SDFGPUProbe probe = (SDFGPUProbe)target;
        
        GUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.8f, 1f);
        if (GUILayout.Button("Read Direct GPU VRAM Value", GUILayout.Height(35)))
        {
            probe.RequestGPUValue();
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif