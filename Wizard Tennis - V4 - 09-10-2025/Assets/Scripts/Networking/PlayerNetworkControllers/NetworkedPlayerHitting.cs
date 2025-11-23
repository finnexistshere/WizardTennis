using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkedPlayerHitting : NetworkBehaviour
{
    [Header("References")]
    public Transform aimTarget;
    public TwoHandIKController_Opponent OppIKRig;
    public SpellEffects spellEffects;
    public ScoreManager scoreManager;
    public AudioSource audioSource;
    public GameObject opponent;
    public GameObject servingBarriers;

    [Header("Ball Settings")]
    public Transform ballSpawnPoint;
    public GameObject ballPrefab;

    private bool nearBall = false;
    private bool serving = true;
    private bool hitting = true;

    [Header("Force Settings")]
    public float strength = 25f;
    public float ogUpForce = 11f;
    private float upForce;

    [Header("Audio")]
    public AudioClip[] hitSounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;
    private float lastHitSfxTime = -999f;

    private Camera cam;
    private CollisionTrackerBall collisionTracker;

    private void Awake()
    {
        cam = Camera.main;
        upForce = ogUpForce;
    }

    public override void OnNetworkSpawn()
    {
        if (PlayerReferenceRelay.Instance != null)
            PlayerReferenceRelay.Instance.ApplyTo(this);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (NetworkBallManager.Instance != null)
            {
                Vector3 pos = ballSpawnPoint != null ? ballSpawnPoint.position : transform.position;
                Quaternion rot = ballSpawnPoint != null ? ballSpawnPoint.rotation : transform.rotation;
                NetworkBallManager.Instance.SpawnAndServeServerRpc(pos, rot, upForce, strength);
                serving = false;
                if (servingBarriers != null) servingBarriers.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore non-ball objects
        if (other == null || other.tag != "Ball") return;

        nearBall = true;
        if (!hitting || serving) return;

        // Safely adjust upForce
        try
        {
            upForce = ogUpForce;
            if (transform.position.x > 35.5f) upForce += 2f;
            if (transform.position.z > -6.25f && transform.position.z < 6.25f) upForce += 2f;
        }
        catch { upForce = ogUpForce; }

        // Optional: local spell effect
        if (spellEffects != null && spellEffects.plrHitSpell)
            spellEffects.castSpell();

        // Local audio/visual feedback
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitSound(contactPoint);

        // Always tell server: ball hit by this player
        if (NetworkBallManager.Instance != null)
        {
            ulong playerId = IsOwner ? OwnerClientId : NetworkManager.Singleton.LocalClientId;
            Vector3 aimPos = aimTarget != null ? aimTarget.position : transform.position + transform.forward;
            NetworkBallManager.Instance.HitBallServerRpc(aimPos, upForce, strength, playerId);
        }

        // Optional: reset spell effect
        if (spellEffects != null && spellEffects.resetOnPlrHit)
            spellEffects.resetSpellEffect();

        // Collision tracker
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && other.CompareTag("Ball"))
            nearBall = false;
    }

    private void PlayHitSound(Vector3 contactPoint)
    {
        if (audioSource == null || hitSounds.Length == 0) return;
        if (Time.time - lastHitSfxTime < minInterval) return;
        lastHitSfxTime = Time.time;

        int index = (hitSounds.Length == 1) ? 0 : Random.Range(0, hitSounds.Length);
        audioSource.transform.position = contactPoint;
        float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float vol = Mathf.Clamp01(1f + Random.Range(-volumeJitter, volumeJitter));
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(hitSounds[index], vol);
    }
}
