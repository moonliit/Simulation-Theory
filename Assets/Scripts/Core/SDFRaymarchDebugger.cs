using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class SDFRaymarchDebugger : MonoBehaviour
{
    [Header("Raymarch Parameters")]
    [Range(0.1f, 10f)] public float sphereRadius = 2.0f;
    [Range(5, 100)] public int maxSteps = 40;
    
    [Header("Precision Thresholds")]
    public float surfaceThreshold = 0.01f; 
    public float maxDistance = 100f;       

    [Header("Debug View Controls")]
    public bool showDebugSteps = false; // Turn this off for a completely clean look!
    [Range(2, 40)] public int gridResolution = 20; // Increased resolution since view is clean now

    void OnDrawGizmos()
    {
        Camera sceneCam = null;
        if (SceneView.lastActiveSceneView != null)
        {
            sceneCam = SceneView.lastActiveSceneView.camera;
        }

        if (sceneCam == null) return;

        Vector3 rayOriginWS = sceneCam.transform.position;
        Gizmos.matrix = Matrix4x4.identity;

        // Trace our grid
        for (int y = 0; y < gridResolution; y++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                float normalizedX = (float)x / (gridResolution - 1);
                float normalizedY = (float)y / (gridResolution - 1);

                Ray viewportRay = sceneCam.ViewportPointToRay(new Vector3(normalizedX, normalizedY, 0));
                Vector3 rayDirWS = viewportRay.direction;

                TraceAdaptiveRay(rayOriginWS, rayDirWS);
            }
        }
    }

    void TraceAdaptiveRay(Vector3 originWS, Vector3 dirWS)
    {
        Vector3 currentPosWS = originWS;
        float t = 0f;
        bool hit = false;

        for (int i = 0; i < maxSteps; i++)
        {
            currentPosWS = originWS + dirWS * t;

            float distanceToSphere = Vector3.Distance(currentPosWS, transform.position) - sphereRadius;

            // Only draw step iterations if the toggle is explicitly enabled
            if (showDebugSteps)
            {
                Gizmos.color = new Color(0.9f, 0.8f, 0.2f, 0.1f);
                Gizmos.DrawWireSphere(currentPosWS, Mathf.Max(distanceToSphere, 0.02f));
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(currentPosWS, 0.015f);
            }

            if (distanceToSphere <= surfaceThreshold)
            {
                hit = true;
                break;
            }

            if (t > maxDistance) break;

            t += distanceToSphere;
        }

        // Draw final outcome visuals
        if (hit)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(currentPosWS, 0.04f); // Crisp point marking the sphere's surface
            
            if (showDebugSteps)
            {
                Gizmos.DrawLine(originWS, currentPosWS);
            }
        }
        else if (showDebugSteps)
        {
            Gizmos.color = Color.red * 0.1f;
            Gizmos.DrawLine(originWS, currentPosWS);
        }
    }
}