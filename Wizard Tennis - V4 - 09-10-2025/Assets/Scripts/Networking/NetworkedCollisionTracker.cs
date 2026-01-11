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
    public bool hasBounced = false;

    private GameObject lastHitterGameObject;
    private GameObject previousHitterGameObject;

    private bool canTrigger = true;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = NetworkedGameManager.Instance;

        if (spellEffects == null)
            spellEffects = FindObjectOfType<SpellEffects>();

        if (networkedSpellEffects == null)
            networkedSpellEffects = NetworkedSpellEffects.Instance;
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
                // Check for Mud spell on bounce
                if (networkedSpellEffects != null &&
                    networkedSpellEffects.spellName == "Mud" &&
                    networkedSpellEffects.resetOnBounce)
                {
                    Debug.Log("[Ball] Mud spell triggered on bounce");
                    networkedSpellEffects.resetSpellEffect();
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

        // Convert numeric clientId -> string label used by scoring logic
        LastHitWizard = hitterId == 0 ? "Player_0" : "Player_1";

        Debug.Log($"[Ball-TryMarkLastHitPlayer] LastHitWizard set to: {LastHitWizard}");

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
    // SCORING LOGIC - FIXED TO USE "Player" and "Opponent"
    // ---------------------------------------------------------

    private void HandleOutOfBounds()
    {
        if (!hasBounced)
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
        if (!hasBounced)
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
        if (hasBounced)
        {
            // Double bounce
            if (LastHitWizard == "Player_0")
                AwardPoint("Opponent", "Player 2 Wins! Double bounce by Player 1!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Player", "Player 1 Wins! Double bounce by Player 2!");
        }
        else
        {
            hasBounced = true;
        }
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
        hasBounced = false;
    }

    private void ToggleTriggerGate()
    {
        canTrigger = !canTrigger;
    }

    public void ResetTracking()
    {
        LastHitWizard = "";
        hasBounced = false;
        lastHitterGameObject = null;
        previousHitterGameObject = null;
        canTrigger = true;
    }
}