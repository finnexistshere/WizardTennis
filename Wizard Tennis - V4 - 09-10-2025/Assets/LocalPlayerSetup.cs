using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Prefabs")]
    public PlayerInput playerInputPrefab;
    public GameObject playerCameraPrefab;
    public GameObject playerPrefab; // The NetworkObject player prefab

    private GameObject spawnedCamera;

    private static bool playersSpawned = false; // Ensure we only spawn once

    public override void OnNetworkSpawn()
    {
        try
        {
            Debug.Log($"[{(IsHost ? "Host" : "Client")}] OnNetworkSpawn called for player {OwnerClientId}");

            if (IsServer && !playersSpawned)
            {
                ForceSpawnTwoPlayers();
                playersSpawned = true;
            }

            if (IsOwner)
            {
                SetupPlayerInput();
                SetupCamera();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{(IsHost ? "Host" : "Client")}] Exception in OnNetworkSpawn for player {OwnerClientId}:\n{e}\nStack Trace:\n{e.StackTrace}");
        }
    }

    private void ForceSpawnTwoPlayers()
    {
        // Hard-coded spawn positions
        Vector3 hostPosition = new Vector3(-2f, 0f, 0f);
        Vector3 clientPosition = new Vector3(2f, 0f, 0f);

        // Host player
        SpawnPlayerForClient(NetworkManager.Singleton.LocalClientId, hostPosition);

        // If there is at least one connected client, spawn their player
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.Singleton.LocalClientId)
                continue; // skip host (already spawned)

            SpawnPlayerForClient(client.ClientId, clientPosition);
        }
    }

    private void SpawnPlayerForClient(ulong clientId, Vector3 spawnPosition)
    {
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("Player prefab is missing NetworkObject component!");
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        Debug.Log($"Spawned player for client {clientId} at {spawnPosition}");
    }

    private void SetupPlayerInput()
    {
        if (playerInputPrefab == null)
        {
            Debug.LogWarning("PlayerInput prefab is not assigned.");
            return;
        }

        var inputInstance = Instantiate(playerInputPrefab);
        inputInstance.gameObject.name = $"PlayerInput_{OwnerClientId}";
        inputInstance.transform.SetParent(transform, false);

        if (!inputInstance.enabled)
            inputInstance.enabled = true;

        try
        {
            inputInstance.ActivateInput();
            Debug.Log($"[{(IsHost ? "Host" : "Client")}] PlayerInput activated for {OwnerClientId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{(IsHost ? "Host" : "Client")}] Failed to activate PlayerInput: {e}");
        }
    }

    private void SetupCamera()
    {
        if (playerCameraPrefab == null)
        {
            Debug.LogWarning("Player Camera Prefab not assigned!");
            return;
        }

        spawnedCamera = Instantiate(playerCameraPrefab);
        spawnedCamera.transform.SetParent(transform, false);
        spawnedCamera.transform.localPosition = Vector3.zero;
        spawnedCamera.transform.localRotation = Quaternion.identity;

        Debug.Log($"[{(IsHost ? "Host" : "Client")}] Camera spawned for player {OwnerClientId}");
    }

    private void OnDestroy()
    {
        Debug.Log($"[{(IsHost ? "Host" : "Client")}] Player {OwnerClientId} destroyed.");
    }
}
