using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkedBall : NetworkBehaviour
{
    [Header("References")]
    public Transform aimTarget;
    public GameObject opponent;
    public TwoHandIKController_Opponent OppIKRig;
    public NetworkedSpellEffects spellEffects;
    public NetworkedScoreManager scoreManager;
    private NetworkedUIManager uiManager;

    [Header("Green Spell State")]
    public bool green = false;

    [Header("Ball Spawn")]
    public Transform ballSpawnPoint;
    public GameObject ballPrefab;
    public GameObject servingBarriers;

    [Header("Physics")]
    public float strength = 25f;
    public float ogUpForce = 11f;
    private float upForce;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;

    private float lastHitSfxTime = -999f;
    private float lastHitTime = -999f;
    private float hitDebounce = 0.15f;
    private float lastServeTime = -999f;
    private float serveProtectionWindow = 0.3f;

    // Local state
    private bool nearBall = false;
    private bool hitting = true;
    private bool localServing = true;

    private Camera cam;
    private static GameObject currentBallInstance;
    private static float lastGlobalServerHitTime = -999f;
    private static float serverHitDebounce = 0.2f;

    // Pause ball state
    private static Vector3 savedVelocity;
    private static RigidbodyConstraints savedConstraints;
    private static bool ballPaused = false;

    // IK Reference - each player tracks their own IK controller
    // IK references (local-only, per client)
    private TwoHandIKController localPlayerIK;
    private TwoHandIKController_Opponent localOpponentIK;

    private void Awake()
    {
        cam = Camera.main;
        upForce = ogUpForce;

        if (opponent == null)
            opponent = GameObject.Find("Opponent");
        if (servingBarriers == null)
            servingBarriers = GameObject.Find("ServingBarriers");
    }

    public override void OnNetworkSpawn()
    {
        // Apply references from the relay
        if (PlayerReferenceRelay.Instance != null)
        {
            try
            {
                PlayerReferenceRelay.Instance.ApplyTo(this);
                Debug.Log($"[NetworkedBall] Player {OwnerClientId} references applied via relay.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkedBall] Relay.ApplyTo failed: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[NetworkedBall] PlayerReferenceRelay.Instance is null!");
        }

        // Get this player's UI manager and IK controller
        if (IsOwner)
        {
            uiManager = GetComponent<NetworkedUIManager>();
            if (uiManager == null)
                Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} has no NetworkedUIManager component!");

            localPlayerIK = GetComponent<TwoHandIKController>();
            if (localPlayerIK == null)
                Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} has no TwoHandIKController component!");
            else
                Debug.Log($"[NetworkedBall] Player {OwnerClientId} IK controller found and cached.");
        }

        if (IsOwner)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} spawned and ready.");
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Spawn ball with E
        if (Input.GetKeyDown(KeyCode.E) && currentBallInstance == null && ballPrefab != null && ballSpawnPoint != null)
        {
            SpawnBallServerRpc(ballSpawnPoint.position, ballSpawnPoint.rotation);
            // Second call to help with Client Spawn Timing
            ServeBallServerRpc();
        }

        // Pause/Resume for testing
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!ballPaused)
                PauseBallServerRpc();
            else
                ResumeBallServerRpc();
        }

        // Serve ball with E when near it
        if (Input.GetKeyDown(KeyCode.E) && nearBall && localServing)
        {
            localServing = false;
            lastServeTime = Time.time;

            // Request server to handle serve and barrier deactivation
            ServeBallServerRpc();
            DeactivateBarriersServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PauseBallServerRpc()
    {
        if (currentBallInstance == null) return;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb == null) return;

        savedVelocity = rb.linearVelocity;
        savedConstraints = rb.constraints;

        rb.linearVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        ballPaused = true;
        PauseBallClientRpc();
    }

    [ClientRpc]
    private void PauseBallClientRpc()
    {
        ballPaused = true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResumeBallServerRpc()
    {
        if (currentBallInstance == null) return;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.constraints = RigidbodyConstraints.None;
        rb.useGravity = true;
        rb.linearVelocity = savedVelocity;

        ballPaused = false;
        ResumeBallClientRpc(savedVelocity);
    }

    [ClientRpc]
    private void ResumeBallClientRpc(Vector3 restoredVelocity)
    {
        savedVelocity = restoredVelocity;
        ballPaused = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void DeactivateBarriersServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        Debug.Log($"[Server] Deactivating serving barriers requested by client {rpcParams.Receive.SenderClientId}");

        // Server deactivates barriers and notifies all clients
        DeactivateBarriersClientRpc();
    }

    [ClientRpc]
    private void DeactivateBarriersClientRpc()
    {
        if (servingBarriers != null && servingBarriers.activeSelf)
        {
            servingBarriers.SetActive(false);
            Debug.Log($"[NetworkedBall] Client {OwnerClientId} - Serving barriers deactivated via ClientRpc.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnBallServerRpc(Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (currentBallInstance != null) return;

        GameObject ball = Instantiate(ballPrefab, position, rotation);
        ball.tag = "Ball";

        NetworkObject netObj = ball.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            netObj.Spawn(true);
            currentBallInstance = ball;

            NotifyBallSpawnedClientRpc();
            Debug.Log("[Server] Ball spawned successfully.");
        }
    }

    // ASSIGNING THE IK RIG ============================================
    [ClientRpc]
    private void NotifyBallSpawnedClientRpc()
    {
        currentBallInstance = GameObject.FindGameObjectWithTag("Ball");

        if (currentBallInstance != null)
        {
            // Local IK assignment
            if (localPlayerIK != null)
                localPlayerIK.AssignBall(currentBallInstance.transform);

            // Opponent IK assignment
                Debug.Log("[NotifyBallSpawnedClientRpc] Trying to Assign IK Rig for Opponent...");
                OppIKRig.StartAssignBallCoroutine();
        }
    }

    private IEnumerator AssignBallToIKAfterSpawn()
    {
        yield return new WaitForEndOfFrame();

        currentBallInstance = GameObject.FindGameObjectWithTag("Ball");

        if (currentBallInstance == null)
        {
            Debug.LogWarning($"[NetworkedBall] Client {OwnerClientId} could not find ball!");
            yield break;
        }

        nearBall = true;
        Transform ballTransform = currentBallInstance.transform;

        // --- LOCAL PLAYER IK ---
        if (localPlayerIK == null)
            localPlayerIK = GetComponent<TwoHandIKController>();

        if (localPlayerIK != null)
        {
            localPlayerIK.AssignBall(ballTransform);
            Debug.Log($"[NetworkedBall] Client {OwnerClientId} assigned ball to LOCAL player IK");
        }

        // --- OPPONENT IK ---
        if (localOpponentIK == null)
            localOpponentIK = FindOpponentIK();

        if (localOpponentIK != null)
        {
            localOpponentIK.AssignBall(ballTransform);
            Debug.Log($"[NetworkedBall] Client {OwnerClientId} assigned ball to OPPONENT IK");
        }
    }

    private TwoHandIKController_Opponent FindOpponentIK()
    {
        TwoHandIKController_Opponent[] allOppIKs =
            FindObjectsOfType<TwoHandIKController_Opponent>(true);

        foreach (var ik in allOppIKs)
        {
            NetworkObject netObj = ik.GetComponentInParent<NetworkObject>();

            if (netObj == null)
                continue;

            // Opponent = NOT owned by this client
            if (!netObj.IsOwner)
            {
                Debug.Log($"[NetworkedBall] Found opponent IK on client {netObj.OwnerClientId}");
                return ik;
            }
        }

        Debug.LogWarning("[NetworkedBall] Could not find opponent IK");
        return null;
    }
    // ===============================================================

    [ServerRpc(RequireOwnership = false)]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallInstance == null) return;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;

            // Fixed: Apply upForce directly as the Y component
            Vector3 serveVelocity = new Vector3(0, ogUpForce, 0);
            rb.linearVelocity = serveVelocity;

            Debug.Log($"[Server] Ball served by client {rpcParams.Receive.SenderClientId} - velocity: {rb.linearVelocity}");
        }

        NotifyServeCompleteClientRpc();
    }

    [ClientRpc]
    private void NotifyServeCompleteClientRpc()
    {
        localServing = false;
        hitting = true;
        lastServeTime = Time.time;
    }

    [ServerRpc(RequireOwnership = false)]
    private void HitBallServerRpc(Vector3 direction, float force, float upwardForce, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallInstance == null) return;

        // Server-side debounce
        if (Time.time - lastGlobalServerHitTime < serverHitDebounce)
        {
            Debug.Log($"[Server] Ignoring hit from client {rpcParams.Receive.SenderClientId} - too soon after last hit ({Time.time - lastGlobalServerHitTime:F2}s ago)");
            return;
        }

        lastGlobalServerHitTime = Time.time;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            Vector3 velocity = direction.normalized * force + new Vector3(0, upwardForce, 0);
            rb.linearVelocity = velocity;

            Debug.Log($"[Server] Ball hit by client {rpcParams.Receive.SenderClientId} - velocity: {velocity}");
        }

        PlayHitEffectsClientRpc(currentBallInstance.transform.position);
    }

    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 position)
    {
        GameObject particleObj = GameObject.FindGameObjectWithTag("Player Hit Particle");
        if (particleObj != null)
        {
            ParticleSystem particle = particleObj.GetComponent<ParticleSystem>();
            if (particle != null)
            {
                particle.transform.position = position;
                particle.Play();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.tag != "Ball") return;

        // Serve protection window
        if (Time.time - lastServeTime < serveProtectionWindow)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} in serve protection window");
            return;
        }

        // Hit debounce
        if (Time.time - lastHitTime < hitDebounce)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} hit debounced");
            return;
        }

        nearBall = true;

        if (!hitting)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} can't hit - hitting is disabled");
            return;
        }

        // Only allow hit if this player is closest to the ball
        if (!IsClosestPlayerToBall(other.transform.position))
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} not closest to ball, ignoring hit.");
            return;
        }

        lastHitTime = Time.time;
        Debug.Log($"[NetworkedBall] Player {OwnerClientId} processing hit!");

        // Calculate upforce
        if (35.5f < transform.position.x) upForce = ogUpForce + 2f;
        else upForce = ogUpForce;
        if (-6.25f < transform.position.z || transform.position.z < 6.25f) upForce += 2f;

        // Spell effects (local visual only)
        if (spellEffects != null && spellEffects.plrHitSpell)
        {
            spellEffects.castSpell();
        }

        // Calculate aim position
        float xPos = 0f;
        if (opponent != null)
        {
            Vector3 oppPos = opponent.transform.position;
            xPos = oppPos.x > 0f ? -2f : 2f;
        }
        if (transform.position.z < 5f || transform.position.x < -5f || transform.position.x > 5f)
            xPos = 0f;

        Vector3 targetPos = aimTarget != null ?
            new Vector3(xPos, aimTarget.position.y, aimTarget.position.z) :
            transform.position + transform.forward * 10f;

        // Play local audio
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitsound(contactPoint);

        // Request server to apply physics
        Vector3 direction = targetPos - transform.position;
        HitBallServerRpc(direction, strength, upForce);

        // Update rally count via NetworkedScoreManager
        if (NetworkedScoreManager.Instance != null)
        {
            NetworkedScoreManager.Instance.IncrementRallyCountServerRpc();
        }

        // Reset spell effects (if not already reset by Fireball)
        if (spellEffects != null && spellEffects.resetOnPlrHit)
        {
            spellEffects.resetSpellEffect();
        }

        // Update collision tracker
        CollisionTrackerBall tracker = other.GetComponent<CollisionTrackerBall>();
        if (tracker != null)
        {
            tracker.LastHitWizard = "Player";
            tracker.hasBounced = false;
        }

        // Hit slowdown effect
        if (spellEffects != null && spellEffects.spellHit)
        {
            StartCoroutine(HitSlowdown());
        }
    }

    private bool IsClosestPlayerToBall(Vector3 ballPosition)
    {
        float myDistance = Vector3.Distance(transform.position, ballPosition);

        if (opponent != null)
        {
            float opponentDistance = Vector3.Distance(opponent.transform.position, ballPosition);
            return myDistance < opponentDistance;
        }

        return true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && other.CompareTag("Ball"))
            nearBall = false;
    }

    private void PlayHitsound(Vector3 contactPoint)
    {
        if (audioSource == null || hitsounds == null || hitsounds.Length == 0) return;
        if (Time.time - lastHitSfxTime < minInterval) return;
        lastHitSfxTime = Time.time;

        int index = (hitsounds.Length == 1) ? 0 : Random.Range(0, hitsounds.Length);
        audioSource.transform.position = contactPoint;

        float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float vol = Mathf.Clamp01(1f + Random.Range(-volumeJitter, volumeJitter));

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(hitsounds[index], vol);
    }

    private static bool isHitSlowActive = false;
    private IEnumerator HitSlowdown()
    {
        if (isHitSlowActive) yield break;
        isHitSlowActive = true;

        if (SpellEffects.isSpellSlowdownActive)
            yield return new WaitUntil(() => SpellEffects.isSpellSlowdownActive == false);

        yield return null;

        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            isHitSlowActive = false;
            yield break;
        }

        float originalFOV = cam.fieldOfView;
        float originalTimeScale = Time.timeScale;

        cam.fieldOfView = 60.5f;
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.08f);

        Time.timeScale = originalTimeScale;
        cam.fieldOfView = originalFOV;
        isHitSlowActive = false;
    }

    public void SetToServingState()
    {
        hitting = false;
        localServing = true;

        if (servingBarriers != null)
            servingBarriers.SetActive(true);

        Debug.Log($"[NetworkedBall] Player {OwnerClientId} set to SERVING state.");
    }
}