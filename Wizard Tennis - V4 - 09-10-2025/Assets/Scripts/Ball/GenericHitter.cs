using System.ComponentModel;
using System;
using UnityEngine;

public class SimpleBallReturner : MonoBehaviour
{
    [Header("Hit Settings")]
    public Transform aimTarget;    // Target to aim the ball at
    public Transform opponent;     // Opponent reference
    public float strength = 15f;   // Forward power
    public float ogUpForce = 5f;    // Vertical lift
    private float upForce = 5f;

    [Header("Tracking")]
    private CollisionTrackerBall collisionTracker; // automatically found at runtime

    private void Awake()
    {
        upForce = ogUpForce;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        Rigidbody ballRb = other.GetComponent<Rigidbody>();
        if (ballRb == null) return;

        // --- Find the CollisionTracker dynamically on the ball ---
        if (collisionTracker == null)
        {
            collisionTracker = other.GetComponent<CollisionTrackerBall>();
            if (collisionTracker == null)
            {
                Debug.LogWarning("Ball has no CollisionTrackerBall component!");
            }
        }

        // --- Adjust upForce dynamically ---
        if (35.5 < transform.position.x) upForce = ogUpForce + 2;
        else upForce = ogUpForce;

        if (-6.25 < transform.position.z || transform.position.z < 6.25) upForce += 2;

        if (other.transform.position.y < 3)
        {
            upForce += 1;
        }

        // --- Determine side to aim ---
        float xPos = (opponent != null && opponent.position.x > 0) ? -2f : 2f;
        if (transform.position.z < 5f || Mathf.Abs(transform.position.x) > 5f)
            xPos = 0f;

        Vector3 targetPos = (aimTarget != null)
            ? new Vector3(xPos, aimTarget.position.y, aimTarget.position.z)
            : new Vector3(xPos, 1f, 0f); // fallback

        // --- Calculate final velocity ---
        Vector3 dir = targetPos - transform.position;
        Vector3 finalVelocity = dir.normalized * strength + Vector3.up * upForce;

        // --- Apply hit ---
        ballRb.linearVelocity = finalVelocity;

        // --- Update collision tracker ---
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }
    }
}
