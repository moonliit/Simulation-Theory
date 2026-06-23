using UnityEngine;

public class ProxySynchronizer : MonoBehaviour
{
    private Transform physicsTarget;
    private Renderer renderTarget;
    private bool isInitialized = false;

    public void Setup(Transform physicsTransform, Renderer rendererComponent)
    {
        physicsTarget = physicsTransform;
        renderTarget = rendererComponent;
        isInitialized = true;
    }

    void LateUpdate()
    {
        // Safety guard: Prevents the script from executing until Setup() has fully completed
        if (!isInitialized || physicsTarget == null || transform == null)
        {
            return;
        }

        // Keep the render proxy locked cleanly to the physical simulated layer
        transform.position = physicsTarget.position;
        transform.rotation = physicsTarget.rotation;

        // If you are setting custom shader matrices (like world-to-local), update them safely here:
        if (renderTarget != null && renderTarget.sharedMaterial != null)
        {
            // renderTarget.sharedMaterial.SetMatrix("_PhysicsToLocal", physicsTarget.worldToLocalMatrix);
        }
    }
}