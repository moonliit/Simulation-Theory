using UnityEngine;
using System.Collections;

public class SwordViewmodel : MonoBehaviour
{
    [Header("Balanceo (Sway)")]
    public float swayAmount = 2f;
    public float maxSwayAmount = 5f;
    public float swaySmoothness = 10f;

    [Header("Animación de Tajo")]
    public Vector3 swingRotation = new Vector3(30f, -60f, -45f); // El ángulo del tajo
    public float swingSpeed = 20f; // Qué tan rápido corta
    public float returnSpeed = 8f; // Qué tan rápido regresa a la guardia

    private Quaternion initialRotation;
    private bool isAttacking = false;

    void Start()
    {
        // Guardamos la rotación original (la pose de guardia)
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        // Si no está atacando, la espada se balancea con la cámara
        if (!isAttacking)
        {
            ApplySway();
        }
    }

    private void ApplySway()
    {
        // Capturamos el movimiento del ratón
        float mouseX = -Input.GetAxis("Mouse X") * swayAmount;
        float mouseY = -Input.GetAxis("Mouse Y") * swayAmount;

        // Lo limitamos para que la espada no dé vueltas locas
        mouseX = Mathf.Clamp(mouseX, -maxSwayAmount, maxSwayAmount);
        mouseY = Mathf.Clamp(mouseY, -maxSwayAmount, maxSwayAmount);

        // Calculamos a dónde debería rotar
        Quaternion swayRotation = Quaternion.Euler(mouseY, mouseX, 0);
        Quaternion targetRotation = initialRotation * swayRotation;

        // Rotamos suavemente hacia ese objetivo
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * swaySmoothness);
    }

    // Esta función será llamada por tu PlayerCombat.cs
    public void PlayAttackAnimation()
    {
        if (!isAttacking)
        {
            StartCoroutine(SwingRoutine());
        }
    }

    private IEnumerator SwingRoutine()
    {
        isAttacking = true;

        // 1. EL TAJO (Movimiento brusco hacia el objetivo)
        Quaternion targetSwing = initialRotation * Quaternion.Euler(swingRotation);
        float t = 0;
        
        while (t < 1f)
        {
            t += Time.deltaTime * swingSpeed;
            // Usamos Slerp para rotaciones esféricas suaves
            transform.localRotation = Quaternion.Slerp(initialRotation, targetSwing, t);
            yield return null;
        }

        // 2. RECUPERACIÓN (Movimiento suave de regreso)
        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * returnSpeed;
            transform.localRotation = Quaternion.Slerp(targetSwing, initialRotation, t);
            yield return null;
        }

        transform.localRotation = initialRotation;
        isAttacking = false;
    }
}