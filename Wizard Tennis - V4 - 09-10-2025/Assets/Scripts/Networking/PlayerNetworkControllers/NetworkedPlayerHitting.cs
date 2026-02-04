using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

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
    
    [Header("Barrier Settings")]
    [Tooltip("If true, each player has their own barriers. If false, there's one shared set in the scene.")]
    public bool usePerPlayerBarriers = false;

    [Tooltip("Tag to use for finding serving barriers (default: 'ServingBarriers')")]
    public string barriersTag = "ServingBarriers";

    [Tooltip("Possible names for barrier GameObjects")]
    public string[] barrierNames = new string[]
    {
        "ServingBarriers",
        "Serving Barriers",
        "Barriers",
        "ServingWalls"
    };

    [Header("Physics")]
    public float strength = 25f;
    public float ogUpForce = 11f;
    private float upForce;

    [Header("Velocity Limits")]
    [Tooltip("Maximum horizontal speed the ball can travel (m/s)")]
    public float maxHorizontalVelocity = 35f;

    [Tooltip("Maximum vertical speed the ball can travel (m/s)")]
    public float maxVerticalVelocity = 25f;

    [Tooltip("Maximum total velocity magnitude (m/s)")]
    public float maxTotalVelocity = 40f;

    [Tooltip("If true, clamp velocity every physics frame")]
    public bool enforceVelocityCapInFixedUpdate = true;

    [Header("Network Sync Settings")]
    [Tooltip("How often to sync ball position (seconds)")]
    public float positionSyncInterval = 0.05f;
    
    [Tooltip("How smoothly clients interpolate to server position (0-1)")]
    [Range(0f, 1f)]
    public float positionLerpSpeed = 0.3f;

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

    // Network sync
    private float lastPositionSyncTime;
    private Vector3 targetPosition;
    private Vector3 targetVelocity;
    private bool hasReceivedSync = false;

    // Pause ball state
    private static Vector3 savedVelocity;
    private static RigidbodyConstraints savedConstraints;
    private static bool ballPaused = false;

    // IK Reference - each player tracks their own IK controller
    private TwoHandIKController localPlayerIK;
    private TwoHandIKController remotePlayerIK;

    public ServingBarrierController barrierController;

    private void Start()
    {
        barrierController = FindObjectOfType<ServingBarrierController>();
    }

    private void Awake()
    {
        cam = Camera.main;
        upForce = ogUpForce;

        if (opponent == null)
            opponent = GameObject.Find("Opponent");

        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.RegisterAsPlayer(gameObject);
        }

        if (servingBarriers == null)
        {
            Transform barriersChild = transform.Find("ServingBarriers");
            if (barriersChild != null)
            {
                servingBarriers = barriersChild.gameObject;
                Debug.Log($"[NetworkedBall] Player {OwnerClientId} found serving barriers as child");
            }
            else
            {
                Debug.LogWarning($"[NetworkedBall] Player has no ServingBarriers child - assign in inspector!");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
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

        if (IsOwner)
        {
            uiManager = GetComponent<NetworkedUIManager>();
            if (uiManager == null)
                Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} has no NetworkedUIManager component!");

            localPlayerIK = GetComponent<TwoHandIKController>();

            if (localPlayerIK == null)
                Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} has no TwoHandIKController!");
            else
                Debug.Log($"[NetworkedBall] Local IK cached for client {OwnerClientId}");
        }

        FindServingBarriers();

        if (IsOwner)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} spawned and ready.");

            if (servingBarriers != null)
            {
                servingBarriers.SetActive(true);
                Debug.Log($"[NetworkedBall] Player {OwnerClientId} initialized barriers to ACTIVE");
            }
        }

        // Server handles ball physics authoritatively
        if (IsServer && currentBallInstance != null)
        {
            Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }
    }

    private void FindServingBarriers()
    {
        Debug.Log($"[NetworkedBall] Player {OwnerClientId} searching for serving barriers...");

        if (usePerPlayerBarriers)
        {
            servingBarriers = FindPerPlayerBarriers();
        }
        else
        {
            servingBarriers = FindSharedBarriers();
        }

        if (servingBarriers != null)
        {
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} ? Found barriers: {servingBarriers.name}");
        }
        else
        {
            Debug.LogError($"[NetworkedBall] Player {OwnerClientId} ? FAILED to find serving barriers!");
        }
    }

    private GameObject FindPerPlayerBarriers()
    {
        Debug.Log($"[NetworkedBall] Looking for per-player barriers for client {OwnerClientId}");

        GameObject barriers = FindBarriersAsChild(transform);
        if (barriers != null)
        {
            Debug.Log($"[NetworkedBall] Found barriers as direct child: {barriers.name}");
            return barriers;
        }

        if (transform.parent != null)
        {
            barriers = FindBarriersAsChild(transform.parent);
            if (barriers != null)
            {
                Debug.Log($"[NetworkedBall] Found barriers as sibling: {barriers.name}");
                return barriers;
            }
        }

        GameObject[] taggedBarriers = GameObject.FindGameObjectsWithTag(barriersTag);
        foreach (GameObject obj in taggedBarriers)
        {
            NetworkObject netObj = obj.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == OwnerClientId)
            {
                Debug.Log($"[NetworkedBall] Found barriers by tag+ownership: {obj.name}");
                return obj;
            }
        }

        barriers = FindClosestBarriers();
        if (barriers != null)
        {
            Debug.Log($"[NetworkedBall] Found barriers by proximity: {barriers.name}");
            return barriers;
        }

        Debug.LogWarning($"[NetworkedBall] Could not find per-player barriers for client {OwnerClientId}");
        return null;
    }

    private GameObject FindSharedBarriers()
    {
        Debug.Log($"[NetworkedBall] Looking for shared scene barriers");

        GameObject barriers = GameObject.FindGameObjectWithTag(barriersTag);
        if (barriers != null)
        {
            Debug.Log($"[NetworkedBall] Found shared barriers by tag: {barriers.name}");
            return barriers;
        }

        foreach (string name in barrierNames)
        {
            barriers = GameObject.Find(name);
            if (barriers != null)
            {
                Debug.Log($"[NetworkedBall] Found shared barriers by name '{name}': {barriers.name}");
                return barriers;
            }
        }

        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allObjects)
        {
            foreach (string name in barrierNames)
            {
                if (obj.name.Contains(name))
                {
                    Debug.Log($"[NetworkedBall] Found shared barriers by pattern match: {obj.name}");
                    return obj;
                }
            }
        }

        Debug.LogWarning($"[NetworkedBall] Could not find shared scene barriers");
        return null;
    }

    private GameObject FindBarriersAsChild(Transform parent)
    {
        if (parent == null) return null;

        foreach (string name in barrierNames)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            string childName = child.name.ToLower();
            foreach (string name in barrierNames)
            {
                if (childName.Contains(name.ToLower()))
                {
                    return child.gameObject;
                }
            }

            if (childName.Contains("barrier") || childName.Contains("wall") || childName.Contains("serve"))
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private GameObject FindClosestBarriers()
    {
        GameObject[] allBarriers = GameObject.FindGameObjectsWithTag(barriersTag);

        if (allBarriers.Length == 0)
        {
            List<GameObject> foundBarriers = new List<GameObject>();
            foreach (string name in barrierNames)
            {
                GameObject[] named = GameObject.FindObjectsOfType<GameObject>()
                    .Where(g => g.name.Contains(name))
                    .ToArray();
                foundBarriers.AddRange(named);
            }
            allBarriers = foundBarriers.ToArray();
        }

        if (allBarriers.Length == 0)
        {
            return null;
        }

        GameObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (GameObject barrier in allBarriers)
        {
            float distance = Vector3.Distance(transform.position, barrier.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = barrier;
            }
        }

        return closest;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Spawn ball with E
        if (Input.GetKeyDown(KeyCode.E) && currentBallInstance == null && ballPrefab != null && ballSpawnPoint != null)
        {
            SpawnBallServerRpc(ballSpawnPoint.position, ballSpawnPoint.rotation);
            ServeBallServerRpc();

            barrierController?.RequestDisableBarriers();
            NetworkedGameManager.Instance?.RequestUnlockPickupSpawning();
        }

        // Pause/Resume for testing
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!ballPaused)
                PauseBallServerRpc();
            else
                ResumeBallServerRpc();
        }

        if (Input.GetKeyDown(KeyCode.E) && nearBall && localServing)
        {
            localServing = false;
            lastServeTime = Time.time;

            barrierController?.RequestDisableBarriers();
            ServeBallServerRpc();
        }
    }

    private void FixedUpdate()
    {
        // Server: Periodically sync ball state to all clients
        if (IsServer && currentBallInstance != null)
        {
            Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();

            // Enforce velocity cap every physics frame (server-authoritative)
            if (rb != null && enforceVelocityCapInFixedUpdate)
            {
                ClampBallVelocity(rb);
            }

            if (Time.time - lastPositionSyncTime >= positionSyncInterval)
            {
                lastPositionSyncTime = Time.time;
                
                if (rb != null)
                {
                    SyncBallStateClientRpc(
                        currentBallInstance.transform.position,
                        rb.linearVelocity,
                        Time.time
                    );
                }
            }
        }

        // Clients: Smoothly interpolate to server-authoritative state
        if (!IsServer && currentBallInstance != null && hasReceivedSync)
        {
            Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Smoothly move towards target position
                Vector3 currentPos = currentBallInstance.transform.position;
                Vector3 newPos = Vector3.Lerp(currentPos, targetPosition, positionLerpSpeed);
                
                // Only apply if difference is significant (avoid jitter)
                if (Vector3.Distance(currentPos, targetPosition) > 0.1f)
                {
                    rb.position = newPos;
                }

                // Blend velocity
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, positionLerpSpeed);
            }
        }
    }

    [ClientRpc]
    private void SyncBallStateClientRpc(Vector3 position, Vector3 velocity, float timestamp)
    {
        if (IsServer) return; // Server doesn't need to sync to itself

        targetPosition = position;
        targetVelocity = velocity;
        hasReceivedSync = true;
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
    private void RequestDeactivateServingBarriersServerRpc()
    {
        Debug.Log("[Server] Deactivating serving barriers");
        DeactivateServingBarriersClientRpc();
    }

    [ClientRpc]
    private void DeactivateServingBarriersClientRpc()
    {
        if (servingBarriers == null)
        {
            Debug.LogError("[Client] servingBarriers NULL during deactivation");
            return;
        }

        servingBarriers.SetActive(false);
        Debug.Log("[Client] Serving barriers deactivated");
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnBallServerRpc(Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (currentBallInstance != null)
        {
            Debug.LogWarning($"[Server] Ball already exists, ignoring spawn request from client {rpcParams.Receive.SenderClientId}");
            return;
        }

        Debug.Log($"[Server] Spawning ball at {position} requested by client {rpcParams.Receive.SenderClientId}");

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
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            netObj.Spawn(true);
            currentBallInstance = ball;

            Debug.Log($"[Server] Ball spawned successfully with NetworkObjectId: {netObj.NetworkObjectId}");
            NotifyBallSpawnedClientRpc();
        }
    }

    [ClientRpc]
    private void NotifyBallSpawnedClientRpc()
    {
        StartCoroutine(AssignBallToAllIKs());
    }

    private IEnumerator AssignBallToAllIKs()
    {
        int attempts = 0;
        while (currentBallInstance == null && attempts < 20)
        {
            currentBallInstance = GameObject.FindGameObjectWithTag("Ball");
            if (currentBallInstance != null) break;

            attempts++;
            yield return new WaitForSeconds(0.05f);
        }

        if (currentBallInstance == null)
        {
            Debug.LogError("[NetworkedBall] Failed to find ball after 20 attempts!");
            yield break;
        }

        Transform ballTransform = currentBallInstance.transform;
        Debug.Log($"[NetworkedBall] Ball found at {ballTransform.position}, assigning to IKs...");

        if (localPlayerIK == null)
            localPlayerIK = GetComponent<TwoHandIKController>();

        if (localPlayerIK != null)
        {
            localPlayerIK.AssignBall(ballTransform);
            Debug.Log($"[NetworkedBall] Client {OwnerClientId} assigned ball to LOCAL IK");
        }
        else
        {
            Debug.LogWarning($"[NetworkedBall] Client {OwnerClientId} LOCAL IK not found");
        }

        yield return new WaitForSeconds(0.2f);

        if (remotePlayerIK == null)
            remotePlayerIK = FindRemotePlayerIK();

        if (remotePlayerIK != null)
        {
            remotePlayerIK.AssignBall(ballTransform);
            Debug.Log($"[NetworkedBall] Client {OwnerClientId} assigned ball to REMOTE IK");
        }
        else
        {
            Debug.LogWarning($"[NetworkedBall] Client {OwnerClientId} REMOTE IK not found, starting retry...");
            StartCoroutine(RetryAssignRemoteIK(ballTransform));
        }
    }

    private IEnumerator RetryAssignRemoteIK(Transform ball)
    {
        float timeout = 2f;
        float timer = 0f;

        while (remotePlayerIK == null && timer < timeout)
        {
            remotePlayerIK = FindRemotePlayerIK();
            timer += Time.deltaTime;
            yield return null;
        }

        if (remotePlayerIK != null)
        {
            remotePlayerIK.AssignBall(ball);
            Debug.Log("[RetryAssignRemoteIK] Remote IK assigned successfully");
        }
        else
        {
            Debug.LogError("[RetryAssignRemoteIK] FAILED to find remote IK");
        }
    }

    private TwoHandIKController FindRemotePlayerIK()
    {
        TwoHandIKController[] allIKs = FindObjectsOfType<TwoHandIKController>(true);

        foreach (var ik in allIKs)
        {
            NetworkObject netObj = ik.GetComponentInParent<NetworkObject>();
            if (netObj == null)
                continue;

            if (!netObj.IsOwner)
            {
                Debug.Log($"[NetworkedBall] Found REMOTE IK on client {netObj.OwnerClientId}");
                return ik;
            }
        }

        Debug.LogWarning("[NetworkedBall] Remote IK not found yet");
        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallInstance == null) return;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            Vector3 serveVelocity = new Vector3(0, ogUpForce, 0);
            rb.linearVelocity = serveVelocity;

            // Clamping here isn't really necessary but I'm being heavy handed with this.
            ClampBallVelocity(rb);

            Debug.Log($"[Server] Ball served by client {rpcParams.Receive.SenderClientId} - velocity: {rb.linearVelocity}");
            
            // Immediately sync the serve to all clients
            SyncBallStateClientRpc(currentBallInstance.transform.position, rb.linearVelocity, Time.time);
        }

        NotifyServeCompleteClientRpc();
    }

    [ClientRpc]
    private void NotifyServeCompleteClientRpc()
    {
        localServing = false;
        hitting = true;
        lastServeTime = Time.time;
        NetworkedGameManager.Instance?.RequestUnlockPickupSpawning();
    }

    [ServerRpc(RequireOwnership = false)]
    private void HitBallServerRpc(
        Vector3 direction,
        float force,
        float upwardForce,
        ServerRpcParams rpcParams = default
    )
    {
        if (!IsServer || currentBallInstance == null)
            return;

        // Global server debounce
        if (Time.time - lastGlobalServerHitTime < serverHitDebounce)
        {
            Debug.Log($"[Server] Ignoring hit from client {rpcParams.Receive.SenderClientId} (debounced)");
            return;
        }

        // Validate closest player on server
        if (!IsClosestServerPlayer(rpcParams.Receive.SenderClientId))
        {
            Debug.Log($"[Server] Rejecting hit from client {rpcParams.Receive.SenderClientId} (not closest)");
            return;
        }

        lastGlobalServerHitTime = Time.time;

        Rigidbody rb = currentBallInstance.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.useGravity = true;

        Vector3 finalVelocity = direction.normalized * force + Vector3.up * upwardForce;
        rb.linearVelocity = finalVelocity;

        // Clamp the Ball's velocity. It was doing some WACKY shit before so this method is going to put a stop to it
        ClampBallVelocity(rb);

        // Get the clamped velocity for sync
        Vector3 clampedVelocity = rb.linearVelocity;

        // Immediately sync to all clients
        SyncBallStateClientRpc(currentBallInstance.transform.position, finalVelocity, Time.time);

        Debug.Log($"[Server] Ball hit accepted from client {rpcParams.Receive.SenderClientId}, velocity: {finalVelocity}");

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
        lastHitTime = Time.time;
        Debug.Log($"[NetworkedBall] Player {OwnerClientId} processing hit!");

        // Calculate upforce
        if (35.5f < transform.position.x) upForce = ogUpForce + 2f;
        else upForce = ogUpForce;
        if (-6.25f < transform.position.z || transform.position.z < 6.25f) upForce += 2f;

        // ? DECLARE localClientId ONCE at the top of this section
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        // Check if we have an active spell
        if (spellEffects != null && spellEffects.HasActiveSpell(localClientId))
        {
            string activeSpell = spellEffects.GetActiveSpellName(localClientId);
            Debug.Log($"[NetworkedBall] Player hit ball with active spell: {activeSpell}");
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

        if (IsOwner)
        {
            PlayHitsound(contactPoint);
        }

        // Request server to apply physics
        Vector3 direction = targetPos - transform.position;
        HitBallServerRpc(direction, strength, upForce);

        // Update rally count
        if (IsOwner && NetworkedScoreManager.Instance != null)
        {
            NetworkedScoreManager.Instance.IncrementRallyCountServerRpc();
        }

        // REUSE localClientId (don't redeclare)
        if (spellEffects != null)
        {
            // Just log - OnPlayerHitBall in CollisionTracker will handle spell logic
            Debug.Log($"[NetworkedBall] Client {localClientId} notified spell system of hit");
        }

        // Update collision tracker
        CollisionTrackerBall tracker = other.GetComponent<CollisionTrackerBall>();
        if (tracker != null)
        {
            tracker.LastHitWizard = "Player";
            tracker.hasBounced = false;
        }

        // Hit slowdown effect
        if (IsOwner && spellEffects != null && spellEffects.spellHit)
        {
            StartCoroutine(HitSlowdown());
        }
    }

    private bool IsClosestServerPlayer(ulong hittingClientId)
    {
        if (currentBallInstance == null)
            return false;

        Vector3 ballPos = currentBallInstance.transform.position;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(hittingClientId, out var hittingClient))
            return false;

        if (hittingClient.PlayerObject == null)
            return false;

        float hitterDist = Vector3.Distance(hittingClient.PlayerObject.transform.position, ballPos);

        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client.PlayerObject == null)
                continue;

            if (client.ClientId == hittingClientId)
                continue;

            float otherDist = Vector3.Distance(client.PlayerObject.transform.position, ballPos);

            // Larger tolerance to prevent double-hits from lag
            if (otherDist < hitterDist - 0.5f)
                return false;
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

    /// <summary>
    /// Clamps a rigidbody's velocity to the configured maximum values
    /// </summary>
    private void ClampBallVelocity(Rigidbody rb)
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        bool wasClamped = false;

        // Clamp horizontal velocity (XZ plane)
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
        if (horizontalVelocity.magnitude > maxHorizontalVelocity)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxHorizontalVelocity;
            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;
            wasClamped = true;

            Debug.LogWarning($"[NetworkedBall] Clamped horizontal velocity to {maxHorizontalVelocity} m/s");
        }

        // Clamp vertical velocity
        if (Mathf.Abs(velocity.y) > maxVerticalVelocity)
        {
            velocity.y = Mathf.Sign(velocity.y) * maxVerticalVelocity;
            wasClamped = true;

            Debug.LogWarning($"[NetworkedBall] Clamped vertical velocity to {maxVerticalVelocity} m/s");
        }

        // Clamp total velocity magnitude
        if (velocity.magnitude > maxTotalVelocity)
        {
            velocity = velocity.normalized * maxTotalVelocity;
            wasClamped = true;

            Debug.LogWarning($"[NetworkedBall] Clamped total velocity to {maxTotalVelocity} m/s");
        }

        // Apply clamped velocity
        if (wasClamped)
        {
            rb.linearVelocity = velocity;
            Debug.Log($"[NetworkedBall] Final clamped velocity: {velocity} (magnitude: {velocity.magnitude:F2})");
        }
    }

    public void SetToServingState()
    {
        Debug.Log($"[Barrier RESET] Re-enabled by {gameObject.name} at frame {Time.frameCount}");
        hitting = false;
        localServing = true;

        if (servingBarriers == null)
        {
            Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} barriers lost, attempting to re-find...");
            FindServingBarriers();
        }

        if (ValidateBarriers())
        {
            servingBarriers.SetActive(true);
            barrierController?.RequestEnableBarriers();
            Debug.Log($"[NetworkedBall] Player {OwnerClientId} serving barriers ENABLED");
        }
        else
        {
            Debug.LogError($"[NetworkedBall] Player {OwnerClientId} cannot enable barriers - reference invalid!");
        }

        Debug.Log($"[NetworkedBall] Player {OwnerClientId} set to SERVING state");
    }

    [ContextMenu("Force Find Barriers")]
    public void ForceRefreshBarriers()
    {
        FindServingBarriers();

        if (servingBarriers != null)
        {
            Debug.Log($"[NetworkedBall] ? Barriers refreshed: {servingBarriers.name} (active: {servingBarriers.activeSelf})");
        }
        else
        {
            Debug.LogError($"[NetworkedBall] ? Failed to refresh barriers!");
        }
    }

    private bool ValidateBarriers()
    {
        if (servingBarriers == null)
        {
            Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} barriers reference is null!");
            return false;
        }

        if (servingBarriers.scene.name == null)
        {
            Debug.LogWarning($"[NetworkedBall] Player {OwnerClientId} barriers are not in a scene (destroyed?)");
            return false;
        }

        return true;
    }

    [ContextMenu("Debug Barrier State")]
    public void DebugBarrierState()
    {
        Debug.Log($"=== BARRIER DEBUG FOR PLAYER {OwnerClientId} ===");
        Debug.Log($"  IsOwner: {IsOwner}");
        Debug.Log($"  IsServer: {IsServer}");
        Debug.Log($"  servingBarriers: {(servingBarriers != null ? servingBarriers.name : "NULL")}");
        Debug.Log($"  barriers active: {(servingBarriers != null ? servingBarriers.activeSelf.ToString() : "N/A")}");
        Debug.Log($"  barriers in scene: {(servingBarriers != null && servingBarriers.scene.name != null ? "YES" : "NO")}");
        Debug.Log($"  localServing: {localServing}");
        Debug.Log($"  nearBall: {nearBall}");
        Debug.Log($"=== END BARRIER DEBUG ===");
    }
}