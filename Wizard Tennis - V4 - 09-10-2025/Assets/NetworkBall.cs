using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
public class NetworkBall : NetworkBehaviour
{
    [Header("Hit Settings")]
    public float strength = 25f;
    public float upForce = 11f;

    [Header("References")]
    public CollisionTrackerBall CollisionTracker;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Server physics authority
        rb.isKinematic = !IsServer;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (IsServer)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
        }
    }

    /// <summary>
    /// Server applies hit physics exactly like offline
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void HitBallServerRpc(Vector3 dir, float force, float upF, ulong hitterId)
    {
        if (!IsServer) return;

        // Clamp current velocity to avoid extreme physics
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 50f);

        // Offline-style additive hit
        Vector3 hitDir = dir; // use raw direction from player
        Vector3 impulse = hitDir + Vector3.up * upF;
        impulse.Normalize();
        impulse *= force;

        rb.AddForce(impulse, ForceMode.VelocityChange);

        if (CollisionTracker != null)
        {
            CollisionTracker.LastHitWizard = hitterId.ToString();
            CollisionTracker.hasBounced = false;
        }

        PlayHitEffectsClientRpc(transform.position);
    }

    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 hitPos)
    {
        // SFX, particles, IK
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (CollisionTracker != null)
            CollisionTracker.hasBounced = true;
    }
}
