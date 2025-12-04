using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkedBall : NetworkBehaviour
{
    [Header("References")]
    public Transform aimTarget;
    public GameObject opponent;
    public TwoHandIKController_Opponent OppIKRig;
    public SpellEffects spellEffects;
    public ScoreManager scoreManager;
    private NetworkedUIManager uiManager;

    [Header("Rally Tracking")]
    private NetworkVariable<int> rallyCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> greenRallyCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool green = false;
    private NetworkVariable<int> greenPoints = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    private float serveProtectionWindow = 0.3f; // Can't hit for 0.3s after serving

    // Local state
    private bool nearBall = false;
    private bool hitting = true;
    private bool localServing = true; // Local flag, no networking needed

    private Camera cam;
    private static GameObject currentBallInstance;
    private static float lastGlobalServerHitTime = -999f; // Static for ALL players
    private static float serverHitDebounce = 0.2f;

    // PAUSE BALL FUNCTIONS FOR TESTING PURPOSES
    private static Vector3 savedVelocity;
    private static RigidbodyConstraints savedConstraints;
    private static bool ballPaused = false;

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

        // Get this player's UI manager
        if (IsOwner)
        {
            uiManager = GetComponent<NetworkedUIManager>();
            if (uiManager == null)
                Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} has no NetworkedUIManager component!");
        }

        // Subscribe to NetworkVariable changes to update UI
        rallyCount.OnValueChanged += OnRallyCountChanged;
        greenPoints.OnValueChanged += OnGreenPointsChanged;

        // Initialize UI with current values
        if (IsOwner && uiManager != null)
        {
            uiManager.UpdateRallyCount(rallyCount.Value);
            if (greenPoints.Value > 0)
            {
                uiManager.UpdateGreenPoints(greenPoints.Value);
            }
        }

        if (IsOwner)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} spawned and ready.");
        }
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from NetworkVariable changes
        rallyCount.OnValueChanged -= OnRallyCountChanged;
        greenPoints.OnValueChanged -= OnGreenPointsChanged;
    }

    private void OnRallyCountChanged(int previousValue, int newValue)
    {
        // Update UI when rally count changes
        if (IsOwner && uiManager != null)
        {
            uiManager.UpdateRallyCount(newValue);
        }
    }

    private void OnGreenPointsChanged(int previousValue, int newValue)
    {
        // Update UI when green points change
        if (IsOwner && uiManager != null)
        {
            uiManager.UpdateGreenPoints(newValue);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Spawn ball with E
        if (Input.GetKeyDown(KeyCode.E) && currentBallInstance == null && ballPrefab != null && ballSpawnPoint != null)
        {
            SpawnBallServerRpc(ballSpawnPoint.position, ballSpawnPoint.rotation);
        }

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
            ServeBallServerRpc();
            localServing = false;
            lastServeTime = Time.time; // Record serve time
            if (servingBarriers != null) servingBarriers.SetActive(false);
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

    [ClientRpc]
    private void NotifyBallSpawnedClientRpc()
    {
        // Find the ball on all clients
        currentBallInstance = GameObject.FindGameObjectWithTag("Ball");
        if (currentBallInstance != null)
        {
            nearBall = true;

            // Assign to IK rigs
            TwoHandIKController ikController = FindObjectOfType<TwoHandIKController>();
            if (ikController != null && IsOwner)
            {
                ikController.AssignBall(currentBallInstance.transform);
            }
            if (OppIKRig != null)
            {
                OppIKRig.AssignBall(currentBallInstance.transform);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallInstance == null) return;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;

            // Match original Ball.cs exactly: gentle upward toss
            // new Vector3(0, upForce, 0).normalized gives (0, 1, 0)
            // multiplied by strength/2 gives a gentle upward velocity
            Vector3 upVector = new Vector3(0, ogUpForce, 0);
            rb.linearVelocity = upVector.normalized * (strength / 2f);

            Debug.Log($"[Server] Ball served by client {rpcParams.Receive.SenderClientId} - velocity: {rb.linearVelocity}");
        }

        NotifyServeCompleteClientRpc();
    }

    [ClientRpc]
    private void NotifyServeCompleteClientRpc()
    {
        localServing = false;
        hitting = true;
        lastServeTime = Time.time; // All clients record the serve time
        if (servingBarriers != null) servingBarriers.SetActive(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void HitBallServerRpc(Vector3 direction, float force, float upwardForce, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallInstance == null) return;

        // Server-side debounce: ignore hits that come too quickly
        if (Time.time - lastGlobalServerHitTime < serverHitDebounce)
        {
            Debug.Log($"[Server] Ignoring hit from client {rpcParams.Receive.SenderClientId} - too soon after last hit ({Time.time - lastGlobalServerHitTime:F2}s ago)");
            return;
        }

        lastGlobalServerHitTime = Time.time;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Ensure gravity is always on during gameplay
            rb.useGravity = true;

            // Apply velocity: normalized direction * force + upward component
            Vector3 velocity = direction.normalized * force + new Vector3(0, upwardForce, 0);
            rb.linearVelocity = velocity;

            Debug.Log($"[Server] Ball hit by client {rpcParams.Receive.SenderClientId} - velocity: {velocity}, gravity: {rb.useGravity}");
        }

        // Notify all clients to play effects
        PlayHitEffectsClientRpc(currentBallInstance.transform.position);
    }

    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 position)
    {
        // Play particle effect
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

    [ServerRpc(RequireOwnership = false)]
    private void IncrementRallyCountServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        rallyCount.Value++;
        Debug.Log($"[Server] Rally count incremented to {rallyCount.Value}");

        if (green)
        {
            greenRallyCount.Value++;
            if (greenRallyCount.Value % 4 == 0)
            {
                greenPoints.Value++;
                if (scoreManager != null)
                {
                    scoreManager.greenPoints = greenPoints.Value;
                }
                Debug.Log($"[Server] Green points incremented to {greenPoints.Value}");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[NetworkedBall] Player {OwnerClientId} OnTriggerEnter with tag: {other?.tag}");

        if (other == null || other.tag != "Ball") return;

        Debug.Log($"[NetworkedBall] Player {OwnerClientId} detected ball - nearBall: {nearBall}, hitting: {hitting}, timeSinceLastHit: {Time.time - lastHitTime:F2}s, timeSinceServe: {Time.time - lastServeTime:F2}s");

        // Protection window: don't hit immediately after serving
        if (Time.time - lastServeTime < serveProtectionWindow)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} in serve protection window");
            return;
        }

        // Debounce
        if (Time.time - lastHitTime < hitDebounce)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} hit debounced");
            return;
        }

        nearBall = true;

        // Can't hit if hitting is disabled
        if (!hitting)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} can't hit - hitting is disabled");
            return;
        }

        // Only allow hit if this player is the closest to the ball
        if (!IsClosestPlayerToBall(other.transform.position))
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} not closest to ball, ignoring hit.");
            return;
        }

        lastHitTime = Time.time;
        Debug.Log($"[NetworkedBall] Player {OwnerClientId} processing hit!");

        // Calculate upforce based on position
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

        // Play local audio immediately
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitsound(contactPoint);

        // Request server to apply physics
        Vector3 direction = targetPos - transform.position;
        Debug.Log($"[NetworkedBall] Player {OwnerClientId} hitting - direction: {direction}, targetPos: {targetPos}, myPos: {transform.position}, force: {strength}, upForce: {upForce}");
        HitBallServerRpc(direction, strength, upForce);

        // Update rally count via server
        IncrementRallyCountServerRpc();

        // *** NEW: Check for Fireball knockback BEFORE resetting spell effects ***
        CheckFireballHit();

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

        // If we have an opponent reference, check their distance
        if (opponent != null)
        {
            float opponentDistance = Vector3.Distance(opponent.transform.position, ballPosition);
            return myDistance < opponentDistance;
        }

        // If no opponent, we're the only player, so we're closest
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
        hitting = false;      // Not hitting yet
        localServing = true;  // Ready to serve the next ball

        // Optional: re-enable serving barriers
        if (servingBarriers != null)
            servingBarriers.SetActive(true);

        Debug.Log($"[NetworkedBall] Player {OwnerClientId} set to SERVING state.");
    }

    /// <summary>
    /// Check if player is hitting a Fireball-enchanted ball and apply knockback
    /// </summary>
    private void CheckFireballHit()
    {
        if (spellEffects == null) return;

        // Check if Fireball spell is active and should trigger on opponent hit
        if (!spellEffects.resetOnOppHit || spellEffects.spellName != "Fireball")
            return;

        GameObject ballObj = GameObject.FindGameObjectWithTag("Ball");
        if (ballObj == null) return;

        CollisionTrackerBall tracker = ballObj.GetComponent<CollisionTrackerBall>();
        if (tracker == null) return;

        // Determine who cast the Fireball by checking who hit the ball last
        // If opponent hit it last, then we're hitting their Fireball-enchanted ball
        bool opponentCastFireball = (tracker.LastHitWizard != "Player");

        if (opponentCastFireball && opponent != null)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} hit opponent's Fireball ball - applying knockback!");

            // Set spell context so SpellEffects knows opponent is caster, we are victim
            spellEffects.AutoSetContext(opponent);

            // Apply knockback to THIS player (the one hitting the Fireball ball)
            spellEffects.ApplyFireballKnockback(gameObject);

            // Reset the spell effect
            spellEffects.resetSpellEffect();

            Debug.Log($"[NetworkedBall] Fireball knockback applied and spell reset");
        }
        else
        {
            Debug.Log($"[NetworkedBall] Fireball active but no knockback - LastHitWizard: {tracker.LastHitWizard}");
        }
    }
}