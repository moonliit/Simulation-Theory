using UnityEngine;

public class SdfRigidBodyWrapper : MonoBehaviour
{
    [SerializeField] private int resolution = 32;
    [SerializeField] private Material raymarchMaterial;

    private Texture3D sdfTexture;
    private GameObject physicsProxy;
    private GameObject renderProxy;
    private MeshRenderer renderProxyRenderer;
    
    // EXPOSE THE ISOLATED MATERIAL INSTANCE
    public Material InstanceMaterial { get; private set; }

    void Start()
    {
        if (renderProxy == null) CreateProxies();
    }

    public void CreateProxies()
    {
        if (renderProxy != null) return; // Prevent double initialization

        // Texture generation steps
        float[] distanceGrid = new float[resolution * resolution * resolution];
        Vector3 center = new Vector3(resolution / 2f, resolution / 2f, resolution / 2f);
        float radius = resolution * 0.35f;

        for (int z = 0; z < resolution; z++) {
            for (int y = 0; y < resolution; y++) {
                for (int x = 0; x < resolution; x++) {
                    int index = x + resolution * (y + resolution * z);
                    distanceGrid[index] = Vector3.Distance(new Vector3(x, y, z), center) - radius;
                }
            }
        }

        sdfTexture = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false);
        sdfTexture.wrapMode = TextureWrapMode.Clamp;
        sdfTexture.filterMode = FilterMode.Trilinear;

        Color[] colorBuffer = new Color[distanceGrid.Length];
        for (int i = 0; i < distanceGrid.Length; i++) {
            colorBuffer[i] = new Color(distanceGrid[i], 0, 0, 0);
        }
        sdfTexture.SetPixels(colorBuffer);
        sdfTexture.Apply();

        // Setup physics
        physicsProxy = new GameObject(gameObject.name + "_PhysicsProxy");
        physicsProxy.transform.SetParent(transform, false);
        physicsProxy.layer = LayerMask.NameToLayer("PhysicsProxy");
        physicsProxy.AddComponent<Rigidbody>().isKinematic = true;
        physicsProxy.AddComponent<SphereCollider>().radius = 1.2f;

        // Setup rendering proxy container
        renderProxy = new GameObject(gameObject.name + "_RenderProxy");
        renderProxy.transform.SetParent(transform, false);
        renderProxy.layer = LayerMask.NameToLayer("SdfRender");

        var filter = renderProxy.AddComponent<MeshFilter>();
        renderProxyRenderer = renderProxy.AddComponent<MeshRenderer>();

        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        filter.sharedMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempCube);

        // Instantiate isolated material unique to this leaf instance
        InstanceMaterial = new Material(raymarchMaterial);
        InstanceMaterial.SetTexture("_VolumeTex", sdfTexture);
        renderProxyRenderer.material = InstanceMaterial;

        // Initialize our localized manager with our unique material instance
        SdfSceneManager localManager = GetComponent<SdfSceneManager>();
        if (localManager != null)
        {
            localManager.InitializeWithMaterial(InstanceMaterial);
        }

        var sync = renderProxy.AddComponent<ProxySynchronizer>();
        sync.Setup(physicsProxy.transform, renderProxyRenderer);
    }

    public void ConfigureVolumeBounds(Vector3 center, Vector3 size)
    {
        if (renderProxy == null) CreateProxies();

        renderProxy.transform.position = center;
        renderProxy.transform.localScale = size;
    }
}