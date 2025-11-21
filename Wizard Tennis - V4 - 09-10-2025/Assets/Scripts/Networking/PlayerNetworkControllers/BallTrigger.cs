using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class BallTriggerHit : NetworkBehaviour
{
    [Header("Hit Settings")]
    public float autoHitStrength = 25f;
    public float autoHitUpForce = 11f;
    public float minHitInterval = 0.1f; // prevent multiple hits per frame

    private float lastHitTime = -999f;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Only server applies authoritative hits
        if (Time.time - lastHitTime < minHitInterval) return;

        // Only trigger on players
        if (other.CompareTag("Player"))
        {
            lastHitTime = Time.time;
            Vector3 playerPos = other.transform.position;

            // Compute hit direction away from player
            Vector3 hitDir = (transform.position - playerPos).normalized;
            if (hitDir.sqrMagnitude < 0.0001f)
                hitDir = Vector3.forward;

            // Apply velocity to the ball
            if (rb != null)
            {
                rb.linearVelocity = hitDir * autoHitStrength + Vector3.up * autoHitUpForce;
            }

            // Optional: inform NetworkBallManager to update hit tracker & effects
            if (NetworkBallManager.Instance != null && NetworkBallManager.Instance.GetCurrentBallGameObject() == gameObject)
            {
                Vector3 aimPos = transform.position + hitDir; // simple aim position for server
                NetworkBallManager.Instance.HitBallServerRpc(aimPos, autoHitUpForce, autoHitStrength);
            }

            Debug.Log($"[BallTriggerHit] Ball auto-hit triggered by {other.name}");
        }
    }
}
