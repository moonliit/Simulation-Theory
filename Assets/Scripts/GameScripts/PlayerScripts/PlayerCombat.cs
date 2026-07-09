using UnityEngine;
using System.Collections;

// ====================================================================
// PlayerCombat.cs (Rehecho para FPS y proyectiles mágicos)
// - Clic Izquierdo: Dispara un tajo SDF con rotación aleatoria.
// - Clic Derecho: Levanta un muro SDF (Earthbending) con cooldown.
// ====================================================================
public class PlayerCombat : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La cámara del jugador para saber hacia dónde mirar y disparar.")]
    public Transform playerCamera;

    [Header("Tajo de Energía (Ataque)")]
    public float slashCooldown = 0.35f;
    public float slashThickness = 0.2f;
    public float slashLength = 3.5f;
    public int slashPoolSize = 10;
    
    public Material neonMaterial;
    public GameObject scarPrefab;

    [Header("Muro (Earthbending)")]
    public float wallCooldown = 6f;
    public float wallDuration = 3f;
    public Vector3 wallSize = new Vector3(4f, 3f, 1f);
    public float wallSpawnDistance = 3f;
    
    [Header("Arma Visual")]
    public SwordViewmodel swordAnim;

    // --- Variables internas ---
    private GameObject[] cutPool;
    private int nextCutIndex = 0;
    private float nextSlashTime = 0f;

    private GameObject activeWall;
    private CuttableSdfObject activeWallCuttable;
    private float nextWallTime = 0f;

    private readonly float[] cutAngles = { 0f, 90f, 45f, -45f };
    private bool[] angleFlipState = new bool[4]; 

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main.transform;

        BuildCutPool();
        BuildWall();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= nextSlashTime)
        {
            nextSlashTime = Time.time + slashCooldown;
            int angleIndex = Random.Range(0, cutAngles.Length);
            float selectedAngle = cutAngles[angleIndex];
            bool flip = angleFlipState[angleIndex];
            angleFlipState[angleIndex] = !flip; 

            if (swordAnim != null)
                swordAnim.PlayAttackAnimation(selectedAngle, flip);

            SFXManager.Instance.PlaySound(SFXManager.Instance.playerSlash);
            FireSlash(selectedAngle);
        }

        if (Input.GetMouseButtonDown(1) && Time.time >= nextWallTime)
        {
            SFXManager.Instance.PlaySound(SFXManager.Instance.wallRaise);
            nextWallTime = Time.time + wallCooldown;
            StartCoroutine(SpawnWallRoutine());
        }

        if (UIManager.Instance != null)
        {
            float remainingWallTime = nextWallTime - Time.time;
            if (remainingWallTime < 0) remainingWallTime = 0;
            
            UIManager.Instance.UpdateWallCooldown(remainingWallTime, wallCooldown);
        }
    }

    // ---------------- MECÁNICA DE TAJO ----------------
    GameObject CreateSlashObject(int index)
    {
        GameObject go = new GameObject("SDF_Slash_" + index);
        go.transform.SetParent(transform, false);

        CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
        go.AddComponent<Rigidbody>().isKinematic = true;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(slashLength, 0.1f, 0.5f);

        if (neonMaterial != null)
            visual.GetComponent<Renderer>().material = neonMaterial;

        cc.direction = 1;
        cc.radius = slashThickness;
        cc.height = slashLength;
        cc.isTrigger = true;

        SdfPrimitiveSubscriber sub = go.AddComponent<SdfPrimitiveSubscriber>();
        sub.shapeType = SdfPrimitiveSubscriber.PrimitiveType.Capsule;
        sub.isSubtractive = true;

        SlashProjectile projScript = go.AddComponent<SlashProjectile>();
        projScript.scarPrefab = this.scarPrefab;
        projScript.neonMaterial = this.neonMaterial;

        go.SetActive(false);
        return go;
    }

    void BuildCutPool()
    {
        cutPool = new GameObject[slashPoolSize];
        for (int i = 0; i < slashPoolSize; i++)
            cutPool[i] = CreateSlashObject(i);
    }

    void FireSlash(float selectedAngle)
    {
        if (cutPool[nextCutIndex] == null)
            cutPool[nextCutIndex] = CreateSlashObject(nextCutIndex);

        GameObject cut = cutPool[nextCutIndex];
        cut.transform.position = playerCamera.position + playerCamera.forward * 0.5f;

        Quaternion forwardRot = Quaternion.LookRotation(playerCamera.forward);
        Quaternion rollRot = Quaternion.Euler(0, 0, selectedAngle);
        cut.transform.rotation = forwardRot * rollRot;

        cut.SetActive(true);
        nextCutIndex = (nextCutIndex + 1) % slashPoolSize;
    }

    // ---------------- MECÁNICA DE MURO ----------------
    void BuildWall()
    {
        activeWall = new GameObject("SDF_EarthWall");
        
        BoxCollider bc = activeWall.AddComponent<BoxCollider>();
        bc.size = wallSize;
        bc.isTrigger = true;

        SdfPrimitiveSubscriber sub = activeWall.AddComponent<SdfPrimitiveSubscriber>();
        sub.shapeType = SdfPrimitiveSubscriber.PrimitiveType.Cube;
        sub.isSubtractive = false;

        activeWallCuttable = activeWall.AddComponent<CuttableSdfObject>();
        activeWallCuttable.maxHealth = 30;
        activeWallCuttable.damagePerSlash = 10;
        activeWallCuttable.scarPrefab = this.scarPrefab;
        activeWallCuttable.sparkMaterial = this.neonMaterial;

        activeWall.transform.localScale = wallSize;
        activeWall.SetActive(false);
    }

    IEnumerator SpawnWallRoutine()
    {
        if (activeWall == null) BuildWall(); 

        activeWallCuttable.ResetHealth();

        Vector3 targetPos = playerCamera.position + playerCamera.forward * wallSpawnDistance;
        Vector3 rayStart = new Vector3(targetPos.x, playerCamera.position.y + 5f, targetPos.z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 15f))
        {
            targetPos.y = hit.point.y + (wallSize.y / 2f);
        }
        else
        {
            targetPos.y = transform.position.y + (wallSize.y / 2f);
        }

        Vector3 finalPos = targetPos;
        Vector3 hiddenPos = finalPos + Vector3.down * wallSize.y;

        activeWall.transform.position = targetPos;
        Vector3 lookAtPos = transform.position;
        lookAtPos.y = activeWall.transform.position.y; 
        activeWall.transform.LookAt(lookAtPos);

        activeWall.SetActive(true);
        
        float animDuration = 0.2f;
        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            if (activeWall == null) yield break;
            activeWall.transform.position = Vector3.Lerp(hiddenPos, finalPos, elapsed / animDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        activeWall.transform.position = finalPos;

        elapsed = 0f;
        float holdTime = wallDuration - (animDuration * 2);
        while (elapsed < holdTime)
        {
            if (activeWall == null) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < animDuration)
        {
            activeWall.transform.position = Vector3.Lerp(finalPos, hiddenPos, elapsed / animDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        activeWall.SetActive(false);
    }
}