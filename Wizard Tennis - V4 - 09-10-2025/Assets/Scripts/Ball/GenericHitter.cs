using UnityEngine;

public class SimpleBallReturner : MonoBehaviour
{
    [Header("Hit Settings")]
    public Transform aimTarget;    // Target to aim the ball at
    public Transform opponent;     // Opponent reference
    public float strength = 25f;   // Forward power
    public float upForce = 11f;    // Vertical lift
    private float ogUpForce;

    [Header("Tracking")]
    private CollisionTrackerBall collisionTracker; // automatically found at runtime

    private void Awake()
    {
        ogUpForce = upForce;

        // Try to auto-assign aimTarget from the Ball if missing
        if (aimTarget == null)
        {
            Ball ball = FindObjectOfType<Ball>();
            if (ball != null)
                aimTarget = ball.aimTarget;
        }

        // Try to auto-assign opponent
        if (opponent == null)
        {
            GameObject oppObj = GameObject.FindWithTag("Opponent");
            if (oppObj != null)
                opponent = oppObj.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        Rigidbody ballRb = other.attachedRigidbody;
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
        upForce = ogUpForce;
        if (transform.position.x > 35.5f) upForce += 2f;
        if (Mathf.Abs(transform.position.z) < 6.25f) upForce += 2f;

        // --- Determine side to aim ---
        float xPos = (opponent != null && opponent.position.x > 0) ? -2f : 2f;
        if (transform.position.z < 5f || Mathf.Abs(transform.position.x) > 5f)
            xPos = 0f;

        Vector3 targetPos = (aimTarget != null)
            ? new Vector3(xPos, aimTarget.position.y, aimTarget.position.z)
            : new Vector3(xPos, 1f, 0f); // fallback

        // --- Calculate final velocity ---
        Vector3 dir = targetPos - other.transform.position;
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
