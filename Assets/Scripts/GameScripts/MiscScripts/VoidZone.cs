using UnityEngine;

// ====================================================================
// VoidZone.cs
// Teletransporta al jugador y le quita 35HP. Destruye todo lo demás.
// ====================================================================
public class VoidZone : MonoBehaviour
{
    [Header("Castigo del Jugador")]
    [Tooltip("El objeto vacío en la arena donde aparecerá el jugador.")]
    public Transform respawnPoint;
    public int fallDamage = 35; 

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Jugador cayó al vacío. Reapareciendo...");
            
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            if (respawnPoint != null)
                other.transform.position = respawnPoint.position;
            
            else
                other.transform.position = new Vector3(0f, 5f, 0f);
            

            if (cc != null) cc.enabled = true;

            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health != null)
                health.TakeDamage(fallDamage);
        }
        else if (other.CompareTag("Boss"))
        {
            BossController boss = other.GetComponentInParent<BossController>();
            if (boss != null) boss.TakeDamage(9999);
        }
        else
        {
            Destroy(other.gameObject);
        }
    }
}