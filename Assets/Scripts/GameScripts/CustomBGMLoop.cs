using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CustomBGMLoop : MonoBehaviour
{
    [Header("¿En qué segundo exacto empieza el loop?")]
    public float loopStartPoint = 3.35f;
    public float loopEndPoint = 178.7f;

    private AudioSource bgm;

    void Start()
    {
        bgm = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (bgm == null || !bgm.isPlaying) return;

        if (bgm.time >= loopEndPoint)
        {
            // ...la teletransportamos instantáneamente al inicio del loop
            bgm.time = loopStartPoint;
        }
    }
}