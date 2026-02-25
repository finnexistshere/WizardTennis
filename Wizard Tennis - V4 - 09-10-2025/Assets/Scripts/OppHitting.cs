using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.EventSystems;

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

    public float[] xCourtBounds = { -12.5f, 12.5f };
    public float[] zCourtBounds = { 0f, 27.5f };

    public TennisAI TennisAI;

    public SpellEffects SpellEffects;

    public CollisionTrackerBall CollisionTracker;
    public GameObject Player;
    public string PlayerName = "Player_Singleplayer";

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;     // Prevents rapid double-fires on multi-collider entries

    [Header("Movement Speed")]
    public float minSpeed = 3f;
    public float maxSpeed = 14f;
    public float reactionTime = 0.2f; // seconds of delay before the opponent starts "reacting"

    [Header("Hit Cooldown")]
    public float hitCooldown = 0.5f;
    private float _lastHitTime = -999f;

    private float _lastHitSfxTime = -999f;

    public NPCBounceAnimator npcBounceAnimator;

    public GameObject tether;

    private void PlayHitsound(Vector3 contactPoint)
    {
        if (audioSource == null) return;
        if (hitsounds == null || hitsounds.Length == 0) return;

        // Anti-spam: ignore if we just played a hit very recently
        if (Time.time - _lastHitSfxTime < minInterval) return;
        _lastHitSfxTime = Time.time;

        // Pick a random clip
        int index = (hitsounds.Length == 1) ? 0 : Random.Range(0, hitsounds.Length);

        // Optional: move the source to the contact point for better spatialization
        audioSource.transform.position = contactPoint;

        // Subtle variation for natural feel
        float basePitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float baseVol = 1f + Random.Range(-volumeJitter, volumeJitter);
        baseVol = Mathf.Clamp01(baseVol); // keep in [0,1]

        audioSource.pitch = basePitch;
        audioSource.PlayOneShot(hitsounds[index], baseVol);
    }


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
        Player = GameObject.Find(PlayerName);
    }

    void Update()
    {
        if (ball == null)
        {
            GameObject ballObj = GameObject.FindWithTag("Ball");
            if (ballObj != null)
            {
                ball = ballObj.transform;
                CollisionTracker = ballObj.GetComponent<CollisionTrackerBall>();
            }
            else return;
        }

        Vector3 interceptTarget = GetInterceptPosition();

        float targetX = Mathf.Clamp(interceptTarget.x, xCourtBounds[0], xCourtBounds[1]);
        float targetZ = Mathf.Clamp(interceptTarget.z, zCourtBounds[0], zCourtBounds[1]);

        targetPosition = new Vector3(targetX, transform.position.y, targetZ);

        float frameMovement = Vector3.Distance(transform.position, targetPosition);
        npcBounceAnimator.isMoving = frameMovement > 0.0001f;

        // Dynamically calculate required speed to reach intercept in time
        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        if (ballRb != null)
        {
            float distanceToIntercept = Vector3.Distance(transform.position, targetPosition);
            float timeToIntercept = EstimateTimeToIntercept(ball.position, ballRb.linearVelocity);

            // Subtract reaction time — opponent doesn't instantly respond
            float effectiveTime = Mathf.Max(timeToIntercept - reactionTime, 0.1f);

            // How fast do we need to go to get there in time?
            float requiredSpeed = distanceToIntercept / effectiveTime;

            // Clamp to min/max so it stays beatable but competent
            speed = Mathf.Clamp(requiredSpeed, minSpeed, maxSpeed);
        }

        Vector3 testPos = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (tether != null)
        {
            if (testPos.x > (tether.transform.position.x + 2f) || testPos.x < (tether.transform.position.x - 2f) ||
                testPos.z > (tether.transform.position.z + 2f) || testPos.z < (tether.transform.position.z - 2f))
            {
                return;
            }
        }

        transform.position = testPos;
    }

    private Vector3 GetInterceptPosition()
    {
        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        if (ballRb == null) return transform.position;

        Vector3 ballPos = ball.position;
        Vector3 ballVel = ballRb.linearVelocity;

        // If the ball is already on the opponent's side, move to meet it directly
        // Ball is on opponent's side
        if (ballPos.z >= zCourtBounds[0])
        {
            Vector3 predicted = PredictLanding(ballPos, ballVel);
            predicted.x = Mathf.Clamp(predicted.x, xCourtBounds[0], xCourtBounds[1]);
            predicted.z = Mathf.Clamp(predicted.z, zCourtBounds[0], zCourtBounds[1]);
            return predicted;
        }
        else
        {
            // Retreat to centre of opponent's own court
            float centreX = (xCourtBounds[0] + xCourtBounds[1]) / 2f;
            float centreZ = (zCourtBounds[0] + zCourtBounds[1]) / 2f;
            return new Vector3(centreX, transform.position.y, centreZ);
        }
    }

    private Vector3 PredictLanding(Vector3 pos, Vector3 vel)
    {
        // Step through the ball's trajectory until it reaches roughly hit height or bounces
        float timeStep = 0.05f;
        float gravity = Physics.gravity.y;
        float maxTime = 3f;

        Vector3 simPos = pos;
        Vector3 simVel = vel;

        for (float t = 0; t < maxTime; t += timeStep)
        {
            simVel.y += gravity * timeStep;
            simPos += simVel * timeStep;

            // Stop when ball reaches a hittable height on the opponent's side
            if (simPos.z >= zCourtBounds[0] && simPos.y <= 2.5f)
            {
                return simPos;
            }

            // Safety: if it somehow goes below the ground, stop
            if (simPos.y < 0f)
            {
                return simPos;
            }
        }

        return simPos;
    }

    private float EstimateTimeToIntercept(Vector3 pos, Vector3 vel)
    {
        float timeStep = 0.05f;
        float gravity = Physics.gravity.y;
        float maxTime = 3f;

        Vector3 simPos = pos;
        Vector3 simVel = vel;

        for (float t = 0; t < maxTime; t += timeStep)
        {
            simVel.y += gravity * timeStep;
            simPos += simVel * timeStep;

            if (simPos.z >= zCourtBounds[0] && simPos.y <= 2.5f)
                return t;

            if (simPos.y < 0f)
                return t;
        }

        return maxTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball")) // if the opponent collides with the ball 
        {
            // Ignore the ball briefly after hitting it to prevent double-hits
            if (Time.time - _lastHitTime < hitCooldown) return;

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

                if (other.transform.position.y < 2) upForce += 1;

                ParticleSystem particle = GameObject.FindGameObjectWithTag("Opponent Hit Particle").GetComponent<ParticleSystem>(); // Plays opponent hit particle

                particle.transform.position = other.transform.position;
                particle.Play();

                //Contact point for audio Spatialization
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                PlayHitsound(contactPoint);

                if (SpellEffects.oppHitSpell)
                {
                    SpellEffects.castSpell();
                }

                aimTarget.transform.position = new Vector3(xPos, aimTargety, zPos);

                // If you want more detailed comments regarding how the ball hitting works, check the PlayerHitting code
                Vector3 dir = aimTarget.transform.position - transform.position;
                other.GetComponent<Rigidbody>().linearVelocity = dir.normalized * strength + new Vector3(0, upForce, 0);

                Player.GetComponent<Ball>().rallyCount++;

                Player.GetComponent<UIManager>().UpdateRallyCount(Player.GetComponent<Ball>().rallyCount);

                if (SpellEffects.resetOnOppHit)
                {
                    SpellEffects.resetSpellEffect();
                }

                CollisionTracker.LastHitWizard = "Opponent";
                CollisionTracker.hasBounced = false;
            }
        }

        if (other.CompareTag("Mud")) speed = 4f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Mud")) speed = 5f;
    }

    private void OnDrawGizmos()
    {
        // Draw the current target position
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(targetPosition, 0.3f);

        // Draw opponent movement bounds
        if (xCourtBounds != null && zCourtBounds != null && xCourtBounds.Length >= 2 && zCourtBounds.Length >= 2)
        {
            Gizmos.color = Color.blue;

            float minX = xCourtBounds[0];
            float maxX = xCourtBounds[1];
            float minZ = zCourtBounds[0];
            float maxZ = zCourtBounds[1];

            Vector3 topLeft = new Vector3(minX, transform.position.y, maxZ);
            Vector3 topRight = new Vector3(maxX, transform.position.y, maxZ);
            Vector3 bottomRight = new Vector3(maxX, transform.position.y, minZ);
            Vector3 bottomLeft = new Vector3(minX, transform.position.y, minZ);

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
        // Draw tether bounds
        if (tether != null)
        {
            Gizmos.color = Color.cyan;

            Vector3 tp = tether.transform.position;
            float tx = tp.x, tz = tp.z, ty = transform.position.y;

            Vector3 topLeft = new Vector3(tx - 2f, ty, tz + 2f);
            Vector3 topRight = new Vector3(tx + 2f, ty, tz + 2f);
            Vector3 bottomRight = new Vector3(tx + 2f, ty, tz - 2f);
            Vector3 bottomLeft = new Vector3(tx - 2f, ty, tz - 2f);

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);

            // Cross-hair on the tether point itself
            Gizmos.DrawSphere(new Vector3(tx, ty, tz), 0.2f);
        }

        // Draw the predicted intercept point
        if (ball != null)
        {
            Rigidbody ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                Vector3 predicted = PredictLanding(ball.position, ballRb.linearVelocity);
                predicted.x = Mathf.Clamp(predicted.x, xCourtAim[0], xCourtAim[1]);
                predicted.z = Mathf.Clamp(predicted.z, zCourtAim[0], zCourtAim[zCourtAim.Length - 1]);

                // Draw the predicted landing spot
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(predicted, 0.3f);

                // Draw a line from the opponent to the intercept point
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, predicted);
            }
        }
    }
}
