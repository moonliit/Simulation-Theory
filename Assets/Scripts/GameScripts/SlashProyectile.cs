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
        BossPart part = other.GetComponent<BossPart>();
        if (part != null)
        {
            BossController boss = part.GetComponentInParent<BossController>();
            bool isProtectedCore = part.isCore && boss != null && !boss.IsCoreExposed();

            if (!isProtectedCore)
            {
                part.TakeDamage(10); 
                if (scarPrefab != null)
                {
                    float penetrationDepth = 0.8f;
                    Vector3 deepPosition = transform.position + (transform.forward * penetrationDepth);
                    GameObject scar = Instantiate(scarPrefab, deepPosition, transform.rotation);
                    scar.transform.SetParent(part.transform, true);
                }
            }
            else
            {
                Debug.Log("Ataque bloqueado: Destruye las torres primero.");
            }

            gameObject.SetActive(false); 
            return;
        }
        
        BossSdfProjectile enemyProj = other.GetComponent<BossSdfProjectile>();
        if (enemyProj != null)
        {
            Destroy(enemyProj.gameObject);
            gameObject.SetActive(false);
            return;
        }
    }
}