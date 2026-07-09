using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ====================================================================
// BossController.cs
// Gestiona el bucle de combate, la selección de ataques y la vida.
// ====================================================================
public class BossController : MonoBehaviour
{
    private Coroutine hitReactionCoroutine;

    public enum BossState { Idle, Telegraphing, Attacking, Cooldown, Dead }
    
    [Header("Estado Actual (Solo lectura)")]
    public BossState currentState = BossState.Idle;

    [Header("Referencias")]
    public Transform player;
    public Transform bossCore;

    [Header("Ajustes de Combate")]
    public float timeBetweenAttacks = 2f;
    public int maxHealth = 1000;
    private int currentHealth;

    [Header("Prefabs y Efectos de Ataques")]
    public GameObject sdfProjectilePrefab;
    public LineRenderer railgunLine;
    public LineRenderer gatlingLine;
    public LineRenderer sweepLine1;
    public LineRenderer sweepLine2;
    public GameObject sdfCageWallPrefab;

    [Header("Sistema de Defensas")]
    public int activeTowers = 4;
    
    [Header("Efectos de Muerte")]
    public Material neonMaterial;

    void Start()
    {
        currentHealth = maxHealth;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBossCoreHealth(currentHealth, maxHealth);
            UIManager.Instance.SetCoreInvulnerable();
        }
        
        if (player == null)
            player = Camera.main.transform;

        if (railgunLine != null)
            railgunLine.gameObject.SetActive(false);

