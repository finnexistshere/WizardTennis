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

    // Public accessor for other scripts
    public static Dictionary<ulong, GameObject> SpawnedPlayers => spawnedPlayers;

    private static readonly Dictionary<ulong, GameObject> spawnedPlayers =
        new Dictionary<ulong, GameObject>();

    // Map client -> spawn transform (public so game manager can read)
    public static readonly Dictionary<ulong, Transform> PlayerSpawnPoints = new Dictionary<ulong, Transform>();

    // Track spawn assignments to prevent race conditions
    private static readonly Dictionary<ulong, int> PlayerSpawnIndices = new Dictionary<ulong, int>();

    private int spawnIndex = 0;

    public static NetworkPlayerSpawner_Better Instance;

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

        // Validate spawn points
        if (player1Spawn == null || player2Spawn == null)
        {
            Debug.LogError("[Spawner] ? Spawn points not assigned! Please assign in inspector.");
            return;
        }

        Debug.Log($"[Spawner] Spawn points validated: P1={player1Spawn.position}, P2={player2Spawn.position}");

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Spawn host manually if not already spawned
        if (!spawnedPlayers.ContainsKey(NetworkManager.Singleton.LocalClientId))
        {
            SpawnPlayer(NetworkManager.Singleton.LocalClientId);
        }

        if (verboseLogging)
            Debug.Log($"[Spawner] Server spawner ready. Host ClientId: {NetworkManager.Singleton.LocalClientId}");
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
        {
            if (verboseLogging)
                Debug.Log($"[Spawner] Host {clientId} already spawned, skipping");
            return;
        }

        SpawnPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (verboseLogging)
            Debug.Log($"[Spawner] Client {clientId} disconnected, cleaning up...");

        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerObj))
        {
            if (playerObj != null && playerObj.TryGetComponent(out NetworkObject netObj))
            {
                if (netObj.IsSpawned)
                    netObj.Despawn();
            }

            Destroy(playerObj);
            spawnedPlayers.Remove(clientId);
        }

        // Cleanup spawn point records
        PlayerSpawnPoints.Remove(clientId);
        PlayerSpawnIndices.Remove(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] ? Player prefab not assigned!");
            return;
        }

        // Get and record spawn transform FIRST
        Transform spawnPoint = GetNextSpawnTransform(clientId);
        int assignedIndex = (spawnIndex - 1 + 2) % 2; // Calculate assigned index (wrap around)

        Vector3 spawnPos = spawnPoint.position;
        Quaternion spawnRot = spawnPoint.rotation;

        Debug.Log($"[Spawner] ?? Client {clientId} assigned to spawn {assignedIndex} at position {spawnPos}");

        // Store spawn data BEFORE instantiation
        PlayerSpawnPoints[clientId] = spawnPoint;
        PlayerSpawnIndices[clientId] = assignedIndex;

        // Instantiate player at spawn position
        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);

        // Verify instantiation position IMMEDIATELY
        Debug.Log($"[Spawner] ?? Player instantiated at {playerInstance.transform.position} (expected {spawnPos})");

        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] ? Player prefab is missing NetworkObject!");
            Destroy(playerInstance);

            // Cleanup records since spawn failed
            PlayerSpawnPoints.Remove(clientId);
            PlayerSpawnIndices.Remove(clientId);
            return;
        }

        // CRITICAL: Disable NetworkTransform temporarily if it exists to prevent position override
        var networkTransform = playerInstance.GetComponent<NetworkTransform>();
        bool hadNetTransform = networkTransform != null;
        if (hadNetTransform)
        {
            networkTransform.enabled = false;
            Debug.Log($"[Spawner] ?? Temporarily disabled NetworkTransform for spawn");
        }

        // FORCE position one more time before spawning
        playerInstance.transform.position = spawnPos;
        playerInstance.transform.rotation = spawnRot;

        // Spawn on network
        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = playerInstance;

        Debug.Log($"[Spawner] ?? NetworkObject spawned. Position now: {playerInstance.transform.position}");

        // Send spawn info via ClientRpc with position data
        SetPlayerSpawnPositionClientRpc(clientId, spawnPos, spawnRot, assignedIndex);

        // Re-enable NetworkTransform and lock position for a moment
        if (hadNetTransform)
        {
            StartCoroutine(ReenableNetworkTransformAfterDelay(playerInstance, networkTransform, spawnPos, spawnRot));
        }

        Debug.Log($"[Spawner] ? Spawned player for client {clientId} at spawn {assignedIndex} ({spawnPos})");
    }

    private IEnumerator ReenableNetworkTransformAfterDelay(GameObject player, NetworkTransform netTransform, Vector3 correctPos, Quaternion correctRot)
    {
        // Wait a frame for network spawn to complete
        yield return new WaitForEndOfFrame();

        // Force position again
        player.transform.position = correctPos;
        player.transform.rotation = correctRot;

        Debug.Log($"[Spawner] ?? Re-forced position to {correctPos} after spawn");

        // Wait a bit more
        yield return new WaitForSeconds(0.1f);

        // Re-enable NetworkTransform
        if (netTransform != null)
        {
            netTransform.enabled = true;
            Debug.Log($"[Spawner] ?? Re-enabled NetworkTransform");
        }

        // Lock position for duration
        float lockEndTime = Time.time + spawnPositionLockDuration;
        while (Time.time < lockEndTime)
        {
            if (player != null && Vector3.Distance(player.transform.position, correctPos) > 0.1f)
            {
                Debug.LogWarning($"[Spawner] ?? Position drift detected! Correcting from {player.transform.position} to {correctPos}");
                player.transform.position = correctPos;
                player.transform.rotation = correctRot;
            }
            yield return new WaitForSeconds(0.05f); // Check every 50ms
        }

        Debug.Log($"[Spawner] ?? Spawn position lock released for {player.name}");
    }

    private Transform GetNextSpawnTransform(ulong clientId)
    {
        // Check if this client already has an assigned spawn point (reconnection case)
        if (PlayerSpawnIndices.ContainsKey(clientId))
        {
            int savedIndex = PlayerSpawnIndices[clientId];
            if (verboseLogging)
                Debug.Log($"[Spawner] ?? Restoring client {clientId} to saved spawn index {savedIndex}");
            return savedIndex == 0 ? player1Spawn : player2Spawn;
        }

        Transform spawnTransform;

        if (spawnIndex == 0 && player1Spawn != null)
            spawnTransform = player1Spawn;
        else if (player2Spawn != null)
            spawnTransform = player2Spawn;
        else
        {
            Debug.LogError("[Spawner] ? No valid spawn point available!");
            spawnTransform = transform; // fallback to spawner position
        }

        spawnIndex = (spawnIndex + 1) % 2;
        return spawnTransform;
    }

    /// <summary>
    /// CRITICAL: Force set player position on all clients immediately after spawn
    /// </summary>
    [ClientRpc]
    private void SetPlayerSpawnPositionClientRpc(ulong clientId, Vector3 position, Quaternion rotation, int spawnIdx)
    {
        Debug.Log($"[Spawner-Client] ?? Received spawn position for client {clientId}: {position} (spawn {spawnIdx})");

        // Find the player object
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(clientId, out NetworkObject netObj))
        {
            GameObject playerObj = netObj.gameObject;

            // FORCE the position on this client
            playerObj.transform.position = position;
            playerObj.transform.rotation = rotation;

            Debug.Log($"[Spawner-Client] ? Set client {clientId} position to {position}");

            // Start position lock coroutine on clients too
            if (Instance != null)
            {
                Instance.StartCoroutine(Instance.LockPlayerPositionForDuration(playerObj, position, rotation));
            }
        }
        else
        {
            Debug.LogWarning($"[Spawner-Client] ?? Could not find spawned player object for client {clientId}");
        }
    }

    private IEnumerator LockPlayerPositionForDuration(GameObject player, Vector3 correctPos, Quaternion correctRot)
    {
        float lockEndTime = Time.time + spawnPositionLockDuration;
        while (Time.time < lockEndTime && player != null)
        {
            if (Vector3.Distance(player.transform.position, correctPos) > 0.1f)
            {
                Debug.LogWarning($"[Spawner] ?? CLIENT SIDE: Position drift detected! Correcting to {correctPos}");
                player.transform.position = correctPos;
                player.transform.rotation = correctRot;
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    public void RespawnSpecificPlayer(ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Spawner] ?? RespawnSpecificPlayer can only be called on server!");
            return;
        }

        // Despawn existing player first
        if (spawnedPlayers.TryGetValue(clientId, out GameObject oldPlayer) && oldPlayer != null)
        {
            if (oldPlayer.TryGetComponent(out NetworkObject oldNetObj) && oldNetObj.IsSpawned)
            {
                oldNetObj.Despawn();
            }
            Destroy(oldPlayer);
        }

        // Get spawn location (prefer saved location)
        Transform spawn;
        if (PlayerSpawnPoints.TryGetValue(clientId, out Transform savedSpawn) && savedSpawn != null)
        {
            spawn = savedSpawn;
            Debug.Log($"[Spawner] ?? Respawning client {clientId} at saved spawn {spawn.position}");
        }
        else
        {
            spawn = GetNextSpawnTransform(clientId);
            PlayerSpawnPoints[clientId] = spawn;
            Debug.Log($"[Spawner] ?? No saved spawn for client {clientId}, using new spawn {spawn.position}");
        }

        Vector3 spawnPos = spawn.position;
        Quaternion spawnRot = spawn.rotation;

        // Instantiate new player
        GameObject newPlayer = Instantiate(playerPrefab, spawnPos, spawnRot);
        NetworkObject netObj = newPlayer.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] ? Player prefab missing NetworkObject during respawn!");
            Destroy(newPlayer);
            return;
        }

        // Disable NetworkTransform temporarily
        var networkTransform = newPlayer.GetComponent<NetworkTransform>();
        if (networkTransform != null)
            networkTransform.enabled = false;

        netObj.SpawnAsPlayerObject(clientId, true);
        spawnedPlayers[clientId] = newPlayer;

        // Force position and re-enable
        SetPlayerSpawnPositionClientRpc(clientId, spawnPos, spawnRot, PlayerSpawnIndices[clientId]);

        if (networkTransform != null)
        {
            StartCoroutine(ReenableNetworkTransformAfterDelay(newPlayer, networkTransform, spawnPos, spawnRot));
        }

        Debug.Log($"[Spawner] ? Respawned player for client {clientId}");
    }

    /// <summary>
    /// Get spawn point for a specific client (safe accessor)
    /// </summary>
    public static Transform GetSpawnPointForClient(ulong clientId)
    {
        if (PlayerSpawnPoints.TryGetValue(clientId, out Transform spawn))
            return spawn;

        Debug.LogWarning($"[Spawner] ?? No spawn point found for client {clientId}");
        return null;
    }

    /// <summary>
    /// Get spawn index for a specific client (0 or 1)
    /// </summary>
    public static int GetSpawnIndexForClient(ulong clientId)
    {
        if (PlayerSpawnIndices.TryGetValue(clientId, out int index))
            return index;

        Debug.LogWarning($"[Spawner] ?? No spawn index found for client {clientId}");
        return -1;
    }

    /// <summary>
    /// Force all spawned players back to their spawn positions
    /// Useful for resetting after round or fixing position bugs
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ForcePlayersToSpawnPositionsServerRpc()
    {
        if (!IsServer) return;

        Debug.Log("[Spawner] ?? Forcing all players to spawn positions...");

        foreach (var kvp in spawnedPlayers)
        {
            ulong clientId = kvp.Key;
            GameObject player = kvp.Value;

            if (player == null) continue;

            if (PlayerSpawnPoints.TryGetValue(clientId, out Transform spawnPoint))
            {
                ForcePlayerToPosition(player, clientId, spawnPoint.position, spawnPoint.rotation);
            }
        }
    }

    /// <summary>
    /// Force a specific player to their spawn position (server only)
    /// </summary>
    public void ForcePlayerToSpawnPosition(ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Spawner] ForcePlayerToSpawnPosition can only be called on server!");
            return;
        }

        if (!spawnedPlayers.TryGetValue(clientId, out GameObject player) || player == null)
        {
            Debug.LogWarning($"[Spawner] No player found for client {clientId}");
            return;
        }

        if (!PlayerSpawnPoints.TryGetValue(clientId, out Transform spawnPoint))
        {
            Debug.LogWarning($"[Spawner] No spawn point found for client {clientId}");
            return;
        }

        ForcePlayerToPosition(player, clientId, spawnPoint.position, spawnPoint.rotation);
    }

    /// <summary>
    /// Internal method to force player position with CharacterController handling
    /// </summary>
    private void ForcePlayerToPosition(GameObject player, ulong clientId, Vector3 position, Quaternion rotation)
    {
        Debug.Log($"[Spawner] ?? Forcing client {clientId} to position {position}");

        // Disable CharacterController if present (it can interfere with position setting)
        CharacterController charController = player.GetComponent<CharacterController>();
        bool hadCharController = charController != null && charController.enabled;

        if (hadCharController)
        {
            charController.enabled = false;
            Debug.Log($"[Spawner] ?? Disabled CharacterController for {player.name}");
        }

        // Disable NetworkTransform temporarily
        var networkTransform = player.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        bool hadNetTransform = networkTransform != null && networkTransform.enabled;

        if (hadNetTransform)
        {
            networkTransform.enabled = false;
            Debug.Log($"[Spawner] ?? Disabled NetworkTransform for {player.name}");
        }

        // Set position
        player.transform.position = position;
        player.transform.rotation = rotation;

        Debug.Log($"[Spawner] ?? Set {player.name} to {player.transform.position}");

        // Re-enable components and sync to clients
        if (hadCharController)
        {
            charController.enabled = true;
            Debug.Log($"[Spawner] ?? Re-enabled CharacterController");
        }

        if (hadNetTransform)
        {
            networkTransform.enabled = true;
            Debug.Log($"[Spawner] ?? Re-enabled NetworkTransform");
        }

        // Force sync to all clients
        ForcePlayerPositionClientRpc(clientId, position, rotation);
    }

    /// <summary>
    /// Force player position on all clients
    /// </summary>
    [ClientRpc]
    private void ForcePlayerPositionClientRpc(ulong clientId, Vector3 position, Quaternion rotation)
    {
        // Find the player by clientId
        GameObject player = null;

        if (spawnedPlayers.TryGetValue(clientId, out player) && player != null)
        {
            // Found in local dictionary
        }
        else if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(clientId, out NetworkObject netObj))
        {
            player = netObj.gameObject;
        }

        if (player == null)
        {
            Debug.LogWarning($"[Spawner-Client] ?? Could not find player for client {clientId}");
            return;
        }

        Debug.Log($"[Spawner-Client] ?? Forcing client {clientId} position to {position}");

        // Disable CharacterController temporarily
        CharacterController charController = player.GetComponent<CharacterController>();
        if (charController != null && charController.enabled)
        {
            charController.enabled = false;
            player.transform.position = position;
            player.transform.rotation = rotation;
            charController.enabled = true;
        }
        else
        {
            player.transform.position = position;
            player.transform.rotation = rotation;
        }

        Debug.Log($"[Spawner-Client] ? Client {clientId} position set to {position}");
    }

    /// <summary>
    /// Context menu helper to force all players to spawn (testing)
    /// </summary>
    [ContextMenu("Force All Players To Spawn Positions")]
    private void ContextMenuForcePlayersToSpawn()
    {
        if (IsServer)
        {
            ForcePlayersToSpawnPositionsServerRpc();
        }
        else
        {
            Debug.LogWarning("[Spawner] Can only force positions from server!");
        }
    }

    /// <summary>
    /// Debug: Print all spawn assignments
    /// </summary>
    [ContextMenu("Debug Print Spawn Assignments")]
    private void DebugPrintSpawnAssignments()
    {
        Debug.Log("=== SPAWN ASSIGNMENTS ===");
        foreach (var kvp in PlayerSpawnPoints)
        {
            int index = PlayerSpawnIndices.ContainsKey(kvp.Key) ? PlayerSpawnIndices[kvp.Key] : -1;
            GameObject player = spawnedPlayers.ContainsKey(kvp.Key) ? spawnedPlayers[kvp.Key] : null;
            Vector3 currentPos = player != null ? player.transform.position : Vector3.zero;
            Debug.Log($"Client {kvp.Key}: Spawn {index} at {kvp.Value.position} | Current: {currentPos}");
        }
        Debug.Log("========================");
    }
}