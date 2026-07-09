using UnityEngine;
using System.Collections;

public class SwordViewmodel : MonoBehaviour
{
    [Header("Balanceo (Sway)")]
    public float swayAmount = 2f;
    public float maxSwayAmount = 5f;
    public float swaySmoothness = 10f;

    [System.Serializable]
    public class SwingPreset
    {
        public string label;
        public float cutAngle;

        [Header("Dirección A")]
        public Vector3 windupA;
        public Vector3 swingA;

        [Header("Dirección B (opuesta, para alternar)")]
        public Vector3 windupB;
        public Vector3 swingB;
    }

    [Header("Animaciones de Tajo por Dirección")]
    public SwingPreset[] swingPresets = new SwingPreset[]
    {
        new SwingPreset { label = "Horizontal", cutAngle = 0f,
            windupA = new Vector3(15, 0,  65),  swingA = new Vector3(5, 0, -100),
            windupB = new Vector3(15, 0, -65),  swingB = new Vector3(5, 0,  100) },

        new SwingPreset { label = "Vertical", cutAngle = 90f,
            windupA = new Vector3(-45, 0,  10), swingA = new Vector3(85, 0, -10),
            windupB = new Vector3( 70, 0, -10), swingB = new Vector3(-60, 0, 10) },

        new SwingPreset { label = "Diagonal /", cutAngle = 45f,
            windupA = new Vector3(-30, 0, -40), swingA = new Vector3(60, 0,  70),
            windupB = new Vector3( 35, 0,  45), swingB = new Vector3(-45, 0, -65) },

        new SwingPreset { label = "Diagonal \\", cutAngle = -45f,
            windupA = new Vector3(-30, 0,  40), swingA = new Vector3(60, 0, -70),
            windupB = new Vector3( 35, 0, -45), swingB = new Vector3(-45, 0,  65) },
    };

    public Vector3 fallbackWindup = new Vector3(10, -30, 15);
    public Vector3 fallbackSwing = new Vector3(30, -60, -45);

    [Header("Velocidades")]
    public float windupSpeed = 25f;
    public float swingSpeed = 14f;
    public float returnSpeed = 8f;
    private Quaternion initialRotation;
    private bool isAttacking = false;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        if (!isAttacking) ApplySway();
    }

    private void ApplySway()
    {
        float mouseX = -Input.GetAxis("Mouse X") * swayAmount;
        float mouseY = -Input.GetAxis("Mouse Y") * swayAmount;
        mouseX = Mathf.Clamp(mouseX, -maxSwayAmount, maxSwayAmount);
        mouseY = Mathf.Clamp(mouseY, -maxSwayAmount, maxSwayAmount);

        Quaternion swayRotation = Quaternion.Euler(mouseY, mouseX, 0);
        Quaternion targetRotation = initialRotation * swayRotation;
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * swaySmoothness);
    }

    public void PlayAttackAnimation(float cutAngle, bool flip)
    {
        if (isAttacking) return;

        Vector3 windup = fallbackWindup;
        Vector3 swing = fallbackSwing;

        foreach (var preset in swingPresets)
        {
            if (Mathf.Approximately(preset.cutAngle, cutAngle))
            {
                windup = flip ? preset.windupB : preset.windupA;
                swing = flip ? preset.swingB : preset.swingA;
                break;
            }
        }

        StartCoroutine(SwingRoutine(windup, swing));
    }

    private IEnumerator SwingRoutine(Vector3 windupEuler, Vector3 swingEuler)
    {
        isAttacking = true;

        Quaternion windupPose = initialRotation * Quaternion.Euler(windupEuler);
        Quaternion swingPose = initialRotation * Quaternion.Euler(swingEuler);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * windupSpeed;
            transform.localRotation = Quaternion.Slerp(initialRotation, windupPose, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * swingSpeed;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2f);
            transform.localRotation = Quaternion.Slerp(windupPose, swingPose, eased);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * returnSpeed;
            transform.localRotation = Quaternion.Slerp(swingPose, initialRotation, t);
            yield return null;
        }

        transform.localRotation = initialRotation;
        isAttacking = false;
    }
}