using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance;
    
    [Header("Paneles de UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Música del Nivel")]
    public AudioSource bgmSource;

    public static bool IsTransitioningScene { get; private set; } = false;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f; 
        IsTransitioningScene = false;
        
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    public void TriggerVictory()
    {
        if (winPanel != null) winPanel.SetActive(true);
        EndGame();
    }

    public void TriggerDefeat()
    {
        if (losePanel != null) losePanel.SetActive(true);
        EndGame();
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
}