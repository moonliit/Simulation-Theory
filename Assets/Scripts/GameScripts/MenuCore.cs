using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class MenuCore : MonoBehaviour
{
    [Header("Paneles de UI")]
    public GameObject mainMenuUI;
    public GameObject controlsUI;

    [Header("Transición")]
    public Image fadePanel;
    public float fadeSpeed = 1.5f;

    [Header("Configuración")]
    public string gameSceneName = "BossTestScene"; 

    [Header("Audio de Botones")]
    public AudioSource uiAudioSource;
    public AudioClip clickSound; 

    void Start()
    {
        if (fadePanel != null)
        {
            Color c = fadePanel.color;
            c.a = 0f;
            fadePanel.color = c;
            fadePanel.raycastTarget = false;
        }

        ShowMainMenu();
    }

    public void Btn_Play()
    {
        PlayClickSound();

        if (fadePanel != null)
            StartCoroutine(FadeAndLoadScene());
        else
            SceneManager.LoadScene(gameSceneName);
    }

    public void Btn_OpenControls()
    {
        PlayClickSound();
        mainMenuUI.SetActive(false);
        controlsUI.SetActive(true);
    }

    public void Btn_CloseControls()
    {
        PlayClickSound();
        controlsUI.SetActive(false);
        mainMenuUI.SetActive(true);
    }

    public void Btn_Exit()
    {
        PlayClickSound();
        Debug.Log("Sistema cerrado...");
        Application.Quit();
    }

    private void ShowMainMenu()
    {
        mainMenuUI.SetActive(true);
        controlsUI.SetActive(false);
    }

    private void PlayClickSound()
    {
        if (uiAudioSource != null && clickSound != null)
        {
            uiAudioSource.PlayOneShot(clickSound);
        }
    }

    private IEnumerator FadeAndLoadScene()
    {
        fadePanel.raycastTarget = true;
        
        Color c = fadePanel.color;
        
        while (c.a < 1f)
        {
            c.a += Time.deltaTime * fadeSpeed;
            fadePanel.color = c;
            yield return null;
        }

        SceneManager.LoadScene(gameSceneName);
    }
}