using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkedPlayerHitting : NetworkBehaviour
{
    [Header("References")]
    public Transform aimTarget;
    public TwoHandIKController_Opponent OppIKRig;
    public SpellEffects spellEffects;
    public ScoreManager scoreManager;
    public AudioSource audioSource;
    public GameObject opponent;
    public GameObject servingBarriers;

    [Header("Ball Settings")]
    public Transform ballSpawnPoint;
    public GameObject ballPrefab; // kept for ghosting if you want

    private bool nearBall = false;
    private bool serving = true;
    private bool hitting = true;

    [Header("Force Settings")]
    public float strength = 25f;
    public float ogUpForce = 11f;
    private float upForce;

    [Header("Audio")]
    public AudioClip[] hitSounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;
    private float lastHitSfxTime = -999f;

    private Camera cam;
    private CollisionTrackerBall collisionTracker;

    // spawn immunity to prevent immediate accidental hits right after spawn
    private bool justSpawnedBall = false;
    private float spawnImmunitySeconds = 0.15f; // tweak to taste

    private void Awake()
    {
        cam = Camera.main;
        upForce = ogUpForce;
    }

    public override void OnNetworkSpawn()
    {
        try
        {
            if (PlayerReferenceRelay.Instance != null)
                PlayerReferenceRelay.Instance.ApplyTo(this);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[NetworkedPlayerHitting] Relay ApplyTo threw: {e.Message}");
        }

        if (IsOwner && cam == null)
        {
            cam = Camera.main;
            if (cam == null) Debug.LogWarning("[NetworkedPlayerHitting] Camera.main is null on owner.");
        }

        if (IsOwner)
            Debug.Log($"[NetworkedPlayerHitting] Owner ({OwnerClientId}) initialized references.");
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Spawn a new ball if none exists (player requests manager spawn at their spawn point)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (NetworkBallManager.Instance == null)
            {
                Debug.LogWarning("[NetworkedPlayerHitting] No NetworkBallManager present.");
            }
            else
            {
                // Start spawn-and-serve coroutine so we don't immediately hit the spawned ball
                StartCoroutine(SpawnAndServeCoroutine());
            }
        }

        // Serve if near the ball and not already serving (fallback if you want manual serve)
        if (Input.GetKeyDown(KeyCode.E) && nearBall && serving)
        {
            if (NetworkBallManager.Instance != null)
            {
                NetworkBallManager.Instance.ServeBallServerRpc(upForce, strength);
                serving = false;
                if (servingBarriers != null) servingBarriers.SetActive(false);
            }
        }
    }

    private IEnumerator SpawnAndServeCoroutine()
    {
        // protect from double-start
        if (justSpawnedBall) yield break;

        justSpawnedBall = true;
        serving = true; // temporarily treat as serving phase (no hits)

        // Request server to spawn at this player's spawn point (or player transform if null)
        Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position : transform.position;
        Quaternion spawnRot = ballSpawnPoint != null ? ballSpawnPoint.rotation : transform.rotation;
        NetworkBallManager.Instance.RequestSpawnBallServerRpc(spawnPos, spawnRot);

        // Wait a short time so physics colliders settle and the player isn't overlapping the ball
        yield return new WaitForSeconds(spawnImmunitySeconds);

        // Now tell the server to perform the upward serve
        NetworkBallManager.Instance.ServeBallServerRpc(upForce, strength);
        serving = false;

        // remove spawn immunity after a little more time (ensure serve has started)
        yield return new WaitForSeconds(0.05f);
        justSpawnedBall = false;

        if (servingBarriers != null) servingBarriers.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only owner runs the detection & requests a server hit
        if (!IsOwner || other == null || !other.CompareTag("Ball")) return;

        // If we just spawned the ball, ignore triggers for a short time to allow serving
        if (justSpawnedBall) return;

        nearBall = true;
        if (!hitting || serving) return;

        // calculate upForce safely
        try
        {
            if (35.5f < transform.position.x) upForce = ogUpForce + 2f;
            else upForce = ogUpForce;
            if (-6.25f < transform.position.z || transform.position.z < 6.25f) upForce += 2f;
        }
        catch { upForce = ogUpForce; }

        // If spellEffects is assigned and this is a player-hit spell, run it locally
        if (spellEffects != null && spellEffects.plrHitSpell)
        {
            try { spellEffects.castSpell(); } catch (System.Exception e) { Debug.LogWarning($"[NetworkedPlayerHitting] spellEffects.castSpell threw: {e.Message}"); }
        }

        // Aim target adjustments (local visual)
        Vector3 aimTargetPos = aimTarget != null ? aimTarget.position : transform.position + transform.forward;
        Vector3 oppPos = opponent != null ? opponent.transform.position : transform.position;
        float xPos = oppPos.x > 0f ? -2f : 2f;
        if (transform.position.z < 5f || transform.position.x < -5f || transform.position.x > 5f)
            xPos = 0f;
        if (aimTarget != null)
            aimTarget.position = new Vector3(xPos, aimTargetPos.y, aimTargetPos.z);

        // Audio local feedback (instant)
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitSound(contactPoint);

        // Request server to apply physics via manager
        Vector3 aimPos = aimTarget != null ? aimTarget.position : transform.position + transform.forward;
        if (NetworkBallManager.Instance != null)
        {
            NetworkBallManager.Instance.HitBallServerRpc(aimPos, upForce, strength);
        }
        else
        {
            Debug.LogWarning("[NetworkedPlayerHitting] Hit requested but no NetworkBallManager found.");
        }

        // Reset spell if needed
        if (spellEffects != null && spellEffects.resetOnPlrHit)
        {
            try { spellEffects.resetSpellEffect(); } catch (System.Exception e) { Debug.LogWarning($"[NetworkedPlayerHitting] resetSpellEffect threw: {e.Message}"); }
        }

        // Collision tracker (local copy attempt)
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }

        // Trigger local slowdown effect if spell flagged as hit
        if (spellEffects != null && spellEffects.spellHit)
            StartCoroutine(HitSlowdown());
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && other.CompareTag("Ball"))
            nearBall = false;
    }

    private void PlayHitSound(Vector3 contactPoint)
    {
        if (audioSource == null || hitSounds == null || hitSounds.Length == 0) return;
        if (Time.time - lastHitSfxTime < minInterval) return;
        lastHitSfxTime = Time.time;

        int index = (hitSounds.Length == 1) ? 0 : Random.Range(0, hitSounds.Length);
        audioSource.transform.position = contactPoint;
        float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float vol = Mathf.Clamp01(1f + Random.Range(-volumeJitter, volumeJitter));
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(hitSounds[index], vol);
    }

    private static bool isHitSlowActive = false;
    private IEnumerator HitSlowdown()
    {
        if (isHitSlowActive) yield break;
        isHitSlowActive = true;

        if (SpellEffects.isSpellSlowdownActive)
            yield return new WaitUntil(() => SpellEffects.isSpellSlowdownActive == false);

        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            isHitSlowActive = false;
            yield break;
        }

        float originalFOV = cam.fieldOfView;
        float originalTimeScale = Time.timeScale;

        try
        {
            cam.fieldOfView = 60.5f;
            Time.timeScale = 0.1f;
            yield return new WaitForSecondsRealtime(0.08f);
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            if (cam != null) cam.fieldOfView = originalFOV;
            isHitSlowActive = false;
        }
    }
}
