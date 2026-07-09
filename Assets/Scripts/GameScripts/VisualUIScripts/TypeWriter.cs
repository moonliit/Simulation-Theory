using UnityEngine;
using TMPro; // Cambia a UnityEngine.UI si usas texto normal
using System.Collections;

public class TypewriterEffect : MonoBehaviour
{
    [Header("Configuración del Título")]
    public float delayBtwChars = 0.1f;
    
    [Header("Conexión con Botones")]
    public GameObject buttonGroup;

    private TextMeshProUGUI textComponent;
    private string fullText;

    void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        fullText = textComponent.text;
        textComponent.text = "";
        
        if (buttonGroup != null)
        {
            buttonGroup.SetActive(false);
        }
    }

    void Start()
    {
        StartCoroutine(ShowText());
    }

    private IEnumerator ShowText()
    {
        for (int i = 0; i <= fullText.Length; i++)
        {
            textComponent.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(delayBtwChars);
        }

        if (buttonGroup != null)
        {
            StartCoroutine(GlitchBootRoutine());
        }
    }

    private IEnumerator GlitchBootRoutine()
    {
        for (int i = 0; i < 3; i++)
        {
            buttonGroup.SetActive(true);
            yield return new WaitForSeconds(0.05f);
            buttonGroup.SetActive(false);
            yield return new WaitForSeconds(0.05f);
        }
        
        buttonGroup.SetActive(true);
    }
}