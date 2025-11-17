using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner_Better : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public Transform player1Spawn;
    public Transform player2Spawn;

    [Header("Player Prefab")]
    public GameObject playerPrefab; // Must have NetworkObject and PlayerInput

    private static readonly Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    private int spawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Spawn host immediately
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
        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] Player prefab is not assigned!");
            return;
        }

        Vector3 spawnPos = GetNextSpawnPosition();
        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] Player prefab is missing NetworkObject!");
            Destroy(playerInstance);
            return;
        }

        // Spawn and assign ownership
        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;
    }

    private Vector3 GetNextSpawnPosition()
    {
        Vector3 spawnPos;

        if (spawnIndex == 0 && player1Spawn != null)
            spawnPos = player1Spawn.position;
        else if (player2Spawn != null)
            spawnPos = player2Spawn.position;
        else
            spawnPos = Vector3.zero; // fallback

        spawnIndex = (spawnIndex + 1) % 2;
        return spawnPos;
    }
}
