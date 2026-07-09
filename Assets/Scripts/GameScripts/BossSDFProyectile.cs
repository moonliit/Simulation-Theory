using UnityEngine;

// ====================================================================
// BossSDFProyectile.cs
// Gestiona los proyectiles 3D de los ataques del Boss.
// ====================================================================
public class BossSdfProjectile : MonoBehaviour
{
    public float speed = 15f;

    [Header("Efectos")]
    public Material neonMaterial;

    private Transform player;
    private bool isQuitting = false;

    void Start()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (var col in GetComponentsInChildren<Collider>())
            col.isTrigger = true;

        player = Camera.main.transform;
        if (speed > 0)
            transform.LookAt(player.position);
        Destroy(gameObject, 6f);
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("El proyectil SDF golpeó al jugador");
            other.GetComponent<PlayerHealth>().TakeDamage(15);
            Destroy(gameObject);
        }
        else if (other.name == "SDF_EarthWall")
        {
            Debug.Log("Proyectil estrellado contra el muro");
            Destroy(gameObject);
        }
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    void OnDestroy()
    {
        if (isQuitting || GameSession.IsTransitioningScene) return;

        int count = Random.Range(5, 8);
        for (int i = 0; i < count; i++)
        {
            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.transform.position = transform.position + Random.insideUnitSphere * 1f;
            frag.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f); 
            
            if (neonMaterial != null) 
                frag.GetComponent<Renderer>().material = neonMaterial;

            Rigidbody rb = frag.AddComponent<Rigidbody>();
            rb.AddExplosionForce(600f, transform.position, 3f);
            
            Destroy(frag, 1.5f); 
        }
    }
}