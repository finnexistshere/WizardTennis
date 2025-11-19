using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner_Better : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public Transform player1Spawn;
    public Transform player2Spawn;

    [Header("Player Prefab")]
    public GameObject playerPrefab; // Must have NetworkObject, NetworkTransform, and Rigidbody

    private static readonly Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Spawn host manually
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
        if (spawnedPlayers.ContainsKey(clientId)) return;

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
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null) return;

        Transform spawnPoint = GetSpawnTransformForClient(clientId);

        // Instantiate on server at spawn position
        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[Spawner] Player prefab missing NetworkObject!");
            Destroy(playerInstance);
            return;
        }

        // Make Rigidbody kinematic initially to prevent falling on client
        if (playerInstance.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;

        // Let client unfreeze Rigidbody after first position sync
        playerInstance.AddComponent<ClientSyncUnfreeze>();

        Debug.Log($"[Spawner] Spawned player for client {clientId} at {spawnPoint.position}");
    }

    private Transform GetSpawnTransformForClient(ulong clientId)
    {
        return (clientId % 2 == 0 && player1Spawn != null) ? player1Spawn : player2Spawn;
    }
}
