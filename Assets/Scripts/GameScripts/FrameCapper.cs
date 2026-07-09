using UnityEngine;

public class FramerateCap : MonoBehaviour
{
    public int targetFPS = 60;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFPS;
    }
}
