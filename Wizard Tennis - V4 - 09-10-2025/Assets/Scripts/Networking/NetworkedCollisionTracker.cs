using UnityEngine;
using Unity.Netcode;

public class NetworkedCollisionTrackerBall : NetworkBehaviour
{
    [Header("References")]
    public NetworkedGameManager gameManager;
    public SpellEffects spellEffects;
    public NetworkedSpellEffects networkedSpellEffects;

    [Header("State")]
    public string LastHitWizard = "";

    // Network variable for bounce tracking - synced across all clients
    private NetworkVariable<bool> hasBounced = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Network variable for last hit timestamp
    private NetworkVariable<float> lastBounceTime = new NetworkVariable<float>(
        -999f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Network variable for spawn time (for grace period)
    private NetworkVariable<float> ballSpawnTime = new NetworkVariable<float>(
        -999f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Network variable for last player hit time
    private NetworkVariable<float> lastPlayerHitTime = new NetworkVariable<float>(
        -999f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Bounce Settings")]
    [Tooltip("Minimum time between bounce registrations (seconds)")]
    public float bounceDebounce = 0.4f;

    [Tooltip("Grace period after ball spawn before collisions are processed (seconds)")]
    public float spawnGracePeriod = 0.5f;

    [Tooltip("Grace period after player hit before out-of-bounds is processed (seconds)")]
    public float hitGracePeriod = 0.2f;

    [Tooltip("Minimum time between scoring triggers (prevents rapid double-scoring)")]
    public float scoringCooldown = 0.5f;

    private float lastScoringTime = -999f;

    private GameObject lastHitterGameObject;
    private GameObject previousHitterGameObject;

    private bool canTrigger = true;

    // Cache for reducing GetComponent calls
    private Rigidbody ballRigidbody;

    // Track collision history to prevent duplicate processing
    private string lastProcessedCollision = "";
    private float lastCollisionTime = -999f;
    private const float collisionDebounce = 0.15f;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = NetworkedGameManager.Instance;

        if (spellEffects == null)
            spellEffects = FindObjectOfType<SpellEffects>();

        if (networkedSpellEffects == null)
            networkedSpellEffects = NetworkedSpellEffects.Instance;

        ballRigidbody = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Reset state when spawned
        if (IsServer)
        {
            hasBounced.Value = false;
            lastBounceTime.Value = -999f;
            ballSpawnTime.Value = Time.time;
            lastPlayerHitTime.Value = -999f;
            lastScoringTime = -999f;

            Debug.Log($"[Ball-Tracker] Ball spawned at time {Time.time:F3}, grace period active until {Time.time + spawnGracePeriod:F3}");
        }

        Debug.Log($"[Ball-Tracker] Spawned on {(IsServer ? "Server" : "Client")}");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only server processes collision scoring & hit tracking
        if (!IsServer) return;

        // CRITICAL: Grace period after spawn to prevent false positives
        if (Time.time - ballSpawnTime.Value < spawnGracePeriod)
        {
            Debug.Log($"[Ball-Tracker] Ignoring collision during spawn grace period ({Time.time - ballSpawnTime.Value:F3}s < {spawnGracePeriod}s)");
            return;
        }

        // --- PLAYER HIT CHECK ---
        if (other.CompareTag("Player") || other.CompareTag("Opponent"))
        {
            TryMarkLastHitPlayer(other);
            return;
        }

        string tag = other.tag;

        // Prevent duplicate collision processing
        string collisionKey = $"{tag}_{Time.frameCount}";
        if (collisionKey == lastProcessedCollision && Time.time - lastCollisionTime < collisionDebounce)
        {
            Debug.Log($"[Ball-Tracker] Ignoring duplicate collision: {tag} (debounce active)");
            return;
        }

        lastProcessedCollision = collisionKey;
        lastCollisionTime = Time.time;

        switch (tag)
        {
            case "OutOfBounds":
                if (canTrigger && CanScore())
                {
                    // Grace period after player hit
                    if (Time.time - lastPlayerHitTime.Value < hitGracePeriod)
                    {
                        Debug.Log($"[Ball-Tracker] OutOfBounds ignored - hit grace period active ({Time.time - lastPlayerHitTime.Value:F3}s < {hitGracePeriod}s)");
                        return;
                    }

                    HandleOutOfBounds();
                    ResetBounceTrigger();
                }
                ToggleTriggerGate();
                break;

            case "OutOfBoundsSide2":
                if (canTrigger && CanScore())
                {
                    // Grace period after player hit
                    if (Time.time - lastPlayerHitTime.Value < hitGracePeriod)
                    {
                        Debug.Log($"[Ball-Tracker] OutOfBoundsSide2 ignored - hit grace period active ({Time.time - lastPlayerHitTime.Value:F3}s < {hitGracePeriod}s)");
                        return;
                    }

                    HandleOutOfBoundsSide2();
                    ResetBounceTrigger();
                }
                ToggleTriggerGate();
                break;

            case "Net":
                if (CanScore())
                {
                    HandleNetHit();
                }
                break;

            case "BounceCheck":
                // Server-authoritative bounce handling with debounce
                if (Time.time - lastBounceTime.Value >= bounceDebounce)
                {
                    HandleBounceCheck();

                    // Check for Mud spell on bounce
                    if (networkedSpellEffects != null &&
                        networkedSpellEffects.spellName == "Mud" &&
                        networkedSpellEffects.resetOnBounce)
                    {
                        Debug.Log("[Ball] Mud spell triggered on bounce");
                        networkedSpellEffects.resetSpellEffect();
                    }
                }
                else
                {
                    Debug.Log($"[Ball-Tracker] Bounce ignored - debounce active (time since last: {Time.time - lastBounceTime.Value:F3}s)");
                }
                break;
        }
    }

    /// <summary>
    /// Check if enough time has passed since last scoring event
    /// </summary>
    private bool CanScore()
    {
        if (Time.time - lastScoringTime < scoringCooldown)
        {
            Debug.Log($"[Ball-Tracker] Scoring blocked by cooldown ({Time.time - lastScoringTime:F3}s < {scoringCooldown}s)");
            return false;
        }
        return true;
    }

    private void TryMarkLastHitPlayer(Collider other)
    {
        Debug.Log($"[Ball-TryMarkLastHitPlayer] === START === Collider: {other.name}, Tag: {other.tag}");

        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[Ball-TryMarkLastHitPlayer] FAILED - No NetworkObject on {other.name} or parent!");
            return;
        }

        ulong hitterId = netObj.OwnerClientId;
        GameObject hitterObject = netObj.gameObject;

        Debug.Log($"[Ball-TryMarkLastHitPlayer] Found NetworkObject: {hitterObject.name}, ClientId: {hitterId}");

        // Track previous hitter for spell effects
        previousHitterGameObject = lastHitterGameObject;
        lastHitterGameObject = hitterObject;

        Debug.Log($"[Ball-TryMarkLastHitPlayer] Previous: {previousHitterGameObject?.name ?? "NULL"}, Current: {lastHitterGameObject?.name}");

        // Convert numeric clientId ? string label used by scoring logic
        LastHitWizard = hitterId == 0 ? "Player_0" : "Player_1";

        Debug.Log($"[Ball-TryMarkLastHitPlayer] LastHitWizard set to: {LastHitWizard}");

        // Update last player hit time (for grace period)
        lastPlayerHitTime.Value = Time.time;
        Debug.Log($"[Ball-TryMarkLastHitPlayer] Last player hit time updated to {Time.time:F3}");

        // Reset bounce state when player hits
        if (hasBounced.Value)
        {
            hasBounced.Value = false;
            lastBounceTime.Value = -999f;
            Debug.Log("[Ball-TryMarkLastHitPlayer] Bounce state reset on player hit");
        }

        // Notify SpellEffects about the hit
        if (networkedSpellEffects == null)
        {
            Debug.LogError("[Ball-TryMarkLastHitPlayer] networkedSpellEffects is NULL!");
            return;
        }

        Debug.Log($"[Ball-TryMarkLastHitPlayer] networkedSpellEffects found, active spell: '{networkedSpellEffects.spellName}'");

        if (previousHitterGameObject != null)
        {
            Debug.Log($"[Ball-TryMarkLastHitPlayer] Calling OnPlayerHitBall(hitter={hitterObject.name}, owner={previousHitterGameObject.name})");
            networkedSpellEffects.OnPlayerHitBall(hitterObject, previousHitterGameObject);
        }
        else
        {
            Debug.Log($"[Ball-TryMarkLastHitPlayer] First hit by {hitterObject.name} - no previous hitter");
            networkedSpellEffects.OnPlayerHitBall(hitterObject, null);
        }

        Debug.Log($"[Ball-TryMarkLastHitPlayer] === END ===");
    }

    // ---------------------------------------------------------
    // SCORING LOGIC
    // ---------------------------------------------------------

    private void HandleOutOfBounds()
    {
        if (!hasBounced.Value)
        {
            // Ball went out without bouncing
            if (LastHitWizard == "Player_0")
                AwardPoint("Opponent", "Player 2 Wins! Player 1 missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Player", "Player 1 Wins! Player 2 hit it out!");
        }
        else
        {
            // Ball bounced then went out
            if (LastHitWizard == "Player_0")
                AwardPoint("Opponent", "Player 2 Wins! Player 1 Missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Player", "Player 1 wins! Player 2 messed up!");
        }
    }

    private void HandleOutOfBoundsSide2()
    {
        if (!hasBounced.Value)
        {
            // Ball went out without bouncing
            if (LastHitWizard == "Player_0")
                AwardPoint("Player", "Player 1 Wins! Player 2 missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Opponent", "Player 2 Wins! Player 1 missed!");
        }
        else
        {
            // Ball bounced then went out
            if (LastHitWizard == "Player_0")
                AwardPoint("Player", "Player 1 wins! Player 2 Missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Opponent", "Player 2 wins! Player 1 Missed!");
        }
    }

    private void HandleNetHit()
    {
        if (LastHitWizard == "Player_0")
            AwardPoint("Opponent", "Player 2 Wins! Player 1 hit the net!");
        else if (LastHitWizard == "Player_1")
            AwardPoint("Player", "Player 1 Wins! Player 2 hit the net!");
    }

    public void HandleBounceCheck()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Ball-Tracker] HandleBounceCheck called on client - should only run on server!");
            return;
        }

        if (hasBounced.Value)
        {
            // Double bounce detected - check if we can score
            if (!CanScore())
            {
                Debug.Log("[Ball-Tracker] Double bounce detected but scoring cooldown active");
                return;
            }

            Debug.Log($"[Ball-Tracker] DOUBLE BOUNCE detected! LastHitWizard: {LastHitWizard}");

            if (LastHitWizard == "Player_0")
                AwardPoint("Opponent", "Player 2 Wins! Double bounce by Player 1!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Player", "Player 1 Wins! Double bounce by Player 2!");
        }
        else
        {
            // First bounce - mark it
            hasBounced.Value = true;
            lastBounceTime.Value = Time.time;

            Debug.Log($"[Ball-Tracker] First bounce registered at time {Time.time:F3}");

            // Sync bounce state to all clients
            NotifyBounceClientRpc();
        }
    }

    [ClientRpc]
    private void NotifyBounceClientRpc()
    {
        Debug.Log($"[Ball-Tracker-Client] Bounce notification received - hasBounced: {hasBounced.Value}");
    }

    private void AwardPoint(string winner, string message)
    {
        if (!IsServer) return;

        // Final safety check - prevent rapid scoring
        if (Time.time - lastScoringTime < scoringCooldown)
        {
            Debug.LogWarning($"[Ball-Tracker] AwardPoint blocked by cooldown ({Time.time - lastScoringTime:F3}s < {scoringCooldown}s)");
            return;
        }

        lastScoringTime = Time.time;

        Debug.Log($"[NetworkedCollisionTrackerBall] AwardPoint called - winner: '{winner}', message: '{message}'");

        if (spellEffects != null)
            spellEffects.ForceResetSpellExplanation();

        if (NetworkedScoreManager.Instance != null)
        {
            Debug.Log($"[NetworkedCollisionTrackerBall] Calling ScoreManager.AddPointServerRpc('{winner}')");
            NetworkedScoreManager.Instance.AddPointServerRpc(winner);
        }
        else
        {
            Debug.LogError("[NetworkedCollisionTrackerBall] NetworkedScoreManager.Instance is NULL!");
        }

        if (gameManager != null)
        {
            gameManager.GameOverRound(message);
        }
        else
        {
            Debug.LogError("[NetworkedCollisionTrackerBall] gameManager is NULL!");
        }

        Debug.Log($"[NetworkedCollisionTrackerBall] Point awarded to {winner}: {message}");
    }

    private void ResetBounceTrigger()
    {
        if (IsServer)
        {
            hasBounced.Value = false;
            lastBounceTime.Value = -999f;
        }
    }

    private void ToggleTriggerGate()
    {
        canTrigger = !canTrigger;
    }

    public void ResetTracking()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Ball-Tracker] ResetTracking called on client - should only run on server!");
            return;
        }

        LastHitWizard = "";
        hasBounced.Value = false;
        lastBounceTime.Value = -999f;
        lastPlayerHitTime.Value = -999f;
        ballSpawnTime.Value = Time.time; // Reset spawn time for new grace period
        lastScoringTime = -999f;
        lastHitterGameObject = null;
        previousHitterGameObject = null;
        canTrigger = true;
        lastProcessedCollision = "";
        lastCollisionTime = -999f;

        Debug.Log($"[Ball-Tracker] Tracking reset - new spawn time: {Time.time:F3}, grace period until {Time.time + spawnGracePeriod:F3}");
    }

    // Public getter for bounce state (for other scripts to check)
    public bool HasBounced => hasBounced.Value;

    /// <summary>
    /// Check if ball is currently in spawn grace period
    /// </summary>
    public bool InSpawnGracePeriod => Time.time - ballSpawnTime.Value < spawnGracePeriod;

    /// <summary>
    /// Check if ball is currently in hit grace period
    /// </summary>
    public bool InHitGracePeriod => Time.time - lastPlayerHitTime.Value < hitGracePeriod;

    // Debug info
    [ContextMenu("Debug Bounce State")]
    private void DebugBounceState()
    {
        Debug.Log($"=== BOUNCE TRACKER DEBUG ===");
        Debug.Log($"  IsServer: {IsServer}");
        Debug.Log($"  hasBounced: {hasBounced.Value}");
        Debug.Log($"  lastBounceTime: {lastBounceTime.Value:F3}");
        Debug.Log($"  Time since last bounce: {Time.time - lastBounceTime.Value:F3}s");
        Debug.Log($"  LastHitWizard: {LastHitWizard}");
        Debug.Log($"  canTrigger: {canTrigger}");
        Debug.Log($"  --- GRACE PERIODS ---");
        Debug.Log($"  ballSpawnTime: {ballSpawnTime.Value:F3}");
        Debug.Log($"  Time since spawn: {Time.time - ballSpawnTime.Value:F3}s");
        Debug.Log($"  In spawn grace: {InSpawnGracePeriod}");
        Debug.Log($"  lastPlayerHitTime: {lastPlayerHitTime.Value:F3}");
        Debug.Log($"  Time since hit: {Time.time - lastPlayerHitTime.Value:F3}s");
        Debug.Log($"  In hit grace: {InHitGracePeriod}");
        Debug.Log($"  --- COOLDOWNS ---");
        Debug.Log($"  lastScoringTime: {lastScoringTime:F3}");
        Debug.Log($"  Time since last score: {Time.time - lastScoringTime:F3}s");
        Debug.Log($"  Can score: {CanScore()}");
        Debug.Log($"=== END DEBUG ===");
    }

    [ContextMenu("Force Reset Grace Periods")]
    private void ForceResetGracePeriods()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Ball-Tracker] Must be server to reset grace periods");
            return;
        }

        ballSpawnTime.Value = -999f;
        lastPlayerHitTime.Value = -999f;
        lastScoringTime = -999f;

        Debug.Log("[Ball-Tracker] All grace periods and cooldowns force-reset");
    }
}