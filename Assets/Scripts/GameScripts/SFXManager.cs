using UnityEngine;
using System.Collections;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [Header("Reproductores")]
    public AudioSource sfxSource;
    public AudioSource loopSource;

    [Header("Sonidos del Jugador")]
    public AudioClip playerSlash;
    public AudioClip playerHurt;
    public AudioClip playerDeath;
    public AudioClip wallRaise;

    [Header("Sonidos del Boss (Ataques)")]
    public AudioClip bossGatling;
    public AudioClip bossRailgunCharge;
    public AudioClip bossRailgunFire;
    public AudioClip missileFlight;
    public AudioClip energySweep;

    [Header("Sonidos del Boss (Estado)")]
    public AudioClip bossHurt;
    public AudioClip coreExplosion;

    [Header("Sistema")]
    public AudioClip victorySound;

    [Header("Configuración Last Stand")]
    public float sweepLoopStart = 4.0f;
    public float sweepLoopEnd = 8.0f;

    private bool isLastStandActive = false;
    private Coroutine vibrationCoroutine;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (isLastStandActive && loopSource != null && loopSource.isPlaying)
        {
            if (loopSource.time >= sweepLoopEnd)
            {
                loopSource.time = sweepLoopStart;
            }
        }
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayBossPartDestroyed()
    {
        if (bossHurt != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(bossHurt, 2.0f); 
        }
    }

    public void StartLastStand()
    {
        if (loopSource != null && energySweep != null)
        {
            loopSource.clip = energySweep;
            loopSource.Play();
            isLastStandActive = true;
        }
    }

    public void StartCriticalVibration()
    {
        isLastStandActive = false;
        if (loopSource != null) loopSource.Stop();

        if (GameSession.Instance != null && GameSession.Instance.bgmSource != null)
        {
            GameSession.Instance.bgmSource.Stop();
        }

        vibrationCoroutine = StartCoroutine(VibrationRoutine());
    }

    private IEnumerator VibrationRoutine()
    {
        while (true)
        {
            if (bossHurt != null)
            {
                sfxSource.pitch = Random.Range(1.2f, 1.6f); 
                sfxSource.PlayOneShot(bossHurt);
            }
            yield return new WaitForSeconds(0.12f);
        }
    }
    public void ExecuteFinalExplosion()
    {
        if (vibrationCoroutine != null) StopCoroutine(vibrationCoroutine);
        
        sfxSource.pitch = 1f;
        
        if (coreExplosion != null) sfxSource.PlayOneShot(coreExplosion);
        if (victorySound != null) sfxSource.PlayOneShot(victorySound);
    }
}