using UnityEngine;
using Unity.Netcode;

public class BounceChecker : MonoBehaviour
{
    private CollisionTrackerBall localTracker;
    private NetworkedCollisionTrackerBall networkTracker;

    private bool justOnce = true;

    private void Awake()
    {
        // Try to find the offline tracker
        localTracker = GetComponent<CollisionTrackerBall>();
        if (localTracker == null)
            localTracker = GetComponentInParent<CollisionTrackerBall>();

        // Try to find the networked tracker
        networkTracker = GetComponent<NetworkedCollisionTrackerBall>();
        if (networkTracker == null)
            networkTracker = GetComponentInParent<NetworkedCollisionTrackerBall>();

        if (localTracker == null && networkTracker == null)
        {
            Debug.LogError("BounceChecker: No CollisionTrackerBall or NetworkedCollisionTrackerBall found!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detect ground bounce
        if (!other.CompareTag("Ground"))
            return;

        if (!justOnce)
            return;

        justOnce = false;

        // ---- NON-NETWORKED BALL ----
        if (localTracker != null)
        {
            localTracker.HandleBounceCheck();
            return;
        }

        // ---- NETWORKED BALL ----  
        if (networkTracker != null)
        {
            // Only the server should process bounce rules
            if (networkTracker.IsServer)
            {
                networkTracker.HandleBounceCheck();
            }
            return;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ground"))
        {
            justOnce = true;
        }
    }
}
