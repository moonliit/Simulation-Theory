using UnityEngine;

[ExecuteAlways]
public class ComputePrebakeManager : MonoBehaviour
{
    public static ComputePrebakeManager Instance; // Singleton reference for fast access

    public ComputeShader raymarchComputeShader;
    public Material displayMaterial;

    [Header("Slice Diagnostic Controls")]
    [Range(0, 2)] public int targetAxis = 2; 
    [Range(0f, 1f)] public float sliceDepth = 0.5f; 
    public Vector3 atlasResolution = new Vector3(512, 512, 512);

    private RenderTexture screenCacheTex;
    private int raymarchKernelIdx;

    // Track last known screen state to prevent redundant asset allocations
    private int lastWidth = 0;
    private int lastHeight = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (raymarchComputeShader == null || displayMaterial == null) return;
        raymarchKernelIdx = raymarchComputeShader.FindKernel("CSMain");

        // Perform initial buffer creation matching current window scale
        RebuildScreenCacheTexture();
    }

    // Call this explicitly AFTER the bricks are baked (or in Update for diagnostic tracking)
    public void RenderScreenCache()
    {
        if (raymarchComputeShader == null) return;

        // 1. Dynamic Check: If screen bounds changed, rebuild the asset inline immediately
        if (Screen.width != lastWidth || Screen.height != lastHeight || screenCacheTex == null)
        {
            RebuildScreenCacheTexture();
        }

        // Double check safety fallback to prevent crashes if texture creation failed (e.g. minimized window)
        if (screenCacheTex == null) return;

        Camera cam = Camera.main;
        if (cam == null) return; // Guard against scene camera changes in the Editor
        
        // Pass precise screen width/height matching the actual texture storage space
        raymarchComputeShader.SetInt("_ScreenWidth", screenCacheTex.width);
        raymarchComputeShader.SetInt("_ScreenHeight", screenCacheTex.height);
        raymarchComputeShader.SetVector("_WorldSpaceCameraPos", cam.transform.position);
        
        // Explicit projection transformations inverted for ray casting tracking
        Matrix4x4 invView = cam.cameraToWorldMatrix;
        Matrix4x4 invProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false).inverse;
        
        raymarchComputeShader.SetMatrix("_InverseViewMatrix", invView);
        raymarchComputeShader.SetMatrix("_InverseProjectionMatrix", invProj);
        
        // Extract depth calculation uniforms matching core standard shader configurations
        Vector4 zBufferParams = Shader.GetGlobalVector("_ZBufferParams");
        raymarchComputeShader.SetVector("_ZBufferParams", zBufferParams);

        // Track active depth texture pass if binding native graphics loops
        // raymarchComputeShader.SetTexture(raymarchKernelIdx, "_CameraDepthTexture", Shader.GetGlobalTexture("_CameraDepthTexture"));

        // Execute 
        raymarchComputeShader.SetTexture(raymarchKernelIdx, "_WritableScreenResult", screenCacheTex);
        
        // Calculate thread groupings based strictly on the current texture dimensions
        int threadGroupsX = Mathf.CeilToInt(screenCacheTex.width / 8f);
        int threadGroupsY = Mathf.CeilToInt(screenCacheTex.height / 8f);
        raymarchComputeShader.Dispatch(raymarchKernelIdx, threadGroupsX, threadGroupsY, 1);
    }

    private void RebuildScreenCacheTexture()
    {
        // Safe release of existing asset structure to avoid VRAM accumulation leaks
        if (screenCacheTex != null)
        {
            screenCacheTex.Release();
            screenCacheTex = null;
        }

        lastWidth = Screen.width;
        lastHeight = Screen.height;

        // Safeguard parameters against 0-sized allocation issues when Editor layout updates
        int safeWidth = Mathf.Max(lastWidth, 8);
        int safeHeight = Mathf.Max(lastHeight, 8);

        screenCacheTex = new RenderTexture(safeWidth, safeHeight, 0, RenderTextureFormat.ARGB32);
        screenCacheTex.enableRandomWrite = true;
        screenCacheTex.filterMode = FilterMode.Point;
        screenCacheTex.Create();

        // Broadcast the fresh reference globally so that all referencing display materials pick it up
        Shader.SetGlobalTexture("_ScreenCacheTex", screenCacheTex);
    }

    void OnDestroy()
    {
        if (screenCacheTex != null) screenCacheTex.Release();
    }
}