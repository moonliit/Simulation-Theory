using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class SceneFadeIn : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 1f;

    void Start()
    {
        if (fadeImage == null) fadeImage = GetComponent<Image>();
        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        Color c = fadeImage.color;
        c.a = 1f;
        fadeImage.color = c;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = 0f;
        fadeImage.color = c;
        fadeImage.raycastTarget = false;
        gameObject.SetActive(false);
    }
}