using UnityEngine;
using Unity.Netcode;

public class NetworkedCollisionTrackerBall : NetworkBehaviour
{
    [Header("References")]
    public NetworkedGameManager gameManager;
    public SpellEffects spellEffects;

    [Header("State")]
    public string LastHitWizard = "";
    public bool hasBounced = false;

    private bool canTrigger = true;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = NetworkedGameManager.Instance;

        if (spellEffects == null)
            spellEffects = FindObjectOfType<SpellEffects>();
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
                // Bounce is now handled by BounceChecker calling HandleBounceCheck()
                break;
        }
    }

    // ---------------------------------------------------------
    // MARK LAST HIT PLAYER BASED ON NETWORK OWNERSHIP
    // ---------------------------------------------------------
    private void TryMarkLastHitPlayer(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null)
        {
            Debug.Log("[Ball] Hit player object but no NetworkObject found!");
            return;
        }

        ulong hitterId = netObj.OwnerClientId;

        // Convert numeric clientId -> string label used by your scoring logic
        LastHitWizard = hitterId == 0 ? "Player_0" : "Player_1";

        Debug.Log($"[Ball] LastHitWizard updated ? {LastHitWizard} (OwnerClientId={hitterId})");
    }

    // ---------------------------------------------------------
    // SCORING LOGIC BELOW (unchanged)
    // ---------------------------------------------------------

    private void HandleOutOfBounds()
    {
        if (!hasBounced)
        {
            if (LastHitWizard == "Player_0")
                AwardPoint("Host", "Player 1 Wins! Player 2 missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Host", "Player 1 Wins! Player 2 hit it out!");
        }
        else
        {
            if (LastHitWizard == "Player_0")
                AwardPoint("Host", "Player 1 Wins! Player 2 Missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Host", "Player 2 messed up, Player 1 wins the point!");
        }
    }

    private void HandleOutOfBoundsSide2()
    {
        if (!hasBounced)
        {
            if (LastHitWizard == "Player_0")
                AwardPoint("Client", "Player 2 Wins! Player 1 missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Client", "Player 2 Wins! Player 1 missed!");
        }
        else
        {
            if (LastHitWizard == "Player_0")
                AwardPoint("Client", "Player 2 wins! Player 1 Missed!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Client", "Player 2 wins! Player 1 Missed!");
        }
    }

    private void HandleNetHit()
    {
        if (LastHitWizard == "Player_0")
            AwardPoint("Client", "Player 2 Wins! Player 1 hit the net!");
        else if (LastHitWizard == "Player_1")
            AwardPoint("Host", "Player 1 Wins! Player 2 hit the net!");
    }

    public void HandleBounceCheck()
    {
        if (hasBounced)
        {
            if (LastHitWizard == "Player_0")
                AwardPoint("Client", "Player 2 Wins! Double bounce by Player 1!");
            else if (LastHitWizard == "Player_1")
                AwardPoint("Host", "Player 1 Wins! Double bounce by Player 2!");
        }
        else
        {
            hasBounced = true;
        }
    }

    private void AwardPoint(string winner, string message)
    {
        if (!IsServer) return;

        if (spellEffects != null)
            spellEffects.ForceResetSpellExplanation();

        if (NetworkedScoreManager.Instance != null)
            NetworkedScoreManager.Instance.AddPoint(winner);

        if (gameManager != null)
            gameManager.RoundOver(message);

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
}
