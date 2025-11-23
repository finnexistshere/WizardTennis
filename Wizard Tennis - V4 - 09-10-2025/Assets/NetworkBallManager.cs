using UnityEngine;
using Unity.Netcode;

public class NetworkBallManager : NetworkBehaviour
{
    public static NetworkBallManager Instance;

    [Header("Ball Setup")]
    public GameObject ballPrefab;
    public Transform defaultSpawnPoint;

    [Header("Effects")]
    public GameObject hitParticlePrefab; // optional, played on clients
    public float defaultUpForce = 11f;
    public float defaultStrength = 25f;

    private NetworkObject currentBallNetObj;
    private Rigidbody ballRb;
    private CollisionTrackerBall collisionTracker;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Debug.Log("[NetworkBallManager] Server active.");
    }

    // ---------- Server: Spawn Ball (called by clients via ServerRpc) ----------
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
            ballRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        netObj.Spawn(true);
        currentBallNetObj = netObj;
        collisionTracker = go.GetComponent<CollisionTrackerBall>();

        Debug.Log($"[NetworkBallManager] Ball spawned at {spawnPos} (from client request).");

        NotifyClientsBallSpawnedClientRpc();
    }

    [ClientRpc]
    private void NotifyClientsBallSpawnedClientRpc(ClientRpcParams rpcParams = default)
    {
        // clients can use this callback to assign IK rigs or destroy ghost objects
    }

    // ---------- Server: Spawn AND Serve (atomic) ----------
    // Use this when the client wants the ball created and immediately served.
    [ServerRpc(RequireOwnership = false)]
    public void SpawnAndServeServerRpc(Vector3 spawnPos, Quaternion spawnRot, float upF, float force, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Spawn if missing
        if (currentBallNetObj == null)
        {
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
                ballRb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            netObj.Spawn(true);
            currentBallNetObj = netObj;
            collisionTracker = go.GetComponent<CollisionTrackerBall>();

            Debug.Log("[NetworkBallManager] SpawnAndServe: spawned new ball.");
        }
        else
        {
            if (ballRb == null) ballRb = currentBallNetObj.GetComponent<Rigidbody>();
            Debug.Log("[NetworkBallManager] SpawnAndServe: found existing ball.");
        }

        // Serve (server authoritative) - apply gravity and velocity
        if (currentBallNetObj == null)
        {
            Debug.LogWarning("[NetworkBallManager] SpawnAndServe: no currentBallNetObj to serve.");
            return;
        }

        if (ballRb == null) ballRb = currentBallNetObj.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            Debug.LogWarning("[NetworkBallManager] SpawnAndServe: missing Rigidbody.");
            return;
        }

        ballRb.useGravity = true;
        Vector3 upVec = new Vector3(0f, upF, 0f);
        if (upVec.sqrMagnitude <= 0.0001f) upVec = Vector3.up * defaultUpForce;

        // Keep same semantics as previous ServeBall: linear upward component scaled by force/2
        ballRb.linearVelocity = upVec.normalized * (force / 2f);

        Debug.Log("[NetworkBallManager] SpawnAndServe: ball served (server authority).");
    }

    // ---------- Server: Serve Ball (existing) ----------
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
        Vector3 upVec = new Vector3(0f, upF, 0f);
        if (upVec.sqrMagnitude <= 0.0001f) upVec = Vector3.up * defaultUpForce;
        ballRb.linearVelocity = upVec.normalized * (force / 2f);

        Debug.Log("[NetworkBallManager] Ball served (server authority).");
    }

    // ---------- Server: Hit Ball (called by players) ----------
    [ServerRpc(RequireOwnership = false)]
    public void HitBallServerRpc(Vector3 aimPos, float upF, float force, ulong playerId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallNetObj == null) return;

        if (ballRb == null) ballRb = currentBallNetObj.GetComponent<Rigidbody>();
        if (ballRb == null) return;

        Vector3 sourcePos = Vector3.zero;
        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(playerId))
        {
            var playerObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
            if (playerObj != null) sourcePos = playerObj.transform.position;
        }

        Vector3 dir = (aimPos - sourcePos).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

        ballRb.linearVelocity = dir * force + Vector3.up * upF;

        // Optional: track hit
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = $"Player_{playerId}";
            collisionTracker.hasBounced = false;
        }

        PlayHitEffectsClientRpc(currentBallNetObj.transform.position);
        Debug.Log($"[NetworkBallManager] Ball hit by client {playerId}.");
    }

    [ClientRpc]
    private void PlayHitEffectsClientRpc(Vector3 worldPos, ClientRpcParams rpcParams = default)
    {
        // Play hit particle and audio on clients for immediate feedback
        if (hitParticlePrefab != null)
        {
            var p = Instantiate(hitParticlePrefab, worldPos, Quaternion.identity);
            var ps = p.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
            Destroy(p, 3f);
        }
    }

    // ---------- Utility ----------
    public GameObject GetCurrentBallGameObject()
    {
        return currentBallNetObj != null ? currentBallNetObj.gameObject : null;
    }
}
