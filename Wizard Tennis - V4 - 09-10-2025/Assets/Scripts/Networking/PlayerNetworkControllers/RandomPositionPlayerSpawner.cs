using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner_Better : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public Transform player1Spawn;
    public Transform player2Spawn;

    [Header("Player Prefab")]
    public GameObject playerPrefab; // Must have NetworkObject and LocalPlayerSetup

    // Public accessor for other scripts
    public static Dictionary<ulong, GameObject> SpawnedPlayers => spawnedPlayers;

    private static readonly Dictionary<ulong, GameObject> spawnedPlayers =
        new Dictionary<ulong, GameObject>();

    // NEW: map client -> spawn transform (public so game manager can read)
    public static readonly Dictionary<ulong, Transform> PlayerSpawnPoints = new Dictionary<ulong, Transform>();

    private int spawnIndex = 0;

    public static NetworkPlayerSpawner_Better Instance;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Spawn host manually, or skip this and rely on callback
        if (!spawnedPlayers.ContainsKey(NetworkManager.Singleton.LocalClientId))
            SpawnPlayer(NetworkManager.Singleton.LocalClientId);
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

        // Skip host if already spawned
        if (clientId == NetworkManager.Singleton.LocalClientId && spawnedPlayers.ContainsKey(clientId))
            return;

        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
        {
            if (playerObj != null && playerObj.TryGetComponent(out NetworkObject netObj))
                netObj.Despawn();

            Destroy(playerObj);
            spawnedPlayers.Remove(clientId);
        }

        // cleanup spawn point record
        if (PlayerSpawnPoints.ContainsKey(clientId))
            PlayerSpawnPoints.Remove(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null) return;

        Transform spawnPoint = GetNextSpawnTransform();
        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation); // rotation set here
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] Player prefab is missing NetworkObject!");
            Destroy(playerInstance);
            return;
        }

        // record spawn transform for this client BEFORE SpawnAsPlayerObject
        PlayerSpawnPoints[clientId] = spawnPoint;

        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;

        Debug.Log($"[Spawner] Spawned player for client {clientId} at {spawnPoint.position}");
    }

    private Transform GetNextSpawnTransform()
    {
        Transform spawnTransform;

        if (spawnIndex == 0 && player1Spawn != null)
            spawnTransform = player1Spawn;
        else
            spawnTransform = player2Spawn;

        spawnIndex = (spawnIndex + 1) % 2;
        return spawnTransform;
    }

    public void RespawnSpecificPlayer(ulong clientId)
    {
        if (!IsServer) return;

        // Get spawn location (already stored in dictionary)
        if (!PlayerSpawnPoints.TryGetValue(clientId, out Transform spawn))
            spawn = GetNextSpawnTransform();

        GameObject newPlayer = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        NetworkObject netObj = newPlayer.GetComponent<NetworkObject>();

        netObj.SpawnAsPlayerObject(clientId, true);

        // Update record
        if (spawnedPlayers.ContainsKey(clientId))
            spawnedPlayers[clientId] = newPlayer;
        else
            spawnedPlayers.Add(clientId, newPlayer);
    }
}
