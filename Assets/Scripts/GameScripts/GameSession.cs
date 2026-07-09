using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance;
    public static bool IsTransitioningScene { get; private set; } = false;

    [Header("Paneles de UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Música del Nivel")]
    public AudioSource bgmSource;

    [Header("Cronómetro de Combate")]
    public TMP_Text liveCombatTimerText;
    public TMP_Text victoryTimeText;
    public TMP_Text victoryBestTimeText;
    public GameObject newRecordBadge; 

    private const string BEST_TIME_KEY = "BestCombatTime";

    private float combatStartTime;
    private bool combatEnded = false;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f; 
        IsTransitioningScene = false;
        
        combatStartTime = Time.time;
        combatEnded = false;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (newRecordBadge != null) newRecordBadge.SetActive(false);
    }

    void Update()
    {
        if (combatEnded) return;

        float elapsed = Time.time - combatStartTime;
        if (liveCombatTimerText != null)
            liveCombatTimerText.text = FormatTime(elapsed);
    }

    public void TriggerVictory()
    {
        combatEnded = true;
        float finalCombatTime = Time.time - combatStartTime;

        bool isNewRecord = false;
        float bestTime = PlayerPrefs.HasKey(BEST_TIME_KEY) ? PlayerPrefs.GetFloat(BEST_TIME_KEY) : -1f;

        if (bestTime < 0f || finalCombatTime < bestTime)
        {
            isNewRecord = true;
            bestTime = finalCombatTime;
            PlayerPrefs.SetFloat(BEST_TIME_KEY, bestTime);
            PlayerPrefs.Save();
        }

        if (victoryTimeText != null)
            victoryTimeText.text = $"Tiempo de combate: {FormatTime(finalCombatTime)}";

        if (victoryBestTimeText != null)
            victoryBestTimeText.text = $"Mejor tiempo: {FormatTime(bestTime)}";

        if (newRecordBadge != null)
            newRecordBadge.SetActive(isNewRecord);

        EndGame();
        if (winPanel != null) StartCoroutine(RevealPanelPrint(winPanel));
    }

    public void TriggerDefeat()
    {
        combatEnded = true;
        EndGame();
        if (losePanel != null) StartCoroutine(RevealPanelPrint(losePanel));
    }

    private IEnumerator RevealPanelPrint(GameObject panelObj, float duration = 0.35f)
    {
        RectTransform panel = panelObj.GetComponent<RectTransform>();
        panelObj.SetActive(true);

        Vector3 targetScale = panel.localScale;
        panel.localScale = new Vector3(targetScale.x, 0f, targetScale.z);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            panel.localScale = new Vector3(targetScale.x, Mathf.Lerp(0f, targetScale.y, t), targetScale.z);
            yield return null;
        }

        panel.localScale = targetScale;
    }

    private void EndGame()
    {
        Time.timeScale = 0f;
        FirstPersonController fps = FindObjectOfType<FirstPersonController>();
        if (fps != null) fps.enabled = false;

        PlayerCombat combat = FindObjectOfType<PlayerCombat>();
        if (combat != null) combat.enabled = false;

        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        IsTransitioningScene = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMainMenu()
    {
        IsTransitioningScene = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public static string FormatTime(float totalSeconds)
    {
        if (totalSeconds < 0f) totalSeconds = 0f;
        int minutes = Mathf.FloorToInt(totalSeconds / 60f);
        float seconds = totalSeconds - minutes * 60f;
        return $"{minutes:00}:{seconds:00.00}";
    }
}