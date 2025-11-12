using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class NetworkedBall : NetworkBehaviour
{
    [Header("Ball Settings")]
    public Transform aimTarget;
    public float strength = 25;
    public float ogUpForce = 11;
    private float upForce = 11;
    public float ballSpeed = 5;

    [Header("State")]
    public NetworkVariable<bool> hitting = new NetworkVariable<bool>(true);
    public NetworkVariable<bool> serving = new NetworkVariable<bool>(true);

    public TwoHandIKController_Opponent OppIKRig;
    private bool nearBall = false;
    public SpellEffects SpellEffects;

    [Header("Spawn Settings")]
    public Transform ballSpawnPoint;
    public GameObject ballPrefab;
    private static NetworkedBall currentBall;

    [Header("Audio")]
    public AudioSource audioSourceComponent;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;

    private float _lastHitSfxTime = -999f;

    public GameObject Opponent;
    public float xPos;
    public GameObject servingBarriers;

    private Camera cam;
    public CollisionTrackerBall CollisionTracker;
    public int rallyCount = 0;
    public int greenRallyCount = 0;
    public bool green = false;
    public int greenPoints = 0;
    public ScoreManager scoreManagerComponent;

    private void Awake()
    {
        cam = Camera.main;
        Opponent = GameObject.Find("Opponent");
        servingBarriers = GameObject.Find("ServingBarriers");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                if (!playerInput.user.valid)
                {
                    InputUser newUser = InputUser.PerformPairingWithDevice(Keyboard.current);
                    newUser.AssociateActionsWithUser(playerInput.actions);
                    newUser.ActivateControlScheme(playerInput.defaultControlScheme);
                }

                playerInput.ActivateInput();
            }

            StartCoroutine(WaitAndApplyRelayReferences());
            StartCoroutine(InputCheckRoutine()); // Start input polling
        }
    }

    private IEnumerator WaitAndApplyRelayReferences()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (PlayerReferenceRelay.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (PlayerReferenceRelay.Instance == null) yield break;

        PlayerReferenceRelay.Instance.ApplyTo(this);
    }

    // --- INPUT HANDLING ---
    private IEnumerator InputCheckRoutine()
    {
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (currentBall == null)
                {
                    SpawnBallServerRpc();
                    nearBall = true;
                }
                else if (nearBall && serving.Value)
                {
                    ServeBallServerRpc();
                }
            }
            yield return null;
        }
    }

    // --- SPAWNING & SERVING ---
    [ServerRpc]
    private void SpawnBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (ballPrefab == null || ballSpawnPoint == null) return;

        GameObject ballObj = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
        netObj.Spawn();

        currentBall = ballObj.GetComponent<NetworkedBall>();
        CollisionTracker = currentBall.GetComponent<CollisionTrackerBall>();

        AssignBallClientRpc(netObj.NetworkObjectId);
    }

    [ServerRpc]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentBall == null) return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        rb.useGravity = true;
        Vector3 dir = new Vector3(0, upForce, 0).normalized * strength / 2;
        rb.linearVelocity = dir;

        serving.Value = false;
        servingBarriers.SetActive(false);
    }

    [ClientRpc]
    private void AssignBallClientRpc(ulong netObjId)
    {
        NetworkObject netObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[netObjId];
        if (netObj == null) return;

        TwoHandIKController ikController = FindObjectOfType<TwoHandIKController>();
        if (ikController != null) ikController.AssignBall(netObj.transform);

        OppIKRig.AssignBall(netObj.transform);
    }

    // --- HIT SOUND ---
    private void PlayHitsound(Vector3 contactPoint)
    {
        if (audioSourceComponent == null || hitsounds.Length == 0) return;
        if (Time.time - _lastHitSfxTime < minInterval) return;

        _lastHitSfxTime = Time.time;
        int index = (hitsounds.Length == 1) ? 0 : Random.Range(0, hitsounds.Length);

        audioSourceComponent.transform.position = contactPoint;
        float basePitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float baseVol = Mathf.Clamp01(1f + Random.Range(-volumeJitter, volumeJitter));
        audioSourceComponent.pitch = basePitch;
        audioSourceComponent.PlayOneShot(hitsounds[index], baseVol);
    }

    // --- BALL HIT LOGIC ---
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Ball")) return;

        nearBall = true;
        if (!hitting.Value) return;

        // --- Original force adjustments ---
        upForce = ogUpForce;
        if (35.5 < transform.position.x) upForce += 2;
        if (-6.25 < transform.position.z || transform.position.z < 6.25) upForce += 2;

        // --- Determine X offset ---
        Vector3 oppPos = Opponent.transform.position;
        xPos = (oppPos.x > 0) ? -2f : 2f;
        if (transform.position.z < 5 || transform.position.x < -5 || transform.position.x > 5) xPos = 0f;

        // --- Spell effects ---
        if (SpellEffects.plrHitSpell) SpellEffects.castSpell();

        aimTarget.transform.position = new Vector3(xPos, aimTarget.transform.position.y, aimTarget.transform.position.z);

        // Particle effect
        ParticleSystem particle = GameObject.FindGameObjectWithTag("Player Hit Particle").GetComponent<ParticleSystem>();
        particle.transform.position = other.transform.position;
        particle.Play();

        // Audio
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        PlayHitsound(contactPoint);

        // Apply velocity if not serving
        if (!serving.Value)
        {
            Vector3 dir = aimTarget.position - transform.position;
            other.GetComponent<Rigidbody>().linearVelocity = dir.normalized * strength + new Vector3(0, upForce, 0);

            // Rally and green points
            rallyCount++;
            if (green)
            {
                greenRallyCount++;
                if (greenRallyCount % 4 == 0)
                {
                    greenPoints++;
                    this.GetComponent<UIManager>().UpdateGreenPoints(greenPoints);
                    scoreManagerComponent.greenPoints = greenPoints;
                }
            }
            this.GetComponent<UIManager>().UpdateRallyCount(rallyCount);
        }

        // Reset spell effect
        if (SpellEffects.resetOnPlrHit) SpellEffects.resetSpellEffect();

        // Update collision tracker
        if (CollisionTracker != null)
        {
            CollisionTracker.LastHitWizard = "Player";
            CollisionTracker.hasBounced = false;
        }

        if (SpellEffects.spellHit)
        {
            StartCoroutine(HitSlowdown());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Ball"))
            nearBall = false;
    }

    private static bool isHitSlowActive = false;
    IEnumerator HitSlowdown()
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

    public static NetworkedBall GetCurrentBall() => currentBall;
}
