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
    public GameObject servingBarriers;

    [Header("Ball Settings")]
    public Transform ballSpawnPoint;
    public GameObject ballPrefab; // pre-spawned recommended  

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
        cam = Camera.main;
        upForce = ogUpForce;
    }

    public override void OnNetworkSpawn()
    {
        if (PlayerReferenceRelay.Instance != null)
        {
            try { PlayerReferenceRelay.Instance.ApplyTo(this); }
            catch { }
        }

        if (IsOwner && cam == null)
        {
            cam = Camera.main;
            if (cam == null) Debug.LogWarning("[NetworkedPlayerHitting] Camera.main is null on owner.");
        }

        // Assign aim targets **after network ownership is guaranteed**  
        GameObject playerAimObj = GameObject.Find("PlayerAim");
        GameObject oppAimObj = GameObject.Find("OppAim");

        if (IsOwner)
            aimTarget = playerAimObj?.transform;
        else
            aimTarget = oppAimObj?.transform;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentBall == null)
            {
                RequestBallSpawnServerRpc();
                ServeBallLocal();
                ServeBallServerRpc();
            }
            else if (nearBall && serving)
            {
                ServeBallLocal();
                ServeBallServerRpc();
            }
        }
    }

    [ServerRpc]
    private void RequestBallSpawnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBall != null || ballPrefab == null || ballSpawnPoint == null) return;

        currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        var netObj = currentBall.GetComponent<NetworkObject>();
        if (netObj != null) netObj.Spawn(true);

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb != null) rb.useGravity = false;

        collisionTracker = currentBall.GetComponent<CollisionTrackerBall>();
        nearBall = true;
    }

    public void ResetBallPosition()
    {
        if (currentBall == null && ballPrefab != null && ballSpawnPoint != null)
            currentBall = ballPrefab;

        if (currentBall != null && ballSpawnPoint != null)
        {
            currentBall.transform.position = ballSpawnPoint.position;
            currentBall.transform.rotation = ballSpawnPoint.rotation;

            Rigidbody rb = currentBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
            }

            collisionTracker = currentBall.GetComponent<CollisionTrackerBall>();
            nearBall = true;
        }
    }

    private void ServeBallLocal()
    {
        if (currentBall == null) return;
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.useGravity = true;
        rb.linearVelocity = Vector3.up * (strength / 2f);

        serving = false;
        if (servingBarriers != null) servingBarriers.SetActive(false);
    }

    [ServerRpc]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBall == null) return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.useGravity = true;
        rb.linearVelocity = Vector3.up * (strength / 2f);

        serving = false;
        if (servingBarriers != null) servingBarriers.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner || other == null || other.tag != "Ball") return;

        nearBall = true;
        if (!hitting || serving) return;

        // Calculate upForce  
        upForce = ogUpForce;
        if (transform.position.x > 35.5f) upForce += 2f;
        if (transform.position.z > -6.25f && transform.position.z < 6.25f) upForce += 2f;

        // Spell effects  
        if (spellEffects != null && spellEffects.plrHitSpell)
            spellEffects.castSpell();

        Vector3 targetPos = aimTarget != null ? aimTarget.position : transform.position + transform.forward;

        PlayHitSound(other.ClosestPoint(transform.position));
        ApplyHitPhysicsLocally(targetPos, upForce, strength);
        HitBallServerRpc(targetPos, upForce, strength);

        if (spellEffects != null && spellEffects.resetOnPlrHit)
            spellEffects.resetSpellEffect();

        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }

        if (spellEffects != null && spellEffects.spellHit)
            StartCoroutine(HitSlowdown());
    }

    private void ApplyHitPhysicsLocally(Vector3 aimPos, float upF, float force)
    {
        if (currentBall == null) return;
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 dir = (aimPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        rb.useGravity = true;
        rb.linearVelocity = dir * force + new Vector3(0, upF, 0);
    }

    [ServerRpc]
    private void HitBallServerRpc(Vector3 aimPos, float upF, float force)
    {
        if (!IsServer || currentBall == null) return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 dir = (aimPos - transform.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

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

        int index = hitSounds.Length == 1 ? 0 : Random.Range(0, hitSounds.Length);
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
        if (cam == null) { isHitSlowActive = false; yield break; }

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