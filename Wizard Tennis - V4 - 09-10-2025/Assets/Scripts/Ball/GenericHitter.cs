using System.ComponentModel;
using System.Collections;
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

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;     // Prevents rapid double-fires on multi-collider entries
    private float _lastHitSfxTime = -999f;

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

        // Stole this code from the PlayerHitting.cs script. Should be a clean drop in.
        // Should be.
        ParticleSystem particle = GameObject.FindGameObjectWithTag("Player Hit Particle").GetComponent<ParticleSystem>();
        particle.transform.position = other.transform.position;
        particle.Play();

        //Contact point for audio Spatialization
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitsound(contactPoint);

        // --- Update collision tracker ---
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }
    }

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
}
