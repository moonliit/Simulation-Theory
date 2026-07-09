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
    private RectTransform failurePanelRect;
    private Image[] glitchBars;

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
            failurePanelRect = systemFailurePanel.rectTransform;
            BuildGlitchBars();
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
            Vector2 originalPos = failurePanelRect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < glitchDuration)
            {
                bool dropout = Random.value < 0.15f;

                if (dropout)
                {
                    systemFailurePanel.color = Color.black;
                    foreach (var bar in glitchBars) bar.gameObject.SetActive(false);
                    failurePanelRect.anchoredPosition = originalPos;
                }
                else
                {
                    systemFailurePanel.color = Random.value < 0.4f
                        ? new Color(1f, 0f, 0.15f, 1f)
                        : new Color(Random.value, Random.value, Random.value, 1f);

                    RandomizeGlitchBars();
                    failurePanelRect.anchoredPosition = originalPos + new Vector2(Random.Range(-15f, 15f), Random.Range(-8f, 8f));
                }

                float flickerTime = Random.Range(0.025f, 0.09f);
                yield return new WaitForSecondsRealtime(flickerTime);
                elapsed += flickerTime;
            }

            foreach (var bar in glitchBars) bar.gameObject.SetActive(false);
            failurePanelRect.anchoredPosition = originalPos;
            systemFailurePanel.color = new Color(1f, 0f, 0f, 1f);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        if (GameSession.Instance != null)
        {
            GameSession.Instance.TriggerDefeat();
        }
    }

    private void BuildGlitchBars()
    {
        glitchBars = new Image[6];
        for (int i = 0; i < glitchBars.Length; i++)
        {
            GameObject barObj = new GameObject("GlitchBar_" + i);
            barObj.transform.SetParent(systemFailurePanel.transform, false);

            Image img = barObj.AddComponent<Image>();
            img.color = Color.white;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            barObj.SetActive(false);
            glitchBars[i] = img;
        }
    }

    private void RandomizeGlitchBars()
    {
        foreach (var bar in glitchBars)
        {
            bool show = Random.value < 0.5f;
            bar.gameObject.SetActive(show);
            if (!show) continue;

            RectTransform rt = bar.rectTransform;
            rt.sizeDelta = new Vector2(0f, Random.Range(4f, 30f));
            rt.anchoredPosition = new Vector2(Random.Range(-40f, 40f), Random.Range(-Screen.height * 0.5f, Screen.height * 0.5f));

            bar.color = Random.value < 0.3f
                ? Color.black
                : new Color(Random.value, Random.value, Random.value, 1f);
        }
    }
}