using UnityEngine;

public static class SparkBurst
{
    public static void Spawn(Vector3 position, Vector3 direction, Material material, int count = 8, float length = 0.4f, float duration = 0.15f)
    {
        if (material == null) return;
        if (direction == Vector3.zero) direction = Vector3.up;

        for (int i = 0; i < count; i++)
        {
            Vector3 randomDir = (direction + Random.insideUnitSphere * 0.8f).normalized;

            GameObject sparkObj = new GameObject("Spark");
            sparkObj.transform.position = position;

            LineRenderer lr = sparkObj.AddComponent<LineRenderer>();
            lr.material = material;
            lr.useWorldSpace = true;
            lr.startWidth = 0.04f;
            lr.endWidth = 0f;
            lr.positionCount = 2;
            lr.SetPosition(0, position);
            lr.SetPosition(1, position + randomDir * length * Random.Range(0.5f, 1f));

            sparkObj.AddComponent<SparkFader>().Init(lr, duration);
        }
    }
}

public class SparkFader : MonoBehaviour
{
    private LineRenderer lr;
    private float duration;
    private float elapsed;
    private Vector3 origin, tip;

    public void Init(LineRenderer renderer, float dur)
    {
        lr = renderer;
        duration = dur;
        elapsed = 0f;
        origin = lr.GetPosition(0);
        tip = lr.GetPosition(1);
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        lr.SetPosition(1, Vector3.Lerp(tip, origin, t));
        lr.startWidth = Mathf.Lerp(0.04f, 0f, t);

        if (t >= 1f) Destroy(gameObject);
    }
}