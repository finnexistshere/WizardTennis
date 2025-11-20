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

        // Instantiate at the client�s provided spawn point
        GameObject go = Instantiate(ballPrefab, spawnPos, spawnRot);
        go.tag = "Ball";

        NetworkObject netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[NetworkBallManager] Ball prefab missing NetworkObject.");
            Destroy(go);
            return;
        }

        // Configure physics server-side
        ballRb = go.GetComponent<Rigidbody>();
        if (ballRb != null)
        {
            ballRb.useGravity = false;
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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
        // clients can use this callback to assign IK rigs or destroy any ghost objects
        // players' local scripts will find the ball by tag or via NetworkManager's spawned objects
    }

    // ---------- Server: Serve Ball ----------
    // Clients call this when they want to serve. We accept upF/force to allow client-side calculated power.
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
    public void HitBallServerRpc(Vector3 aimPos, float upF, float force, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || currentBallNetObj == null) return;

        if (ballRb == null) ballRb = currentBallNetObj.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            Debug.LogWarning("[NetworkBallManager] Hit failed - no Rigidbody.");
            return;
        }

        // Compute direction using server-side player transform snapshot (we trust the server's transform)
        // Find the player object that issued the RPC (sender)
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

        ballRb.linearVelocity = dir * force + new Vector3(0f, upF, 0f);

        // Update collision tracker
        if (collisionTracker != null)
        {
            collisionTracker.LastHitWizard = playerNetObj != null ? $"Player_{senderId}" : "Player";
            collisionTracker.hasBounced = false;
        }

        // Let clients play particles/sfx at the hit point
        PlayHitEffectsClientRpc(currentBallNetObj.transform.position);

        Debug.Log($"[NetworkBallManager] Ball hit by client {senderId}.");
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

        // Optional: play a global sfx or local sfx via an AudioSource in the scene
        // (players can still play their own localized audio from their Player script)
    }

    // ---------- Utility ----------
    public GameObject GetCurrentBallGameObject()
    {
        return currentBallNetObj != null ? currentBallNetObj.gameObject : null;
    }
}
