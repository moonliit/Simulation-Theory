using UnityEngine;

public class GuardianBossFlight : MonoBehaviour
{
    [Header("Órbita")]
    [Tooltip("Punto alrededor del cual orbita. Si se deja vacío, se genera automáticamente en el centro de la arena a la altura base.")]
    public Transform orbitCenter;
    public float orbitRadius = 6f;
    [Tooltip("Grados por segundo. Positivo = sentido antihorario visto desde arriba.")]
    public float orbitSpeed = 20f;
    public float baseHeight = 6f;

    [Header("Oscilación vertical")]
    public float bobAmplitude = 0.5f;
    public float bobSpeed = 1.5f;

    [Header("Encarar al jugador")]
    public Transform player;
    public bool facePlayer = true;
    public float turnSpeed = 90f; // grados/seg

    [Header("Rotación sobre sí mismo (Efecto Carrusel)")]
    public bool spinConstantly = true;
    public float spinSpeed = 90f; // Grados por segundo en su propio eje

    [Header("Control Externo")]
    [Tooltip("Si es true, el núcleo deja de mirar al jugador. Usado para ataques giratorios.")]
    public bool pauseCoreTracking = false;

    [HideInInspector] public float currentTargetRadius = 6f;
    private float currentRadius;

    private float angle;

    void Start()
    {
        if (orbitCenter == null)
        {
            GameObject autoCenter = new GameObject("OrbitCenter_Auto");
            autoCenter.transform.position = new Vector3(transform.position.x, baseHeight, transform.position.z);
            orbitCenter = autoCenter.transform;
        }

        Vector3 offset = transform.position - orbitCenter.position;
        angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;

        currentTargetRadius = orbitRadius;
        currentRadius = orbitRadius;
    }

    void Update()
    {
        currentRadius = Mathf.Lerp(currentRadius, currentTargetRadius, Time.deltaTime * 2.5f);
        angle += orbitSpeed * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;

        Vector3 orbitPos = orbitCenter.position + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * currentRadius;
        orbitPos.y = baseHeight + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

        transform.position = orbitPos;

        if (spinConstantly)
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);

            Transform core = transform.Find("Core");
            if (core != null && player != null && !pauseCoreTracking) 
            {
                core.rotation = Quaternion.LookRotation(player.position - core.position);
            }
        }
        else if (facePlayer && player != null)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f; 
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
        }
    }
}