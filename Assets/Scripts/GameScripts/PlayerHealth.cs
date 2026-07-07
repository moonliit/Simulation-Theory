using UnityEngine;

// ====================================================================
// PlayerHealth.cs
// Gestiona la vida del jugador y el estado de Game Over.
// ====================================================================
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;

        if (UIManager.Instance != null)
            UIManager.Instance.UpdatePlayerHealth(currentHealth, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        Debug.Log($"Jugador recibe {amount} de daño. Vida restante: {currentHealth}");

        if (UIManager.Instance != null)
            UIManager.Instance.UpdatePlayerHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("¡GAME OVER! Has sido derrotado por el Guardián.");
        
        GetComponent<FirstPersonController>().enabled = false;
        GetComponent<PlayerCombat>().enabled = false;
        
        // TODO: Mostrar pantalla roja o botón de reiniciar escena
    }
}