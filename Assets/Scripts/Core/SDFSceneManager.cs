using UnityEngine;
using System.Collections.Generic;

public class SDFSceneManager : MonoBehaviour
{
    // Local tracking lists populated directly by the Octree Leaf Node
    [HideInInspector] public readonly List<Transform> localCubes = new List<Transform>();
    [HideInInspector] public readonly List<Transform> localSpheres = new List<Transform>();

    private Material targetMaterial;
    private bool isShaderDirty = false;

    // Fixed GPU buffer allocations per cluster instance
    private readonly Vector4[] cubePositions = new Vector4[32];
    private readonly Vector4[] cubeSizes = new Vector4[32];
    private readonly Vector4[] spherePositions = new Vector4[32];
    private readonly Vector4[] sphereRadii = new Vector4[32];

    public void InitializeWithMaterial(Material instanceMaterial)
    {
        targetMaterial = instanceMaterial;
        MarkTransformDirty();
    }

    public void MarkTransformDirty()
    {
        isShaderDirty = true;
    }

    void LateUpdate()
    {
        // Only push data to the GPU if something inside our leaf node actually moved or scaled
        if (isShaderDirty && targetMaterial != null)
        {
            UpdateShaderBuffers();
            isShaderDirty = false;
        }
    }

    void UpdateShaderBuffers()
    {
        // 1. Pack Cubes
        int cubeCount = Mathf.Min(localCubes.Count, 32);
        for (int i = 0; i < cubeCount; i++)
        {
            if (localCubes[i] != null)
            {
                cubePositions[i] = localCubes[i].position;
                cubeSizes[i] = localCubes[i].lossyScale * 0.5f;
            }
        }

        // 2. Pack Spheres
        int sphereCount = Mathf.Min(localSpheres.Count, 32);
        for (int j = 0; j < sphereCount; j++)
        {
            if (localSpheres[j] != null)
            {
                spherePositions[j] = localSpheres[j].position;
                float radius = Mathf.Max(localSpheres[j].lossyScale.x, Mathf.Max(localSpheres[j].lossyScale.y, localSpheres[j].lossyScale.z)) * 0.5f;
                sphereRadii[j] = new Vector4(radius, 0, 0, 0);
            }
        }

        // 3. Upload variables down into this isolated Material instance
        targetMaterial.SetVectorArray("_CubePositions", cubePositions);
        targetMaterial.SetVectorArray("_CubeSizes", cubeSizes);
        targetMaterial.SetInt("_CubeCount", cubeCount);

        targetMaterial.SetVectorArray("_SpherePositions", spherePositions);
        targetMaterial.SetVectorArray("_SphereRadii", sphereRadii);
        targetMaterial.SetInt("_SphereCount", sphereCount);
    }
}