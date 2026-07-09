using UnityEngine;

public class CuttableSdfObject : MonoBehaviour
{
    [Header("Salud")]
    public int maxHealth = 20;
    public int damagePerSlash = 10;
    private int currentHealth;

    [Header("Efectos")]
    public GameObject scarPrefab;

    void Awake() => currentHealth = maxHealth;

    public void ResetHealth() => currentHealth = maxHealth;

    public void RegisterSlash(Vector3 hitPoint, Quaternion hitRotation)
    {
        currentHealth -= damagePerSlash;

        if (scarPrefab != null)
        {
            GameObject scar = Instantiate(scarPrefab, hitPoint, hitRotation);
            scar.transform.SetParent(transform, true);
        }

        if (SFXManager.Instance != null)
            SFXManager.Instance.PlaySound(SFXManager.Instance.bossHurt);

        if (currentHealth <= 0)
            Destroy(gameObject);
    }
}