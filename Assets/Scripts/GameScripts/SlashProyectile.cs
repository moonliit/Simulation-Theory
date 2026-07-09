using UnityEngine;

// ====================================================================
// SlashProyectile.cs
// Maneja el proyectil de cortes, scars dinámicos y destrucción de jaulas.
// ====================================================================
public class SlashProjectile : MonoBehaviour
{
    public float speed = 25f;
    public float lifetime = 1.5f;
    private float timer;

    [Header("Efectos Visuales")]
    [Tooltip("El prefab del cubo delgado que restará geometría permanentemente.")]
    public GameObject scarPrefab;

    void OnEnable()
    {
        timer = 0f;
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
        BossController boss = FindObjectOfType<BossController>();
        
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
            if (boss != null && boss.IsCoreExposed())
            {
                boss.TakeDamage(10);
                Vector3 surfaceHitPoint = other.ClosestPoint(transform.position);
                SpawnScar(other.transform, surfaceHitPoint);
            }
            else
            {
                Debug.Log("El núcleo está protegido. Golpe bloqueado.");
            }
            return;
        }

        if (other.name.Contains("Cage"))
        {
            Debug.Log("¡Muro de la jaula destruido por un tajo!");
            
            if (SFXManager.Instance != null) 
                SFXManager.Instance.PlayBossPartDestroyed();
            
            Destroy(other.gameObject);
            gameObject.SetActive(false); 
            return;
        }
    }

    void SpawnScar(Transform parentTransform, Vector3 exactHitPoint)
    {
        if (scarPrefab != null)
        {
            GameObject scar = Instantiate(scarPrefab, exactHitPoint, transform.rotation);
            scar.transform.SetParent(parentTransform, true);
        }
    }
}