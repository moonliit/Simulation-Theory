using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    private Vector3 originalPos;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        originalPos = transform.localPosition;
    }

    public void Shake(float duration, float magnitude)
    {
        StopCoroutine(nameof(ShakeRoutine));
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
    }

    private float hitStopEndTime = -1f;
    private Coroutine hitStopCoroutine;

    public void HitStop(float duration = 0.04f, float freezeScale = 0.05f)
    {
        float requestedEnd = Time.unscaledTime + duration;
        if (requestedEnd > hitStopEndTime)
        {
            hitStopEndTime = requestedEnd;
            if (hitStopCoroutine == null)
                hitStopCoroutine = StartCoroutine(HitStopRoutine(freezeScale));
        }
    }

    private IEnumerator HitStopRoutine(float freezeScale)
    {
        Time.timeScale = freezeScale;

        while (Time.unscaledTime < hitStopEndTime)
            yield return null;

        Time.timeScale = 1f;
        hitStopCoroutine = null;
        hitStopEndTime = -1f;
    }
}