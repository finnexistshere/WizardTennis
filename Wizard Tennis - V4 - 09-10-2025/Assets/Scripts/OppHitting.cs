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

    public TennisAI TennisAI;

    public SpellEffects SpellEffects;

    public CollisionTrackerBall CollisionTracker;
    public GameObject Player;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;     // Prevents rapid double-fires on multi-collider entries

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
        Player = GameObject.Find("Player");
    }

    void Update()
    {
        // If ball reference is missing, try to find it
        if (ball == null)
        {
            GameObject ballObj = GameObject.FindWithTag("Ball");
            if (ballObj != null)
            {
                ball = ballObj.transform;
                CollisionTracker = ballObj.GetComponent<CollisionTrackerBall>();
            }
            else
            {
                return; // Exit early if no ball exists yet
            }
        }

        // Move opponent on X axis toward the ball
        targetPosition.x = ball.position.x;

        // Measure actual movement between frames
        float frameMovement = Mathf.Abs(targetPosition.x - transform.position.x);

        // Trigger bounce whenever *any* movement occurs (even very small)
        npcBounceAnimator.isMoving = frameMovement > 0.0001f;

        // Perform movement
        //if (barrier) targetPosition.x *= -1;
        Vector3 testPos = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        if (tether != null)
        {
            if (testPos.x > (tether.transform.position.x + 2f) || testPos.x < (tether.transform.position.x - 2f))
            {
                return;
            }
        }
        transform.position = testPos;
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
                other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0);

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
