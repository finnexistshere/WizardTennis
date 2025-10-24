using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ball : MonoBehaviour
{
    public Transform aimTarget; // point on the opposite side
    public float strength = 25;
    public float ogUpForce = 11;
    private float upForce = 11;
    public float ballSpeed = 5;

    private bool hitting = true;
    public bool serving;

    private bool nearBall = false;

    [Header("Spawn Settings")]
    public Transform ballSpawnPoint;  // Where the ball will spawn
    public GameObject ballPrefab;      // Prefab for the ball

    private static GameObject currentBall;  // Ensures only one ball exists

    public SpellEffects SpellEffects;
    public Spellcasting spellcasting;
    public CollisionTrackerBall CollisionTracker;

    void Start()
    {
        serving = true;
        upForce = ogUpForce;

        // Only try to find the ball if one exists
        if (currentBall == null)
            currentBall = GameObject.FindWithTag("Ball");
    }

    void Update()
    {
        // Press E: spawn ball if none exists
        if (Input.GetKeyDown(KeyCode.E) && currentBall == null && ballPrefab != null && ballSpawnPoint != null)
        {
            currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
            CollisionTracker = currentBall.GetComponent<CollisionTrackerBall>(); // get tracker
            nearBall = true; // immediately allow serving
        }

        // Press E: serve if near ball
        if (Input.GetKeyDown(KeyCode.E) && nearBall && serving)
        {
            if (currentBall != null)
            {
                Rigidbody rb = currentBall.GetComponent<Rigidbody>();
                rb.useGravity = true;
                rb.velocity = new Vector3(0, upForce, 0).normalized * strength / 2;
                serving = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            nearBall = true;

            if (hitting)
            {
                if (SpellEffects.plrHitSpell)
                    SpellEffects.castSpell();

                if (35.5 < transform.position.x) upForce = ogUpForce + 2;
                else upForce = ogUpForce;

                if (-6.25 < transform.position.z || transform.position.z < 6.25) upForce += 2;

                ParticleSystem particle = GameObject.FindGameObjectWithTag("Player Hit Particle").GetComponent<ParticleSystem>();
                particle.transform.position = other.transform.position;
                particle.Play();

                if (!serving)
                {
                    Vector3 dir = aimTarget.position - transform.position;
                    other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0);
                }

                if (SpellEffects.resetOnPlrHit)
                    SpellEffects.resetSpellEffect();

                if (CollisionTracker != null)
                {
                    CollisionTracker.LastHitWizard = "Player";
                    CollisionTracker.hasbounced = false;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball"))
            nearBall = false;
    }

    // Optional: helper to let other scripts safely get the current ball
    public static GameObject GetCurrentBall() => currentBall;
}
