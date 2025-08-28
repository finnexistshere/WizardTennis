using System;
using UnityEngine;

public class CollisionTrackerBall : MonoBehaviour
{
    public string LastHitWizard = "";
    public GameManager gameManager;
    public bool hasbounced = false;

    private void OnTriggerEnter(Collider other)
    {
        // Track who last hit the ball, and if the ball bounced
        if (other.CompareTag("Player"))
        {
            LastHitWizard = "Player";
            hasbounced = false;
        }
        if (other.CompareTag("Ground"))
        {
            hasbounced = true;
            return;
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
        // This is supposed to check if the ball has bounced before it went over, as of right now it's not working, presumably because of how I'm marking hasbounced. I'll fix it later.
 //     if (!hasbounced)
 //     {
  //        if (other.CompareTag("BounceCheck"))
  //        {
  //            HandleBounceCheck();
   //       }
   //   }
        hasbounced = false;
    }

    // this has got to be the worst code I've ever written
    private void HandleOutOfBounds()
    {
        if (hasbounced == false)
        {

            if (gameManager == null)
                gameManager = GameManager.Instance;

            if (LastHitWizard == "Player")
            {
                gameManager.GameOver("Opponent Wins! You hit it out!");
            }
            else if (LastHitWizard == "Opponent")
            {
                gameManager.GameOver("Player Wins! Opponent hit it out!");
            }
            else
            {
                gameManager.GameOver("Game Over: Unknown State!");
            }
        }
        if (hasbounced == true)
        {

            if (gameManager == null)
                gameManager = GameManager.Instance;

            if (LastHitWizard == "Player")
            {
                gameManager.GameOver("You Win! Opponent Missed!");
            }
            else if (LastHitWizard == "Opponent")
            {
                gameManager.GameOver("How");
            }
            else
            {
                gameManager.GameOver("Game Over: Unknown State!");
            }
        }
    }

    private void HandleOutOfBoundsSide2()
    {
        if (hasbounced == false)
        {
            if (gameManager == null)
                gameManager = GameManager.Instance;

            if (LastHitWizard == "Player")
            {
                gameManager.GameOver("You're really bad at Tennis");
            }
            else if (LastHitWizard == "Opponent")
            {
                gameManager.GameOver("Player Wins! Opponent hit it out!");
            }
            else
            {
                gameManager.GameOver("Game Over: Unknown State!");
            }
        }
        if (hasbounced == true)
        {
            if (gameManager == null)
                gameManager = GameManager.Instance;

            if (LastHitWizard == "Player")
            {
                gameManager.GameOver("Maybe pick a different sport champ");
            }
            else if (LastHitWizard == "Opponent")
            {
                gameManager.GameOver("Opponent Wins! You missed!");
            }
            else
            {
                gameManager.GameOver("Game Over: Unknown State!");
            }
        }
    }
    private void HandleNetHit()
    {
        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (LastHitWizard == "Player")
        {
            gameManager.GameOver("Try not to aim for the Net");
        }
        else if (LastHitWizard == "Opponent")
        {
            gameManager.GameOver("The Ai is stupid and hit the net");
        }
        else
        {
            gameManager.GameOver("Game Over: Unknown State!");
        }
    }

    private void HandleBounceCheck()
    {
        if (gameManager == null)
            gameManager = GameManager.Instance;

        if (LastHitWizard == "Player")
        {
            gameManager.GameOver("You lose! Your ball bounced before it went over!");
        }
        else if (LastHitWizard == "Opponent")
        {
            gameManager.GameOver("The AI is stupid and couldn't hit the ball properly");
        }
        else
        {
            gameManager.GameOver("Game Over: Unknown State!");
        }
    }
}
