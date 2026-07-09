using UnityEngine;
using UnityEngine.UI; 
using System.Collections;

// ====================================================================
// PlayerHealth.cs
// Gestiona la vida del jugador y el estado de Game Over.
// ====================================================================
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    private bool isDead = false;

    [Header("Efectos de Muerte (Glitch)")]
    public Image systemFailurePanel;
    public float glitchDuration = 2.0f;

    [Header("Efectos de Daño")]
    public Image damageOverlay;
    public float damageFlashDuration = 0.5f;
    private Coroutine damageFlashCoroutine;

    void Start()
    {
        currentHealth = maxHealth;

        if (UIManager.Instance != null)
            UIManager.Instance.UpdatePlayerHealth(currentHealth, maxHealth);

        if (systemFailurePanel != null)
        {
            Color c = systemFailurePanel.color;
            c.a = 0f;
            systemFailurePanel.color = c;
        }

        if (damageOverlay != null)
        {
            Color c = damageOverlay.color;
            c.a = 0f;
            damageOverlay.color = c;
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        SFXManager.Instance.PlaySound(SFXManager.Instance.playerHurt);
        currentHealth -= amount;

        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.5f);

        if (damageOverlay != null)
        {
            if (damageFlashCoroutine != null) StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        Debug.Log($"Jugador recibe {amount} de daño. Vida restante: {currentHealth}");

        if (UIManager.Instance != null)
            UIManager.Instance.UpdatePlayerHealth(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        Color c = damageOverlay.color;
        c.a = 0.6f; 
        damageOverlay.color = c;

        float elapsed = 0f;
        while (elapsed < damageFlashDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0.6f, 0f, elapsed / damageFlashDuration);
            damageOverlay.color = c;
            yield return null;
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("¡GAME OVER! Has sido derrotado por el Guardián.");
        
        GetComponent<FirstPersonController>().enabled = false;
        GetComponent<PlayerCombat>().enabled = false;
        
        if (GameSession.Instance != null && GameSession.Instance.bgmSource != null)
            GameSession.Instance.bgmSource.Stop();
        
        if (SFXManager.Instance != null)
        {
            if (SFXManager.Instance.sfxSource != null) 
                SFXManager.Instance.sfxSource.Stop();
                
            if (SFXManager.Instance.loopSource != null) 
                SFXManager.Instance.loopSource.Stop();
                
            SFXManager.Instance.PlaySound(SFXManager.Instance.playerDeath);
        }

        StartCoroutine(SystemFailureRoutine());
    }

    private IEnumerator SystemFailureRoutine()
    {
        if (systemFailurePanel != null)
        {
            float elapsed = 0f;
            Color[] glitchColors = new Color[] { Color.black, Color.red, Color.cyan, Color.magenta, Color.white };

            while (elapsed < glitchDuration)
            {
                Color randomColor = glitchColors[Random.Range(0, glitchColors.Length)];
                randomColor.a = 1f;
                
                systemFailurePanel.color = randomColor;

                float flickerTime = Random.Range(0.05f, 0.15f);
                yield return new WaitForSeconds(flickerTime);
                elapsed += flickerTime;
            }

            systemFailurePanel.color = new Color(255f, 0f, 0f, 1f);
            yield return new WaitForSeconds(0.5f);
        }

        if (GameSession.Instance != null)
        {
            GameSession.Instance.TriggerDefeat();
        }
    }
}