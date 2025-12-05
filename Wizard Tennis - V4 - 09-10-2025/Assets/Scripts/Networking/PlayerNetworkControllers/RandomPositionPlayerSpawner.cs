using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class NetworkPlayerSpawner_Better : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public Transform player1Spawn;
    public Transform player2Spawn;

    [Header("Player Prefab")]
    public GameObject playerPrefab; // Must have NetworkObject and LocalPlayerSetup

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;
    [SerializeField] private float spawnPositionLockDuration = 1f; // Lock position for this long after spawn

    // Public accessor
    public static Dictionary<ulong, GameObject> SpawnedPlayers => spawnedPlayers;

    private static readonly Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    private static readonly Dictionary<ulong, Transform> playerSpawnPoints = new Dictionary<ulong, Transform>();

    private int nextSpawnIndex = 0;
    private static NetworkPlayerSpawner_Better Instance;
    private bool afterBothStarted = false;

    [SerializeField] private float delayBeforeFiring = 2f; // seconds before first fire
    [SerializeField] private int fireCount = 3; // number of times to fire
    [SerializeField] private float repeatDelay = 1f; // time between fires

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Spawner] Multiple spawner instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (player1Spawn == null || player2Spawn == null)
        {
            Debug.LogError("[Spawner] Spawn points not assigned!");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Register existing connected clients (defensive)
        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            ulong cid = kv.Key;
            var clientObj = kv.Value.PlayerObject;
            if (clientObj != null)
            {
                spawnedPlayers[cid] = clientObj.gameObject;
                AssignSpawnPointIfMissing(cid);
                SendSpawnTransformToOwner(cid);
            }
            else
            {
                SpawnPlayer(cid);
            }
        }

        if (verboseLogging)
            Debug.Log($"[Spawner] Server ready. Connected clients: {NetworkManager.Singleton.ConnectedClientsList.Count}");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // Register existing PlayerObject if present
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                if (!spawnedPlayers.ContainsKey(clientId))
                    spawnedPlayers[clientId] = client.PlayerObject.gameObject;

                AssignSpawnPointIfMissing(clientId);
                SendSpawnTransformToOwner(clientId);
                TryStartAfterBothCoroutine();
                return;
            }
        }

        // Otherwise spawn a new player
        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (verboseLogging)
            Debug.Log($"[Spawner] Client {clientId} disconnected, cleaning up...");

        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
        {
            if (playerObj != null && playerObj.TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
                netObj.Despawn();

            Destroy(playerObj);
            spawnedPlayers.Remove(clientId);
        }

        playerSpawnPoints.Remove(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (!IsServer) return;

        // Defensive: don't spawn if Netcode already created one
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)
            && client.PlayerObject != null)
        {
            if (verboseLogging)
                Debug.Log($"[Spawner] PlayerObject already exists for {clientId}, registering instead of spawning.");
            spawnedPlayers[clientId] = client.PlayerObject.gameObject;
            AssignSpawnPointIfMissing(clientId);
            SendSpawnTransformToOwner(clientId);
            TryStartAfterBothCoroutine();
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] Player prefab not assigned!");
            return;
        }

        Transform spawn = (nextSpawnIndex == 0 && player1Spawn != null) ? player1Spawn :
                          (player2Spawn != null ? player2Spawn : transform);
        nextSpawnIndex = (nextSpawnIndex + 1) % 2;

        Vector3 spawnPos = spawn.position;
        Quaternion spawnRot = spawn.rotation;

        playerSpawnPoints[clientId] = spawn;

        GameObject instance = Instantiate(playerPrefab, spawnPos, spawnRot);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[Spawner] Player prefab missing NetworkObject!");
            Destroy(instance);
            playerSpawnPoints.Remove(clientId);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = instance;

        if (verboseLogging)
            Debug.Log($"[Spawner] Spawned network object for client {clientId} at {spawnPos}");

        SendSpawnTransformToOwner(clientId);
        TryStartAfterBothCoroutine();
    }

    private void AssignSpawnPointIfMissing(ulong clientId)
    {
        if (!playerSpawnPoints.ContainsKey(clientId))
        {
            Transform spawn = (nextSpawnIndex == 0 && player1Spawn != null) ? player1Spawn :
                              (player2Spawn != null ? player2Spawn : transform);
            nextSpawnIndex = (nextSpawnIndex + 1) % 2;
            playerSpawnPoints[clientId] = spawn;
        }
    }

    private void SendSpawnTransformToOwner(ulong clientId)
    {
        if (!playerSpawnPoints.TryGetValue(clientId, out var spawn)) return;

        var rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
        };
        SetLocalPlayerTransformClientRpc(spawn.position, spawn.rotation, rpcParams);
    }

    [ClientRpc]
    private void SetLocalPlayerTransformClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject?.gameObject;
        if (player == null) return;

        StartCoroutine(LockTransformCoroutine(player, position, rotation));
    }

    private IEnumerator LockTransformCoroutine(GameObject player, Vector3 pos, Quaternion rot)
    {
        float lockEnd = Time.time + spawnPositionLockDuration;
        while (Time.time < lockEnd && player != null)
        {
            player.transform.position = pos;
            player.transform.rotation = rot;
            yield return null;
        }
    }

    private void TryStartAfterBothCoroutine()
    {
        if (!afterBothStarted && spawnedPlayers.Count >= 2)
        {
            afterBothStarted = true;
            StartCoroutine(AfterBothSpawnedCoroutine());
        }
    }

    private IEnumerator AfterBothSpawnedCoroutine()
    {
        yield return new WaitForSeconds(delayBeforeFiring);

        for (int i = 0; i < fireCount; i++)
        {
            if (verboseLogging) Debug.Log($"[Spawner] Firing event {i + 1}/{fireCount}");
            ForcePlayersToSpawnPositionsServerRpc();
            yield return new WaitForSeconds(repeatDelay);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ForcePlayersToSpawnPositionsServerRpc()
    {
        foreach (var kv in spawnedPlayers)
        {
            ulong clientId = kv.Key;
            GameObject player = kv.Value;
            if (player == null) continue;

            if (playerSpawnPoints.TryGetValue(clientId, out Transform spawn))
            {
                ForcePlayerToPosition(player, clientId, spawn.position, spawn.rotation);
            }
        }
    }

    private void ForcePlayerToPosition(GameObject player, ulong clientId, Vector3 position, Quaternion rotation)
    {
        // Disable NetworkTransform or CharacterController if present
        var cc = player.GetComponent<CharacterController>();
        var nt = player.GetComponent<NetworkTransform>();

        if (cc != null) cc.enabled = false;
        if (nt != null) nt.enabled = false;

        player.transform.position = position;
        player.transform.rotation = rotation;

        if (cc != null) cc.enabled = true;
        if (nt != null) nt.enabled = true;

        ForcePlayerPositionClientRpc(clientId, position, rotation);
    }

    [ClientRpc]
    private void ForcePlayerPositionClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject?.gameObject;
        if (player == null) return;

        player.transform.position = position;
        player.transform.rotation = rotation;
    }
}