        StartCoroutine(CombatLoopRoutine());
    }

    void Update()
    {
        if (bossCore != null)
        {
            Shader.SetGlobalVector("_BossCorePos", bossCore.position);
            Shader.SetGlobalVector("_BossCoreForward", bossCore.forward);
        }
    }

    // ---------------- LÓGICA CENTRAL ----------------
    
    IEnumerator CombatLoopRoutine()
    {
        yield return new WaitForSeconds(2f);

        while (currentState != BossState.Dead)
        {
            if (IsCoreExposed())
            {
                StartCoroutine(LastStandRoutine());
                yield break;
            }

            currentState = BossState.Idle;
            int attackChoice = Random.Range(0, 5);
            currentState = BossState.Attacking;

            if (attackChoice == 0) yield return StartCoroutine(Attack_Railgun());
            else if (attackChoice == 1) yield return StartCoroutine(Attack_SDFProjectiles());
            else if (attackChoice == 2) yield return StartCoroutine(Attack_Gatling());
            else if (attackChoice == 3) yield return StartCoroutine(Attack_Sweep());
            else if (attackChoice == 4) yield return StartCoroutine(Attack_Cage());

            currentState = BossState.Cooldown;
            yield return new WaitForSeconds(timeBetweenAttacks);
        }
    }

    // ---------------- ATAQUES ----------------

    // Ataque 1: Railgun (Check de Dash)
    IEnumerator Attack_Railgun()
    {
        currentState = BossState.Telegraphing;
        if (railgunLine != null) railgunLine.gameObject.SetActive(true);

        SFXManager.Instance.PlaySound(SFXManager.Instance.bossRailgunCharge);

        float prepTime = 1.5f;
        float elapsed = 0f;
        Vector3 targetPosition = player.position + Vector3.up * 1.2f;

        while (elapsed < prepTime)
        {
            targetPosition = player.position + Vector3.up * 1.2f;
            if (railgunLine != null)
            {
                railgunLine.SetPosition(0, bossCore.position);
                railgunLine.SetPosition(1, targetPosition);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        float delayTime = 0.5f;
        float delayElapsed = 0f;
        while (delayElapsed < delayTime)
        {
            if (railgunLine != null) 
            {
                railgunLine.SetPosition(0, bossCore.position);
            }
            delayElapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 direction = (targetPosition - bossCore.position).normalized;
        float distance = Vector3.Distance(bossCore.position, targetPosition) + 5f; 
        
        SFXManager.Instance.PlaySound(SFXManager.Instance.bossRailgunFire);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.35f);

        if (Physics.Raycast(bossCore.position, direction, out RaycastHit hit, distance))
        {
            if (railgunLine != null) railgunLine.SetPosition(1, hit.point);

            if (hit.collider.name == "SDF_EarthWall")
            {
                Debug.Log("¡Muro bloqueó el Railgun!");
            }
            else if (hit.collider.CompareTag("Player")) 
            {
                Debug.Log("¡Impacto directo al jugador!");
                hit.collider.GetComponent<PlayerHealth>().TakeDamage(20);
            }
        }
        else
        {
            if (railgunLine != null) railgunLine.SetPosition(1, bossCore.position + direction * distance);
        }

        Debug.Log("¡RAILGUN DISPARADO a la última posición guardada!");

        yield return new WaitForSeconds(0.3f);
        
        if (railgunLine != null) railgunLine.gameObject.SetActive(false);
    }

    // Ataque 2: Proyectiles SDF (Check de Tajo)
    IEnumerator Attack_SDFProjectiles()
    {
        currentState = BossState.Telegraphing;

        yield return new WaitForSeconds(1f);

        Debug.Log("¡Lanzando Proyectiles SDF!");
        
        if (sdfProjectilePrefab != null)
        {
            Instantiate(sdfProjectilePrefab, bossCore.position + Vector3.left * 3f, Quaternion.identity);
            Instantiate(sdfProjectilePrefab, bossCore.position + Vector3.right * 3f, Quaternion.identity);

            SFXManager.Instance.PlaySound(SFXManager.Instance.missileFlight);
        }

        yield return new WaitForSeconds(1f);
    }

    // Ataque 3: Ráfagas (Check de Muro)
    IEnumerator Attack_Gatling()
    {
        currentState = BossState.Telegraphing;
        Debug.Log("¡Cargando ametralladora!");
        yield return new WaitForSeconds(1f);

        Vector3 centerOffset = Vector3.up * 1.2f;
        Vector3 aimPosition = player.position + centerOffset;
        int numberOfShots = 15;

        for (int i = 0; i < numberOfShots; i++)
        {
            SFXManager.Instance.PlaySound(SFXManager.Instance.bossGatling);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.05f, 0.08f);

            aimPosition = Vector3.Lerp(aimPosition, player.position + centerOffset, 0.5f);

            if (gatlingLine != null)
            {
                gatlingLine.gameObject.SetActive(true);
                gatlingLine.SetPosition(0, bossCore.position);
                gatlingLine.SetPosition(1, aimPosition + (Random.insideUnitSphere * 1.5f));
            }

            Vector3 direction = (aimPosition - bossCore.position).normalized;
            if (Physics.Raycast(bossCore.position, direction, out RaycastHit hit, 50f))
            {
                if (hit.collider.CompareTag("Player")) 
                {
                    Debug.Log("¡Impacto directo al jugador!");
                    hit.collider.GetComponent<PlayerHealth>().TakeDamage(5);
                }
                
                if (hit.collider.name == "SDF_EarthWall")
                {
                    Debug.Log("Bloqueado por el muro");
                }
            }

            yield return new WaitForSeconds(0.05f);
            if (gatlingLine != null) gatlingLine.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.5f);
    }

    // Ataque 4: Barrido Circular (Check de Salto)
    IEnumerator Attack_Sweep()
    {
        currentState = BossState.Telegraphing;
        Debug.Log("¡El Boss va al centro y desciende para barrer!");

        GuardianBossFlight flightScript = GetComponent<GuardianBossFlight>();

        if (flightScript != null) flightScript.currentTargetRadius = 0f;
        yield return new WaitForSeconds(1.5f);
        if (flightScript != null) flightScript.pauseCoreTracking = true;

        bossCore.rotation = Quaternion.Euler(0f, bossCore.eulerAngles.y, 0f);

        Vector3 originalLocalPos = bossCore.localPosition;
        Vector3 targetSweepPos = new Vector3(0, -5f, 0);

        float descendTime = 1f;
        float elapsed = 0f;
        while (elapsed < descendTime)
        {
            bossCore.localPosition = Vector3.Lerp(originalLocalPos, targetSweepPos, elapsed / descendTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sweepLine1 != null) sweepLine1.gameObject.SetActive(true);
        if (sweepLine2 != null) sweepLine2.gameObject.SetActive(true);

        float sweepDuration = 4f;
        
        if (SFXManager.Instance.loopSource != null)
        {
            SFXManager.Instance.loopSource.clip = SFXManager.Instance.energySweep;
            SFXManager.Instance.loopSource.Play();
        }
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.15f);

        elapsed = 0f;
        while (elapsed < sweepDuration)
        {
            
            bossCore.Rotate(Vector3.up, 180f * Time.deltaTime);

            Vector3 rightDir = bossCore.right;
            Vector3 leftDir = -bossCore.right;

            if (sweepLine1 != null) { sweepLine1.SetPosition(0, bossCore.position); sweepLine1.SetPosition(1, bossCore.position + rightDir * 30f); }
            if (sweepLine2 != null) { sweepLine2.SetPosition(0, bossCore.position); sweepLine2.SetPosition(1, bossCore.position + leftDir * 30f); }

            RaycastHit hit;
            if (Physics.Raycast(bossCore.position, rightDir, out hit, 30f) || Physics.Raycast(bossCore.position, leftDir, out hit, 30f))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    Debug.Log("¡Barrido impactó al jugador!");
                    hit.collider.GetComponent<PlayerHealth>().TakeDamage(10);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sweepLine1 != null) sweepLine1.gameObject.SetActive(false);
        if (sweepLine2 != null) sweepLine2.gameObject.SetActive(false);

        if (SFXManager.Instance.loopSource != null) 
            SFXManager.Instance.loopSource.Stop();

        elapsed = 0f;
        float ascendTime = 1f;
        while (elapsed < ascendTime)
        {
            bossCore.localPosition = Vector3.Lerp(targetSweepPos, originalLocalPos, elapsed / ascendTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flightScript != null) 
        {
            flightScript.pauseCoreTracking = false;
            flightScript.currentTargetRadius = flightScript.orbitRadius; 
        }
    }

    // Ataque 5: Jaula Geométrica + Railgun Ineludible (Check de Tajo bajo presión)
    IEnumerator Attack_Cage()
    {
        currentState = BossState.Telegraphing;
        Debug.Log("¡Enjaulando al jugador!");

        Vector3 center = player.position;
        center.y = 1.5f; 

        List<GameObject> cageWalls = new List<GameObject>();
        Vector3[] finalPositions = new Vector3[4];
        Vector3[] hiddenPositions = new Vector3[4];

        if (sdfCageWallPrefab != null)
        {
            cageWalls.Add(Instantiate(sdfCageWallPrefab, center + Vector3.forward * 2f, Quaternion.Euler(0, 0, 0)));
            cageWalls.Add(Instantiate(sdfCageWallPrefab, center + Vector3.back * 2f, Quaternion.Euler(0, 0, 0)));
            cageWalls.Add(Instantiate(sdfCageWallPrefab, center + Vector3.left * 2f, Quaternion.Euler(0, 90, 0)));
            cageWalls.Add(Instantiate(sdfCageWallPrefab, center + Vector3.right * 2f, Quaternion.Euler(0, 90, 0)));
        }

        for (int i = 0; i < cageWalls.Count; i++)
        {
            finalPositions[i] = cageWalls[i].transform.position;
            hiddenPositions[i] = finalPositions[i] + Vector3.down * 4f;
            cageWalls[i].transform.position = hiddenPositions[i];
        }

        float animTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < animTime)
        {
            for (int i = 0; i < cageWalls.Count; i++)
            {
                if (cageWalls[i] != null)
                    cageWalls[i].transform.position = Vector3.Lerp(hiddenPositions[i], finalPositions[i], elapsed / animTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (railgunLine != null) railgunLine.gameObject.SetActive(true);
        float chargeTime = 2.2f;

        SFXManager.Instance.PlaySound(SFXManager.Instance.bossRailgunCharge);

        elapsed = 0f;

        while (elapsed < chargeTime)
        {
            if (railgunLine != null)
            {
                railgunLine.SetPosition(0, bossCore.position);
                railgunLine.SetPosition(1, center);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("¡BOOM! Railgun de la jaula dispara.");

        SFXManager.Instance.PlaySound(SFXManager.Instance.bossRailgunFire);
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.25f, 0.5f);
            CameraShake.Instance.HitStop(0.05f, 0.03f);
        }

        if (Vector3.Distance(player.position, center) < 2.5f)
        {
            Debug.Log("¡Jugador no escapó de la jaula! DAÑO MASIVO.");
            player.GetComponent<PlayerHealth>().TakeDamage(50);
        }

        yield return new WaitForSeconds(0.5f);
        if (railgunLine != null) railgunLine.gameObject.SetActive(false);

        elapsed = 0f;
        while (elapsed < animTime)
        {
            for (int i = 0; i < cageWalls.Count; i++)
            {
                if (cageWalls[i] != null)
                    cageWalls[i].transform.position = Vector3.Lerp(finalPositions[i], hiddenPositions[i], elapsed / animTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        foreach (var wall in cageWalls)
        {
            if (wall != null) Destroy(wall);
        }
    }

    // ---------------- DAÑO Y VIDA ----------------

    public void TakeDamage(int damageAmount)
    {
        if (currentState == BossState.Dead) return;
        
        SFXManager.Instance.PlaySound(SFXManager.Instance.bossHurt);
        currentHealth -= damageAmount;
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.1f, 0.2f);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowHitmarker();
            UIManager.Instance.UpdateBossCoreHealth(currentHealth, maxHealth);
        }

        Debug.Log($"Boss recibe {damageAmount} de daño. Vida restante: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (currentState == BossState.Dead) return;
        currentState = BossState.Dead;

        StopAllCoroutines();

        if (railgunLine != null) railgunLine.enabled = false;
        if (gatlingLine != null) gatlingLine.enabled = false;
        if (sweepLine1 != null) sweepLine1.enabled = false;
        if (sweepLine2 != null) sweepLine2.enabled = false;

        Debug.Log("¡El Boss ha sido derrotado!");
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        GuardianBossFlight flightScript = GetComponent<GuardianBossFlight>();
        if (flightScript != null) flightScript.enabled = false;

        SFXManager.Instance.StartCriticalVibration();

        Vector3 originalPos = bossCore.position;

        float elapsed = 0f;
        while (elapsed < 1f)
        {
            bossCore.position = originalPos + Random.insideUnitSphere * 0.5f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        bossCore.position = originalPos; 
        bossCore.gameObject.SetActive(false);

        SFXManager.Instance.ExecuteFinalExplosion();

        for (int i = 0; i < 40; i++)
        {
            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.transform.position = originalPos + Random.insideUnitSphere * 2f;
            frag.transform.localScale = Vector3.one * Random.Range(0.3f, 1f);

            if (neonMaterial != null) frag.GetComponent<Renderer>().material = neonMaterial;

            Rigidbody rb = frag.AddComponent<Rigidbody>();
            rb.AddExplosionForce(2000f, originalPos, 15f);
            Destroy(frag, 5f);
        }

        for (int i = 0; i < 12; i++)
        {
            GameObject rayObj = new GameObject("DeathRay");
            LineRenderer lr = rayObj.AddComponent<LineRenderer>();

            if (neonMaterial != null) lr.material = neonMaterial;
            lr.startWidth = 0.8f; 
            lr.endWidth = 0f;

            lr.SetPosition(0, originalPos);
            lr.SetPosition(1, originalPos + Random.onUnitSphere * 40f); 

            Destroy(rayObj, 0.5f);
        }

        yield return new WaitForSeconds(2f);

        if (GameSession.Instance != null) 
        {
            if (GameSession.Instance.bgmSource != null) 
                GameSession.Instance.bgmSource.Play();
                
            GameSession.Instance.TriggerVictory();
        }

    }

    public bool IsCoreExposed()
    {
        return activeTowers <= 0;
    }

    public void ReportTowerDestroyed()
    {
        activeTowers--;
        if (activeTowers <= 0)
        {
            Debug.Log("¡NÚCLEO EXPUESTO! ¡ES TU OPORTUNIDAD!");

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.3f, 0.4f);
                CameraShake.Instance.HitStop(0.15f, 0.02f);
            }

            if (UIManager.Instance != null)
                UIManager.Instance.SetCoreVulnerable();
        }
    }

    // FASE FINAL: El jefe baja al centro y barre infinitamente hasta morir
    IEnumerator LastStandRoutine()
    {
        currentState = BossState.Attacking;
        Debug.Log("¡ÚLTIMA DEFENSA! El Boss barrerá infinitamente.");

        GuardianBossFlight flightScript = GetComponent<GuardianBossFlight>();

        if (flightScript != null) flightScript.currentTargetRadius = 0f;
        yield return new WaitForSeconds(1.5f);

        if (flightScript != null) flightScript.pauseCoreTracking = true;
        bossCore.rotation = Quaternion.Euler(0f, bossCore.eulerAngles.y, 0f);

        Vector3 originalLocalPos = bossCore.localPosition;
        Vector3 targetSweepPos = new Vector3(0, -5f, 0);

        float descendTime = 1f;
        float elapsed = 0f;
        while (elapsed < descendTime)
        {
            bossCore.localPosition = Vector3.Lerp(originalLocalPos, targetSweepPos, elapsed / descendTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sweepLine1 != null) sweepLine1.gameObject.SetActive(true);
        if (sweepLine2 != null) sweepLine2.gameObject.SetActive(true);

        SFXManager.Instance.StartLastStand();

        while (currentState != BossState.Dead)
        {
            bossCore.Rotate(Vector3.up, 180f * Time.deltaTime);

            Vector3 rightDir = bossCore.right;
            Vector3 leftDir = -bossCore.right;

            if (sweepLine1 != null) { sweepLine1.SetPosition(0, bossCore.position); sweepLine1.SetPosition(1, bossCore.position + rightDir * 30f); }
            if (sweepLine2 != null) { sweepLine2.SetPosition(0, bossCore.position); sweepLine2.SetPosition(1, bossCore.position + leftDir * 30f); }

            RaycastHit hit;
            if (Physics.Raycast(bossCore.position, rightDir, out hit, 30f) || Physics.Raycast(bossCore.position, leftDir, out hit, 30f))
            {
                PlayerHealth ph = hit.collider.GetComponent<PlayerHealth>();
                if (ph != null) ph.TakeDamage(10);
            }

            yield return null; 
        }
    }
    
    public void TriggerHitReaction()
    {
        if (currentState == BossState.Dead) return;

        if (hitReactionCoroutine != null) StopCoroutine(hitReactionCoroutine);
        hitReactionCoroutine = StartCoroutine(HitReactionRoutine());
    }

    private IEnumerator HitReactionRoutine()
    {
        GuardianBossFlight flightScript = GetComponent<GuardianBossFlight>();
        if (flightScript != null) flightScript.enabled = false;

        Vector3 originalPos = transform.position;
        float elapsed = 0f;
        
        while (elapsed < 0.15f)
        {
            transform.position = originalPos + Random.insideUnitSphere * 0.3f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPos;

        if (flightScript != null && currentState != BossState.Dead)
        {
            flightScript.enabled = true;
        }
    }
}