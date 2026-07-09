using UnityEngine;

// =======================================================================
// SlashProyectile.cs
// Maneja el proyectil de cortes, scars dinámicos y destrucción de jaulas.
// =======================================================================
public class SlashProjectile : MonoBehaviour
{
    public float speed = 25f;
    public float lifetime = 1.5f;
    private float timer;
    public float coreProtectionRadius = 0.5f;

    [Header("Efectos Visuales")]
    [Tooltip("El prefab del cubo delgado que restará geometría permanentemente.")]
    public GameObject scarPrefab;

    private BossController cachedBoss;

    void OnEnable()
    {
        timer = 0f;
        if (cachedBoss == null) cachedBoss = FindObjectOfType<BossController>();
    }

    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
        
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {   
        BossPart part = other.GetComponent<BossPart>();
        if (part != null)
        {
            if (part.CanBeScarred())
            {
                Vector3 surfaceHitPoint = other.ClosestPoint(transform.position);
                SpawnScar(part.transform, surfaceHitPoint);
            }
            
            part.TakeDamage(10);
            return;
        }

        if (other.name.Contains("Core")) 
        {
            if (cachedBoss != null && cachedBoss.IsCoreExposed())
            {
                cachedBoss.TakeDamage(10);
                Vector3 surfaceHitPoint = other.ClosestPoint(transform.position);
                SpawnScar(other.transform, surfaceHitPoint);
            }
            return;
        }

        CuttableSdfObject cuttable = other.GetComponent<CuttableSdfObject>();
        if (cuttable != null)
        {
            Vector3 surfaceHitPoint = other.ClosestPoint(transform.position);
            cuttable.RegisterSlash(surfaceHitPoint, transform.rotation);
            return;
        }
    }

    void SpawnScar(Transform parentTransform, Vector3 exactHitPoint)
    {
        if (cachedBoss != null && !cachedBoss.IsCoreExposed() && cachedBoss.bossCore != null)
        {
            float distToCore = Vector3.Distance(exactHitPoint, cachedBoss.bossCore.position);
            if (distToCore < coreProtectionRadius) return;
        }

        if (scarPrefab != null)
        {
            GameObject scar = Instantiate(scarPrefab, exactHitPoint, transform.rotation);
            scar.transform.SetParent(parentTransform, true);
        }
    }
}