using UnityEngine;

// ====================================================================
// BossPart.cs
// Se adjunta a cada parte y al núcleo. Maneja la vida individual.
// ====================================================================
public class BossPart : MonoBehaviour
{
    [Header("Tipo y Material")]
    public bool isCore = false;
    public Material debrisMaterial;
    

    [Header("Puntos de Vida")]
    public int maxHealth = 50;
    private int currentHealth;

    [Header("Interfaz de Usuario")]
    [Tooltip("El índice de este anillo en la UI (0, 1, 2 o 3)")]
    public int uiIndex = 0;


    private BossController boss;

    void Start()
    {
        boss = GetComponentInParent<BossController>();
        currentHealth = maxHealth;
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateBossPartHealth(uiIndex, currentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (isCore)
        {
            if (!boss.IsCoreExposed())
            {
                Debug.Log("¡El núcleo es invulnerable! Destruye las torres protectoras primero.");
                return;
            }
            
            boss.TakeDamage(amount);
            boss.TriggerHitReaction();
            return;
        }

        SFXManager.Instance.PlaySound(SFXManager.Instance.bossHurt);
        boss.TriggerHitReaction();

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.08f, 0.15f);
            CameraShake.Instance.HitStop(0.03f, 0.05f);
        }

        currentHealth -= amount;
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBossPartHealth(uiIndex, currentHealth, maxHealth);
            UIManager.Instance.ShowHitmarker();
        }

        if (currentHealth <= 0)
        {
            SFXManager.Instance.PlayBossPartDestroyed();
            Debug.Log($"¡Parte destruida: {gameObject.name}!");
            boss.ReportTowerDestroyed();
            ExplodeIntoFragments(8);

            if (CameraShake.Instance != null) 
            {
                CameraShake.Instance.Shake(0.2f, 0.3f);
                CameraShake.Instance.HitStop(0.08f, 0.02f);
            }

            Destroy(gameObject); 
        }
    }
    
    private void ExplodeIntoFragments(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.transform.position = transform.position + Random.insideUnitSphere * 2f;

            frag.transform.localScale = Vector3.one * Random.Range(0.2f, 0.6f); 

            if (debrisMaterial != null) 
                frag.GetComponent<Renderer>().material = debrisMaterial;

            Rigidbody rb = frag.AddComponent<Rigidbody>();
            rb.AddExplosionForce(1000f, transform.position, 5f);

            Destroy(frag, 3f); 
        }
    }

    public bool CanBeScarred()
    {
        if (isCore && !boss.IsCoreExposed())
        {
            return false;
        }
        return true;
    }
}