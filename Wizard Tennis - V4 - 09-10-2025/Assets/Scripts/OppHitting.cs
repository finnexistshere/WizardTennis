using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OppHitting : MonoBehaviour
{
    public Transform ball;
    public GameObject aimTarget;
    public float strength = 25;
    public float ogUpForce = 15;
    private float upForce = 15;
    private Vector3 targetPosition;
    public float speed;

    public float[] xCourtAim = {27.5f, 40.5f};
    public float[] zCourtAim = { -12.5f, 0f, 12.5f };

    public TennisAI TennisAI;

    public SpellEffects SpellEffects;

    public CollisionTrackerBall CollisionTracker;
    public GameObject Player;

    public float xPos;
    public float zPos;

    void Start()
    {
        targetPosition = transform.position; // make the 'targetPosition' equal to the opponent's current position
        aimTarget = GameObject.Find("OppAim");
        upForce = ogUpForce;
    }

    private void Awake()
    {
        Player = GameObject.Find("Player");
    }

    void Update()
    {
        // I updated this to move on the X axis instead so it works with the V2 map - Ed
        targetPosition.x = ball.position.x; // update the targetPosition to the ball's x position so the bot only moves on the x axis
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball")) // if the opponent collides with the ball 
        {
            if (TennisAI.AttemptReturn() == true)
            {

                // Change the position of the aimTarget to a random position on the Player court. This position is divided into sections (upcourt, downcourt, and centre, left, right)
                float aimTargety = aimTarget.transform.position.y;
                Vector3 plrPos = Player.transform.position;
                if (plrPos.x > 0)
                {
                    xPos = -3;
                }
                else
                {
                    xPos = 3;
                }

                if (plrPos.z > 6)
                {
                    zPos = 10;
                }
                else
                {
                    zPos = 3;
                }

                upForce = ogUpForce;
                if (other.transform.position.y < 3 && zPos != 3 /*&& xRand != 1*/)
                {
                    upForce += 1;
                }

                ParticleSystem particle = GameObject.FindGameObjectWithTag("Opponent Hit Particle").GetComponent<ParticleSystem>(); // Plays opponent hit particle

                particle.transform.position = other.transform.position;
                particle.Play();

                if (SpellEffects.oppHitSpell)
                {
                    SpellEffects.castSpell();
                }

                aimTarget.transform.position = new Vector3(xPos, aimTargety, zPos);

                // If you want more detailed comments regarding how the ball hitting works, check the PlayerHitting code
                Vector3 dir = aimTarget.transform.position - transform.position;
                other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0);

                Player.GetComponent<Ball>().rallyCount++;
                
                Player.GetComponent<UIManager>().UpdateRallyCount(Player.GetComponent<Ball>().rallyCount);

                if (SpellEffects.resetOnOppHit)
                {
                    SpellEffects.resetSpellEffect();
                }

                CollisionTracker.LastHitWizard = "Opponent";
                CollisionTracker.hasbounced = false;
            }
        }
    }
    private void OnDrawGizmos()
    {
        // Draw the current target position
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(targetPosition, 0.3f);

        // Only draw court boundaries if we have valid data
        if (xCourtAim != null && zCourtAim != null && xCourtAim.Length >= 2 && zCourtAim.Length >= 2)
        {
            Gizmos.color = Color.green;

            // Determine court bounds
            float minX = Mathf.Min(xCourtAim);
            float maxX = Mathf.Max(xCourtAim);
            float minZ = Mathf.Min(zCourtAim);
            float maxZ = Mathf.Max(zCourtAim);

            // Draw a wireframe rectangle for the court
            Vector3 topLeft = new Vector3(minX, transform.position.y, maxZ);
            Vector3 topRight = new Vector3(maxX, transform.position.y, maxZ);
            Vector3 bottomRight = new Vector3(maxX, transform.position.y, minZ);
            Vector3 bottomLeft = new Vector3(minX, transform.position.y, minZ);

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }
}
