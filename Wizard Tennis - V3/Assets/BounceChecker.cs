using UnityEngine;

public class BounceChecker : MonoBehaviour
{
    private CollisionTrackerBall collisionTrackerBall;
    private bool justOnce = true;

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

    private void OnTriggerEnter(Collider other)
    {
        if (collisionTrackerBall == null) return;

        // If the ball hits the ground, mark as bounced
        if (other.CompareTag("Ground"))
        {
            if (justOnce)
            {
                collisionTrackerBall.HandleBounceCheck();
                justOnce = false;
            }
        }
        else
        {
            collisionTrackerBall.hasbounced = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (collisionTrackerBall == null) return;

        if (other.CompareTag("Ground"))
        {
            justOnce = true;
        }
    }
}

