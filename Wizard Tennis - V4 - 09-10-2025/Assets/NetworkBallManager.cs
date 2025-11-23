using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkBallManager : NetworkBehaviour
{
    public static NetworkBallManager Instance;

    [Header("Ball Setup")]
    public GameObject ballPrefab;
    public Transform defaultSpawnPoint;

    [Header("Effects")]
    public GameObject hitParticlePrefab;
    public float defaultUpForce = 11f;
    public float defaultStrength = 25f;

    [Header("Dynamic Ownership")]
    public float ownershipCheckInterval = 0.1f;
    public float ownershipSwitchThreshold = 0.4f;
    private float lastOwnershipCheckTime = 0f;

    [Header("Physics Sync")]
    public float physicsResyncDelay = 0.05f;

    [Header("Server-Side Hit Detection")]
    public float hitDetectionRadius = 2f;
    public LayerMask playerLayerMask;

    private NetworkObject currentBallNetObj;
    private Rigidbody ballRb;
    private CollisionTrackerBall collisionTracker;
    private ulong lastBallOwner = ulong.MaxValue;
    private ulong lastHitPlayerId = ulong.MaxValue;
    private float lastServerHitTime = -999f;
    private float serverHitDebounce = 0.2f;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (currentBallNetObj == null) return;

        if (Time.time - lastOwnershipCheckTime >= ownershipCheckInterval)
        {
            lastOwnershipCheckTime = Time.time;
            ServerCheckBallOwnership();
        }
    }

    private void ServerCheckBallOwnership()
    {
        NetworkObject ballNO = currentBallNetObj;
        if (ballNO == null) return;

        Vector3 ballPos = ballNO.transform.position;

        float closestDist = float.MaxValue;
        ulong closestClientId = ballNO.OwnerClientId;

        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            ulong clientId = kvp.Key;
            var playerObj = kvp.Value.PlayerObject;
            if (playerObj == null) continue;

            float dist = Vector3.Distance(playerObj.transform.position, ballPos);

            if (dist < closestDist)
            {
                closestDist = dist;
                closestClientId = clientId;
            }
        }

        float currentOwnerDist = float.MaxValue;
        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(ballNO.OwnerClientId))
        {
            var currentOwnerObj = NetworkManager.Singleton.ConnectedClients[ballNO.OwnerClientId].PlayerObject;
            if (currentOwnerObj != null)
            {
                currentOwnerDist = Vector3.Distance(currentOwnerObj.transform.position, ballPos);
            }
        }

        if (closestClientId != ballNO.OwnerClientId &&
            closestDist < currentOwnerDist - ownershipSwitchThreshold)
        {
            ballNO.ChangeOwnership(closestClientId);
            lastBallOwner = closestClientId;

            Debug.Log($"[SERVER] Ball ownership switched to client {closestClientId}.");

            // Force a physics state sync after ownership change
            SyncBallPhysicsClientRpc();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Debug.Log("[NetworkBallManager] Server active.");
    }

    public void ServerProcessPlayerHit(ulong hitterId)
    {
        if (currentBallNetObj == null || ballRb == null) return;

        var hitter = NetworkManager.Singleton.ConnectedClients[hitterId].PlayerObject;
        if (hitter == null) return;

        Vector3 dir = hitter.transform.forward;
        Vector3 up = Vector3.up * defaultUpForce;

        ballRb.linearVelocity = dir * defaultStrength + up;

        Debug.Log($"[SERVER] Applied hit from client {hitterId}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSpawnBallServerRpc(Vector3 spawnPos, Quaternion spawnRot, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (currentBallNetObj != null)
        {
            Debug.Log("[NetworkBallManager] Spawn request ignored - ball already exists.");
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("[NetworkBallManager] Missing ballPrefab.");
            return;
        }

        GameObject go = Instantiate(ballPrefab, spawnPos, spawnRot);
        go.tag = "Ball";

        NetworkObject netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[NetworkBallManager] Ball prefab missing NetworkObject.");
            Destroy(go);
            return;
        }

        ballRb = go.GetComponent<Rigidbody>();
        if (ballRb != null)
        {
            ballRb.useGravity = false;
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // CRITICAL: Disable kinematic mode on spawn - let physics own it
            ballRb.isKinematic = false;
        }

        netObj.Spawn(true);
        currentBallNetObj = netObj;
        collisionTracker = go.GetComponent<CollisionTrackerBall>();
        lastBallOwner = netObj.OwnerClientId;

        Debug.Log($"[NetworkBallManager] Ball spawned at {spawnPos}");

        NotifyClientsBallSpawnedClientRpc();
    }

    [ClientRpc]
    private void NotifyClientsBallSpawnedClientRpc(ClientRpcParams rpcParams = default)
    {
        // Clients can reinitialize local references
    }

    [ClientRpc]
    private void SyncBallPhysicsClientRpc(ClientRpcParams rpcParams = default)
    {
        // Force clients to re-fetch the ball's physics state from network
        // This helps local physics catch up after ownership transfer
        if (currentBallNetObj != null && ballRb != null)
        {
            // Just reading the NetworkObject's transform updates local physics
            ballRb.linearVelocity = currentBallNetObj.GetComponent<Rigidbody>().linearVelocity;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ServeBallServerRpc(float upF, float force, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallNetObj == null) return;
        if (ballRb == null) ballRb = currentBallNetObj.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            Debug.LogWarning("[NetworkBallManager] Serve failed - no Rigidbody.");
            return;
        }

        ballRb.useGravity = true;
        ballRb.isKinematic = false; // Ensure it's not kinematic
        Vector3 upVec = new Vector3(0f, upF, 0f);
        if (upVec.sqrMagnitude <= 0.0001f) upVec = Vector3.up * defaultUpForce;
        ballRb.linearVelocity = upVec.normalized * (force / 2f);

        Debug.Log("[NetworkBallManager] Ball served.");
    }

    [ServerRpc(RequireOwnership = false)]
    public void HitBallServerRpc(Vector3 aimPos, float upF, float force, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallNetObj == null) return;

        if (ballRb == null) ballRb = currentBallNetObj.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            Debug.LogWarning("[NetworkBallManager] Hit failed - no Rigidbody.");
            return;
        }

        ulong senderId = rpcParams.Receive.SenderClientId;
        NetworkObject playerNetObj = null;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.ContainsKey(senderId))
        {
            playerNetObj = NetworkManager.Singleton.ConnectedClients[senderId].PlayerObject;
        }

        Vector3 sourcePos = playerNetObj != null ? playerNetObj.transform.position : Vector3.zero;
        Vector3 dir = (aimPos - sourcePos);
        if (dir.sqrMagnitude < 0.0001f) dir = playerNetObj != null ? playerNetObj.transform.forward : Vector3.forward;
        dir = dir.normalized;

        ballRb.isKinematic = false; // Ensure physics is active
        ballRb.linearVelocity = dir * force + new Vector3(0f, upF, 0f);

        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = playerNetObj != null ? $"Player_{senderId}" : "Player";
            collisionTracker.hasBounced = false;
        }

        PlayHitEffectsClientRpc(currentBallNetObj.transform.position);

        Debug.Log($"[SERVER] Ball hit by client {senderId}.");
    }

    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 worldPos, ClientRpcParams rpcParams = default)
    {
        if (hitParticlePrefab != null)
        {
            var p = Instantiate(hitParticlePrefab, worldPos, Quaternion.identity);
            var ps = p.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
            Destroy(p, 3f);
        }
    }

    public GameObject GetCurrentBallGameObject()
    {
        return currentBallNetObj != null ? currentBallNetObj.gameObject : null;
    }
}