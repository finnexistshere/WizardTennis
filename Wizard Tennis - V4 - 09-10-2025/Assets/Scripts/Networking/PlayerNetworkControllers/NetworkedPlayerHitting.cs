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
    public GameObject ballPrefab;

    private static GameObject currentBall;
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

    private void Awake()
    {
        // Defer to OnNetworkSpawn for network-dependent initialization.
        cam = Camera.main;
        upForce = ogUpForce;
    }

    public override void OnNetworkSpawn()
    {
        // Defensive: guard against missing relay or exceptions in relay
        try
        {
            if (PlayerReferenceRelay.Instance != null)
                PlayerReferenceRelay.Instance.ApplyTo(this);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[NetworkedPlayerHitting] Relay ApplyTo threw: {e.Message}");
        }

        // Ensure owner has a camera reference if not found in Awake
        if (IsOwner && cam == null)
        {
            cam = Camera.main;
            if (cam == null)
                Debug.LogWarning("[NetworkedPlayerHitting] Camera.main is null on owner.");
        }

        if (IsOwner)
            Debug.Log($"[NetworkedPlayerHitting] Owner ({OwnerClientId}) initialized references.");
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Spawn a new ball if none exists
        if (Input.GetKeyDown(KeyCode.E) && currentBall == null && ballPrefab != null && ballSpawnPoint != null)
        {
            RequestBallSpawnServerRpc();
        }

        // Serve if near the ball
        if (Input.GetKeyDown(KeyCode.E) && nearBall && serving && currentBall != null)
        {
            ServeBallServerRpc();
        }
    }

    [ServerRpc]
    private void RequestBallSpawnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (currentBall != null) return;
        if (ballPrefab == null || ballSpawnPoint == null) { Debug.LogWarning("[Server] Ball prefab or spawn point missing."); return; }

        currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        currentBall.tag = "Ball";

        var rb = currentBall.GetComponent<Rigidbody>();
        if (rb != null) rb.useGravity = false;

        NetworkObject netObj = currentBall.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn(true);
        else
            Debug.LogWarning("[Server] Spawned ball has no NetworkObject!");

        collisionTracker = currentBall.GetComponent<CollisionTrackerBall>();
        nearBall = true;

        if (GameManager.Instance != null)
            GameManager.Instance.UnlockPickupSpawning();

        // Assign to IK rigs (local-only assignments, safe on server)
        var ikController = FindObjectOfType<TwoHandIKController>();
        if (ikController != null)
            ikController.AssignBall(currentBall.transform);
        if (OppIKRig != null)
            OppIKRig.AssignBall(currentBall.transform);

        Debug.Log("[Server] Spawned ball and assigned to both IK rigs.");
    }

    [ServerRpc]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (currentBall == null) return;

        var rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) { Debug.LogWarning("[Server] Ball missing Rigidbody on serve."); return; }

        rb.useGravity = true;
        // guard against zero vectors
        Vector3 upVec = new Vector3(0f, upForce, 0f);
        if (upVec.sqrMagnitude <= 0.0001f) upVec = Vector3.up * ogUpForce;
        rb.linearVelocity = upVec.normalized * (strength / 2f);

        serving = false;
        if (servingBarriers != null) servingBarriers.SetActive(false);

        Debug.Log("[Server] Ball served.");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only owner should run the hit logic
        if (!IsOwner || other == null || other.tag != "Ball") return;

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

        // If spellEffects is assigned and this is a player-hit spell, run it
        if (spellEffects != null && spellEffects.plrHitSpell)
        {
            try { spellEffects.castSpell(); } catch (System.Exception e) { Debug.LogWarning($"[NetworkedPlayerHitting] spellEffects.castSpell threw: {e.Message}"); }
        }

        // Aim target adjustments
        Vector3 aimTargetPos = aimTarget != null ? aimTarget.position : transform.position + transform.forward;
        Vector3 oppPos = opponent != null ? opponent.transform.position : transform.position;
        float xPos = oppPos.x > 0f ? -2f : 2f;
        if (transform.position.z < 5f || transform.position.x < -5f || transform.position.x > 5f)
            xPos = 0f;
        if (aimTarget != null)
            aimTarget.position = new Vector3(xPos, aimTargetPos.y, aimTargetPos.z);

        // Audio
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitSound(contactPoint);

        // Physics (server authoritative)
        HitBallServerRpc(aimTarget != null ? aimTarget.position : transform.position + transform.forward, upForce, strength);

        // Reset spell if needed
        if (spellEffects != null && spellEffects.resetOnPlrHit)
        {
            try { spellEffects.resetSpellEffect(); } catch (System.Exception e) { Debug.LogWarning($"[NetworkedPlayerHitting] resetSpellEffect threw: {e.Message}"); }
        }

        // Collision tracker
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }

        // Trigger local slowdown effect if spell flagged as hit
        if (spellEffects != null && spellEffects.spellHit)
            StartCoroutine(HitSlowdown());
    }

    [ServerRpc]
    private void HitBallServerRpc(Vector3 aimPos, float upF, float force)
    {
        if (!IsServer) return;
        if (currentBall == null) return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 dir = (aimPos - transform.position);
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir = dir.normalized;

        rb.linearVelocity = dir * force + new Vector3(0, upF, 0);
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
    private Coroutine hitSlowCoroutine;
    private IEnumerator HitSlowdown()
    {
        // Prevent multiple overlapping hit slowdowns
        if (isHitSlowActive) yield break;
        isHitSlowActive = true;

        // If a global spell slowdown is active, wait until it's finished
        if (SpellEffects.isSpellSlowdownActive)
            yield return new WaitUntil(() => SpellEffects.isSpellSlowdownActive == false);

        // refresh camera reference if needed
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
            // Safely restore
            Time.timeScale = originalTimeScale;
            if (cam != null) cam.fieldOfView = originalFOV;
            isHitSlowActive = false;
        }
    }
}
