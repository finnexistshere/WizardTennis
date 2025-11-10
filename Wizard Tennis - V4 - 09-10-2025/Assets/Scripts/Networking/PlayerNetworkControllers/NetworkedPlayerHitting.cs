using System.Collections;
using UnityEngine;
using Unity.Netcode;

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
    public Transform ballSpawnPoint;  // Where the ball will spawn
    public GameObject ballPrefab;      // Prefab for the ball
    private static NetworkedBall currentBall;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;

    private float _lastHitSfxTime = -999f;

    public GameObject Opponent;
    public float xPos;

    public GameObject servingBarriers;

    private Camera cam;

    public Spellcasting spellcasting;
    public CollisionTrackerBall CollisionTracker;
    public int rallyCount = 0;
    public int greenRallyCount = 0;
    public bool green = false;
    public int greenPoints = 0;
    public ScoreManager scoreManager;

    private void Awake()
    {
        cam = Camera.main;
        Opponent = GameObject.Find("Opponent");
        servingBarriers = GameObject.Find("ServingBarriers");
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Only allow input on the owning player
            StartCoroutine(InputCheckRoutine());
        }
    }

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

    [ServerRpc]
    private void SpawnBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (ballPrefab == null || ballSpawnPoint == null) return;

        GameObject ballObj = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
        NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
        netObj.Spawn();

        currentBall = ballObj.GetComponent<NetworkedBall>();
        CollisionTracker = currentBall.GetComponent<CollisionTrackerBall>();

        // Assign to IK rigs on all clients
        AssignBallClientRpc(netObj.NetworkObjectId);
    }

    [ServerRpc]
    private void ServeBallServerRpc(ServerRpcParams rpcParams = default)
    {
        if (currentBall == null) return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        rb.useGravity = true;
        Vector3 dir = new Vector3(0, upForce, 0).normalized * strength / 2;
        rb.velocity = dir;

        serving.Value = false;
        servingBarriers.SetActive(false);
    }

    [ClientRpc]
    private void AssignBallClientRpc(ulong netObjId)
    {
        NetworkObject netObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[netObjId];
        if (netObj != null)
        {
            TwoHandIKController ikController = FindObjectOfType<TwoHandIKController>();
            if (ikController != null)
                ikController.AssignBall(netObj.transform);

            OppIKRig.AssignBall(netObj.transform);
        }
    }

    private void PlayHitsound(Vector3 contactPoint)
    {
        if (audioSource == null || hitsounds.Length == 0) return;
        if (Time.time - _lastHitSfxTime < minInterval) return;

        _lastHitSfxTime = Time.time;
        int index = (hitsounds.Length == 1) ? 0 : Random.Range(0, hitsounds.Length);

        audioSource.transform.position = contactPoint;
        float basePitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float baseVol = Mathf.Clamp01(1f + Random.Range(-volumeJitter, volumeJitter));
        audioSource.pitch = basePitch;
        audioSource.PlayOneShot(hitsounds[index], baseVol);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Only server handles collision logic

        if (other.CompareTag("Ball"))
        {
            nearBall = true;
            if (hitting.Value)
            {
                // Apply hit logic here (same as your original Ball.cs)
                // Calculate upForce, xPos, and direction
                // Update rally count, spell effects, and UI via RPCs if needed
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Ball"))
            nearBall = false;
    }

    public static NetworkedBall GetCurrentBall() => currentBall;
}
