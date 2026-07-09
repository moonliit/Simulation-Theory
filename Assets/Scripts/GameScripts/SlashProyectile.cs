using UnityEngine;

// ====================================================================
// SlashProyectile.cs
// Maneja el proyectil de cortes
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
                SpawnScar(part.transform);
            
            part.TakeDamage(10);
            return;
        }

        if (other.name.Contains("Core")) 
        {
            if (boss != null && boss.IsCoreExposed())
            {
                boss.TakeDamage(10);
                SpawnScar(other.transform);
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

    void SpawnScar(Transform parentTransform)
    {
        if (scarPrefab != null)
        {
            float penetrationDepth = 0.4f; 
            Vector3 deepPosition = transform.position + (transform.forward * penetrationDepth);
            GameObject scar = Instantiate(scarPrefab, deepPosition, transform.rotation);
            scar.transform.SetParent(parentTransform, true);
        }
    }
}