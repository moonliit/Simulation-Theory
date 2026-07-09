using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Retícula y Hitmarker")]
    public GameObject hitmarkerObj;
    public float hitmarkerDuration = 0.15f;

    [Header("Barra de Vida del Player")]
    public Slider playerHealthBar;

    [Header("Barra de Vida del Núcleo (Boss)")]
    public Slider bossCoreHealthBar;
    public Image bossCoreFill;
    public Color coreInvulnerableColor = Color.gray;
    public Color coreVulnerableColor = Color.red;

    [Header("Barra de Vida de Partes (Boss)")]
    public Slider[] bossPartBars;

    [Header("Cooldowns")]
    public Image dashCooldownImage;
    public Slider wallCooldownBar;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (hitmarkerObj != null) hitmarkerObj.SetActive(false);
        if (dashCooldownImage != null) dashCooldownImage.gameObject.SetActive(false);
        if (wallCooldownBar != null) wallCooldownBar.gameObject.SetActive(false);
    }

    public void UpdatePlayerHealth(int current, int max)
    {
        playerHealthBar.maxValue = max;
        playerHealthBar.value = current;
    }

    public void UpdateBossCoreHealth(int current, int max)
    {
        bossCoreHealthBar.maxValue = max;
        bossCoreHealthBar.value = current;
    }

    public void UpdateBossPartHealth(int index, int current, int max)
    {
        if (index >= 0 && index < bossPartBars.Length && bossPartBars[index] != null)
        {
            bossPartBars[index].maxValue = max;
            bossPartBars[index].value = current;

            if (current <= 0) 
                bossPartBars[index].gameObject.SetActive(false);
        }
    }

    public void SetCoreInvulnerable()
    {
        if (bossCoreFill != null) bossCoreFill.color = coreInvulnerableColor;
    }

    public void SetCoreVulnerable()
    {
        if (bossCoreFill != null) bossCoreFill.color = coreVulnerableColor;
    }


    public void ShowHitmarker()
    {
        StopCoroutine(HitmarkerRoutine());
        StartCoroutine(HitmarkerRoutine());
    }

    private IEnumerator HitmarkerRoutine()
    {
        hitmarkerObj.SetActive(true);
        yield return new WaitForSeconds(hitmarkerDuration);
        hitmarkerObj.SetActive(false);
    }

    public void UpdateDashCooldown(float currentTimer, float maxCooldown)
    {
        if (currentTimer > 0)
        {
            dashCooldownImage.gameObject.SetActive(true);
            dashCooldownImage.fillAmount = 1f - (currentTimer / maxCooldown);
        }
        else
        {
            dashCooldownImage.gameObject.SetActive(false);
        }
    }

    public void UpdateWallCooldown(float currentTimer, float maxCooldown)
    {
        if (currentTimer > 0)
        {
            wallCooldownBar.gameObject.SetActive(true);
            wallCooldownBar.maxValue = maxCooldown;
            wallCooldownBar.value = maxCooldown - currentTimer; 
        }
        else
        {
            wallCooldownBar.gameObject.SetActive(false);
        }
    }
}