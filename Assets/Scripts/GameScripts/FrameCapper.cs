using UnityEngine;

public class FramerateCap : MonoBehaviour
{
    public int targetFPS = 60;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;

        // 2. Set your desired framerate limit
        Application.targetFrameRate = targetFPS;
    }
}
