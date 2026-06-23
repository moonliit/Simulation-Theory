using UnityEngine;

public class SDFVolumeData : MonoBehaviour
{
    [Header("Volume Configuration")]
    [Range(16, 64)] public int resolution = 32; // Voxel grid resolution
    public Material raymarchMaterial;

    private float[,,] voxelGrid;
    private Texture3D volumeTexture;
    private Renderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<Renderer>();
        InitializeGrid();
    }

    // 1. Setup the empty arrays and allocate the GPU Texture memory allocation
    void InitializeGrid()
    {
        voxelGrid = new float[resolution, resolution, resolution];
        
        // Create an uncompressed single-channel float texture (RFloat) for raw distance values
        volumeTexture = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false);
        volumeTexture.filterMode = FilterMode.Trilinear; // Gives us smooth hardware-interpolated lookups
        volumeTexture.wrapMode = TextureWrapMode.Clamp;

        // Instantiate unique material instance so multiple objects don't share the same texture
        if (meshRenderer != null && raymarchMaterial != null)
        {
            Material uniqueMat = new Material(raymarchMaterial);
            meshRenderer.material = uniqueMat;
            uniqueMat.SetTexture("_VolumeTex", volumeTexture);
        }

        ResetToBaseCube();
    }

    // 2. Build our starting primitive shape into the voxels
    [ContextMenu("Reset to Base Cube")]
    public void ResetToBaseCube()
    {
        float center = resolution / 2f;
        float halfExtent = resolution * 0.3f; // The starting size of our box primitive

        for (int z = 0; z < resolution; z++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Convert grid coordinates to distance vector components relative to center
                    float dx = Mathf.Abs(x - center) - halfExtent;
                    float dy = Mathf.Abs(y - center) - halfExtent;
                    float dz = Mathf.Abs(z - center) - halfExtent;

                    // Standard analytical box distance field calculated inside our voxels
                    float exteriorDist = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + 
                                                    Mathf.Max(dy, 0) * Mathf.Max(dy, 0) + 
                                                    Mathf.Max(dz, 0) * Mathf.Max(dz, 0));
                    float interiorDist = Mathf.Min(Mathf.Max(dx, Mathf.Max(dy, dz)), 0f);

                    // Store raw distance data normalized between [-0.5, 0.5] object scale
                    voxelGrid[x, y, z] = (exteriorDist + interiorDist) / resolution;
                }
            }
        }

        UploadToGPU();
    }

    // 3. THE FLEXIBLE CARVING ENGINE: Call this to bite chunks out of your shape!
    public void CarveSphere(Vector3 localPoint, float radius)
    {
        // Convert the incoming object space coordinates [-0.5, 0.5] into Voxel index coordinates [0, resolution]
        float gridX = (localPoint.x + 0.5f) * resolution;
        float gridY = (localPoint.y + 0.5f) * resolution;
        float gridZ = (localPoint.z + 0.5f) * resolution;
        float gridRadius = radius * resolution;

        // Optimize performance by calculating a bounding container box around the cut zone
        int minX = Mathf.Clamp(Mathf.FloorToInt(gridX - gridRadius), 0, resolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(gridX + gridRadius), 0, resolution - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(gridY - gridRadius), 0, resolution - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(gridY + gridRadius), 0, resolution - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt(gridZ - gridRadius), 0, resolution - 1);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt(gridZ + gridRadius), 0, resolution - 1);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Distance from this specific voxel node to our carving tool center
                    float distToCarveCenter = Mathf.Sqrt(Mathf.Pow(x - gridX, 2) + Mathf.Pow(y - gridY, 2) + Mathf.Pow(z - gridZ, 2));
                    float sphereSDF = (distToCarveCenter - gridRadius) / resolution;

                    // CSG Subtraction math: max(Base Shape, -Carving Shape)
                    voxelGrid[x, y, z] = Mathf.Max(voxelGrid[x, y, z], -sphereSDF);
                }
            }
        }

        UploadToGPU();
    }

    // 4. Flatten the 3D grid data array down into a flat buffer stream for the graphics hardware
    void UploadToGPU()
    {
        float[] flatColors = new float[resolution * resolution * resolution];
        int index = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    flatColors[index++] = voxelGrid[x, y, z];
                }
            }
        }

        volumeTexture.SetPixelData(flatColors, 0);
        volumeTexture.Apply(false); // Update graphics hardware memory immediately
    }
}