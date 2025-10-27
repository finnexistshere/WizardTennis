using UnityEngine;

public class CollisionTrackerBall : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;

    [Header("State")]
    public string LastHitWizard = "";
    public bool hasBounced = false;

    private bool canTrigger = true;

    private void Awake()
    {
        // Auto-assign GameManager if not set
        if (gameManager == null)
            gameManager = GameManager.Instance;
    }

    private void OnTriggerEnter(Collider other)
    {
        string tag = other.tag;

        switch (tag)
        {
            case "Player":
            case "Opponent":
                // In case you want to reset bounce or mark last hitter
                // LastHitWizard = tag;
                // hasBounced = false;
                break;

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
                // HandleBounceCheck();
                break;
        }
    }

    private void HandleOutOfBounds()
    {
        if (!hasBounced)
        {
            // Ball didn’t bounce before going out
            if (LastHitWizard == "Player")
                AwardPoint("Opponent", "Opponent Wins! You hit it out!");
            else if (LastHitWizard == "Opponent")
                AwardPoint("Player", "Player Wins! Opponent hit it out!");
        }
        else
        {
            // Ball bounced once before going out
            if (LastHitWizard == "Player")
                AwardPoint("Player", "You Win! Opponent Missed!");
            else if (LastHitWizard == "Opponent")
                AwardPoint("Player", "Opponent messed up, Player wins the point!");
        }
    }

    private void HandleOutOfBoundsSide2()
    {
        if (!hasBounced)
        {
            if (LastHitWizard == "Player")
                AwardPoint("Opponent", "You're really bad at Tennis!");
            else if (LastHitWizard == "Opponent")
                AwardPoint("Player", "Player Wins! Opponent hit it out!");
        }
        else
        {
            if (LastHitWizard == "Player")
                AwardPoint("Opponent", "Maybe pick a different sport champ - Player lose");
            else if (LastHitWizard == "Opponent")
                AwardPoint("Opponent", "Opponent wins! You Missed!");
        }
    }

    private void HandleNetHit()
    {
        if (LastHitWizard == "Player")
            AwardPoint("Opponent", "Try not to aim for the Net - Player lose");
        else if (LastHitWizard == "Opponent")
            AwardPoint("Player", "The AI is stupid and hit the net - Player win");
    }

    public void HandleBounceCheck()
    {
        if (hasBounced)
        {
            // Double bounce = lose point
            if (LastHitWizard == "Player")
                AwardPoint("Opponent", "The AI is stupid and couldn't hit the ball properly");
            else if (LastHitWizard == "Opponent")
                AwardPoint("Opponent", "You lose! Your ball bounced before it went over!");
        }
        else
        {
            hasBounced = true;
        }
    }

    // --- Utility Methods ---
    private void AwardPoint(string winner, string message)
    {
        ScoreManager.Instance.AddPoint(winner);
        gameManager.RoundOver(message);
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
