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

    [Header("Bounce Settings")]
    [Tooltip("Minimum time between bounce registrations (seconds)")]
    public float bounceDebounce = 0.3f;

    private GameObject lastHitterGameObject;
    private GameObject previousHitterGameObject;

    private bool canTrigger = true;

    // Cache for reducing GetComponent calls
    private Rigidbody ballRigidbody;

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
        }

        Debug.Log($"[Ball-Tracker] Spawned on {(IsServer ? "Server" : "Client")}");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only server processes collision scoring & hit tracking
        if (!IsServer) return;

        // --- PLAYER HIT CHECK ---
        if (other.CompareTag("Player") || other.CompareTag("Opponent"))
        {
            TryMarkLastHitPlayer(other);
            return;
        }

        string tag = other.tag;

        switch (tag)
        {
            case "OutOfBounds":
                if (canTrigger)
                {
                    HandleOutOfBounds();
                    ResetBounceTrigger();
                }
                ToggleTriggerGate();
                break;

            case "OutOfBoundsSide2":
                if (canTrigger)
                {
                    HandleOutOfBoundsSide2();
                    ResetBounceTrigger();
                }
                ToggleTriggerGate();
                break;

            case "Net":
                HandleNetHit();
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

        // Reset bounce state when player hits
        if (hasBounced.Value)
        {
            hasBounced.Value = false;
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
            // Double bounce detected
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
        lastHitterGameObject = null;
        previousHitterGameObject = null;
        canTrigger = true;

        Debug.Log("[Ball-Tracker] Tracking reset");
    }

    // Public getter for bounce state (for other scripts to check)
    public bool HasBounced => hasBounced.Value;

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
        Debug.Log($"=== END DEBUG ===");
    }
}