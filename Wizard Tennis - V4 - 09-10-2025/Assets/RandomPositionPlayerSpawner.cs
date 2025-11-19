using System.Collections.Generic;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner_Better : NetworkBehaviour
{
    [Header("Spawn Settings")]
    public Transform player1Spawn;
    public Transform player2Spawn;

    [Header("Player Prefab")]
    public GameObject playerPrefab;

    private static readonly Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    private int spawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Spawner] OnNetworkSpawn called. IsServer={IsServer}, IsOwner={IsOwner}, OwnerClientId={OwnerClientId}");

        // 1? Server handles spawning
        if (IsServer)
        {
            Debug.Log("[Spawner] Server detected, registering client callbacks.");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            // Spawn host manually
            if (!spawnedPlayers.ContainsKey(NetworkManager.Singleton.LocalClientId))
            {
                Debug.Log("[Spawner] Spawning host player manually.");
                SpawnPlayer(NetworkManager.Singleton.LocalClientId);
            }
        }

        // 2? Local owner setup (camera, input, position)
        if (IsOwner)
        {
            Debug.Log("[Spawner] Local owner detected, moving to spawn point.");
            StartCoroutine(MoveOwnerToSpawn());
        }
    }


    private IEnumerator MoveOwnerToSpawn()
    {
        // Wait until this NetworkObject is fully spawned
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        Transform spawnPoint = (OwnerClientId == 0 ? player1Spawn : player2Spawn);
        if (spawnPoint == null)
        {
            Debug.LogWarning("[Spawner] Spawn point for owner is null!");
            yield break;
        }

        Debug.Log($"[Spawner] Moving owner to spawn point at {spawnPoint.position}");
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        // Move camera if it exists
        var setup = GetComponent<LocalPlayerSetup>();
        if (setup != null && setup.spawnedCamera != null)
        {
            setup.spawnedCamera.transform.position = spawnPoint.position;
            setup.spawnedCamera.transform.rotation = spawnPoint.rotation;
            Debug.Log("[Spawner] Owner camera moved to spawn point.");
        }
    }


    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[Spawner] OnDestroy called but NetworkManager.Singleton is null.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        Debug.Log("[Spawner] Client callbacks removed.");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[Spawner] Client connected: {clientId}");

        if (!IsServer)
        {
            Debug.Log("[Spawner] Not server, ignoring client connected callback.");
            return;
        }

        // Only spawn if this client hasn't been spawned yet
        if (spawnedPlayers.ContainsKey(clientId))
        {
            Debug.Log($"[Spawner] Client {clientId} already spawned, skipping.");
            return;
        }

        Debug.Log($"[Spawner] Spawning player for client {clientId}");
        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[Spawner] Client disconnected: {clientId}");

        if (!IsServer)
        {
            Debug.Log("[Spawner] Not server, ignoring client disconnected callback.");
            return;
        }

        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
        {
            if (playerObj != null)
            {
                if (playerObj.TryGetComponent(out NetworkObject netObj))
                {
                    netObj.Despawn();
                    Debug.Log($"[Spawner] Despawned NetworkObject for client {clientId}");
                }
                else
                {
                    Debug.LogWarning($"[Spawner] Player object for client {clientId} missing NetworkObject!");
                }

                Destroy(playerObj);
                Debug.Log($"[Spawner] Destroyed player GameObject for client {clientId}");
            }

            spawnedPlayers.Remove(clientId);
            Debug.Log($"[Spawner] Removed client {clientId} from spawnedPlayers dictionary.");
        }
        else
        {
            Debug.LogWarning($"[Spawner] No spawned player found for disconnected client {clientId}");
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        Debug.Log($"[Spawner] Attempting to spawn player for client {clientId}.");

        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] Player prefab is null! Cannot spawn player.");
            return;
        }

        Transform spawnPoint = GetNextSpawnTransform();
        if (spawnPoint == null)
        {
            Debug.LogError("[Spawner] Spawn point is null! Cannot spawn player.");
            return;
        }

        Debug.Log($"[Spawner] Using spawn point at {spawnPoint.position}");

        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        if (playerInstance == null)
        {
            Debug.LogError("[Spawner] Failed to instantiate player prefab!");
            return;
        }

        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[Spawner] Instantiated player is missing NetworkObject!");
            Destroy(playerInstance);
            return;
        }

        Debug.Log($"[Spawner] Spawning NetworkObject for client {clientId}");
        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;

        var setup = playerInstance.GetComponent<LocalPlayerSetup>();
        if (setup != null)
        {
            Debug.Log("[Spawner] Found LocalPlayerSetup, client will set its own position at OnNetworkSpawn.");
        }
        else
        {
            Debug.LogWarning("[Spawner] Spawned player has no LocalPlayerSetup component!");
        }

    }

    private Transform GetNextSpawnTransform()
    {
        Transform spawnTransform = null;

        if (spawnIndex == 0 && player1Spawn != null)
        {
            spawnTransform = player1Spawn;
        }
        else if (player2Spawn != null)
        {
            spawnTransform = player2Spawn;
        }
        else
        {
            Debug.LogWarning("[Spawner] Both player spawn points are null!");
        }

        Debug.Log($"[Spawner] Selected spawn point {(spawnIndex == 0 ? "player1" : "player2")} at {spawnTransform?.position}");
        spawnIndex = (spawnIndex + 1) % 2;
        return spawnTransform;
    }
}
