using System;
using UnityEngine;

public class CollisionTrackerBall : MonoBehaviour
{
    public string LastHitWizard = "";
    public GameManager gameManager;
    public bool hasbounced = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            LastHitWizard = "Player";
            hasbounced = false;
        }
        else if (other.CompareTag("Opponent"))
        {
            LastHitWizard = "Opponent";
            hasbounced = false;
        }
        else if (other.CompareTag("OutOfBounds"))
        {
            HandleOutOfBounds();
            hasbounced = false;
        }
        else if (other.CompareTag("OutOfBoundsSide2"))
        {
            HandleOutOfBoundsSide2();
            hasbounced = false;
        }
        else if (other.CompareTag("Net"))
        {
            HandleNetHit();
        }
        else if (other.CompareTag("BounceCheck"))
        {
            //HandleBounceCheck();
        }

    }

    private void HandleOutOfBounds()
    {
        if (!hasbounced)
        {
            if (LastHitWizard == "Player")
            {
                ScoreManager.Instance.AddPoint("Opponent");
                gameManager.RoundOver("Opponent Wins! You hit it out!");
            }
            else if (LastHitWizard == "Opponent")
            {
                ScoreManager.Instance.AddPoint("Player");
                gameManager.RoundOver("Player Wins! Opponent hit it out!");
            }
        }
        else
        {
            if (LastHitWizard == "Player")
            {
                ScoreManager.Instance.AddPoint("Player");
                gameManager.RoundOver("You Win! Opponent Missed!");
            }
            else if (LastHitWizard == "Opponent")
            {
                ScoreManager.Instance.AddPoint("Opponent");
                gameManager.RoundOver("Opponent Wins! You missed!");
            }
        }
    }

    private void HandleOutOfBoundsSide2()
    {
        if (!hasbounced)
        {
            if (LastHitWizard == "Player")
            {
                ScoreManager.Instance.AddPoint("Opponent");
                gameManager.RoundOver("You're really bad at Tennis!");
            }
            else if (LastHitWizard == "Opponent")
            {
                ScoreManager.Instance.AddPoint("Player");
                gameManager.RoundOver("Player Wins! Opponent hit it out!");
            }
        }
        else
        {
            if (LastHitWizard == "Player")
            {
                ScoreManager.Instance.AddPoint("Opponent");
                gameManager.RoundOver("Maybe pick a different sport champ - Player lose");
            }
            else if (LastHitWizard == "Opponent")
            {
                ScoreManager.Instance.AddPoint("Player");
                gameManager.RoundOver("Opponent messed up, Player wins the point!");
            }
        }
    }

    private void HandleNetHit()
    {
        if (LastHitWizard == "Player")
        {
            ScoreManager.Instance.AddPoint("Opponent");
            gameManager.RoundOver("Try not to aim for the Net - Player lose");
        }
        else if (LastHitWizard == "Opponent")
        {
            ScoreManager.Instance.AddPoint("Player");
            gameManager.RoundOver("The AI is stupid and hit the net - Player win");
        }
    }

    public void HandleBounceCheck()
    {
        if (hasbounced)
        {
            if (LastHitWizard == "Player")
            {
                ScoreManager.Instance.AddPoint("Opponent");
                gameManager.RoundOver("The AI is stupid and couldn't hit the ball properly");
            }
            else if (LastHitWizard == "Opponent")
            {
                ScoreManager.Instance.AddPoint("Player");
                gameManager.RoundOver("You lose! Your ball bounced before it went over!");
            }
        }
        else
        {
            hasbounced = true;
        }
    }
}