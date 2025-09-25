using UnityEngine;

public class BounceChecker : MonoBehaviour
{
    private CollisionTrackerBall collisionTrackerBall;

    private void Awake()
    {
        // Get reference to the CollisionTrackerBall on this object or its parent
        collisionTrackerBall = GetComponent<CollisionTrackerBall>();
        if (collisionTrackerBall == null)
        {
            collisionTrackerBall = GetComponentInParent<CollisionTrackerBall>();
        }

        if (collisionTrackerBall == null)
        {
            Debug.LogError("BounceChecker: No CollisionTrackerBall found!");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionTrackerBall == null) return;

        // If the ball hits the ground, mark as bounced
        if (collision.collider.CompareTag("Ground"))
        {
            collisionTrackerBall.hasbounced = true;
        }
        else
        {
            collisionTrackerBall.hasbounced = false;
        }
    }
}

