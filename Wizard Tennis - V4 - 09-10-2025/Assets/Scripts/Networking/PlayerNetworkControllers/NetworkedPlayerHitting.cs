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

    private static GameObject currentBall;
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
        // On spawn, pull references from the relay
        if (PlayerReferenceRelay.Instance)
            PlayerReferenceRelay.Instance.ApplyTo(this);

        if (IsOwner)
            Debug.Log($"[NetworkedPlayerHitting] Owner ({OwnerClientId}) initialized references.");
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Spawn a new ball if none exists
        if (Input.GetKeyDown(KeyCode.E) && currentBall == null && ballPrefab && ballSpawnPoint)
        {
            RequestBallSpawnServerRpc();
        }

        // Serve if near the ball
        if (Input.GetKeyDown(KeyCode.E) && nearBall && serving && currentBall != null)
        {
            ServeBallServerRpc();
        }
    }

    [ServerRpc]
    private void RequestBallSpawnServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentBall != null) return;

        currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        currentBall.tag = "Ball";
        currentBall.GetComponent<Rigidbody>().useGravity = false;
        NetworkObject netObj = currentBall.GetComponent<NetworkObject>();
        if (netObj != null) netObj.Spawn(true);

        collisionTracker = currentBall.GetComponent<CollisionTrackerBall>();
        nearBall = true;
        GameManager.Instance.UnlockPickupSpawning();

        // Assign to IK rigs
        var ikController = FindObjectOfType<TwoHandIKController>();
        if (ikController != null)
            ikController.AssignBall(currentBall.transform);
        if (OppIKRig != null)
            OppIKRig.AssignBall(currentBall.transform);

        Debug.Log("[Server] Spawned ball and assigned to both IK rigs.");
    }

    [ServerRpc]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentBall == null) return;
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.linearVelocity = new Vector3(0, upForce, 0).normalized * strength / 2f;
        serving = false;

        if (servingBarriers) servingBarriers.SetActive(false);

        Debug.Log("[Server] Ball served.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner || other.tag != "Ball") return;

        nearBall = true;
        if (!hitting || serving) return;

        if (35.5 < transform.position.x) upForce = ogUpForce + 2;
        else upForce = ogUpForce;
        if (-6.25 < transform.position.z || transform.position.z < 6.25) upForce += 2;

        if (spellEffects != null && spellEffects.plrHitSpell)
            spellEffects.castSpell();

        Vector3 aimTargetPos = aimTarget.position;
        Vector3 oppPos = opponent.transform.position;
        float xPos = oppPos.x > 0 ? -2f : 2f;
        if (transform.position.z < 5 || transform.position.x < -5 || transform.position.x > 5)
            xPos = 0f;

        aimTarget.position = new Vector3(xPos, aimTargetPos.y, aimTargetPos.z);

        // Audio
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitSound(contactPoint);

        // Physics (done on server)
        HitBallServerRpc(aimTarget.position, upForce, strength);

        if (spellEffects != null && spellEffects.resetOnPlrHit)
            spellEffects.resetSpellEffect();

        if (collisionTracker)
        {
            collisionTracker.LastHitWizard = "Player";
            collisionTracker.hasBounced = false;
        }

        if (spellEffects != null && spellEffects.spellHit)
            StartCoroutine(HitSlowdown());
    }

    [ServerRpc]
    private void HitBallServerRpc(Vector3 aimPos, float upF, float force)
    {
        if (currentBall == null) return;
        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        Vector3 dir = (aimPos - transform.position).normalized;
        rb.linearVelocity = dir * force + new Vector3(0, upF, 0);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball"))
            nearBall = false;
    }

    private void PlayHitSound(Vector3 contactPoint)
    {
        if (!audioSource || hitSounds == null || hitSounds.Length == 0) return;
        if (Time.time - lastHitSfxTime < minInterval) return;
        lastHitSfxTime = Time.time;

        int index = (hitSounds.Length == 1) ? 0 : Random.Range(0, hitSounds.Length);
        audioSource.transform.position = contactPoint;
        float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float vol = Mathf.Clamp01(1f + Random.Range(-volumeJitter, volumeJitter));
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(hitSounds[index], vol);
    }

    private static bool isHitSlowActive = false;
    private IEnumerator HitSlowdown()
    {
        if (SpellEffects.isSpellSlowdownActive)
            yield return new WaitUntil(() => SpellEffects.isSpellSlowdownActive == false);

        yield return null;
        if (isHitSlowActive) yield break;
        isHitSlowActive = true;

        float originalFOV = cam.fieldOfView;
        float originalTimeScale = Time.timeScale;

        cam.fieldOfView = 60.5f;
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.08f);

        Time.timeScale = originalTimeScale;
        cam.fieldOfView = originalFOV;
        isHitSlowActive = false;
    }
}
