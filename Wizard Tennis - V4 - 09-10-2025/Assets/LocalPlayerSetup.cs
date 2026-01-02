using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Prefabs")]
    public PlayerInput playerInputPrefab;

    [Header("Camera Settings")]
    public Transform cameraSpawnTransform; // Assign in Inspector to control camera spawn point

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // Only run for local owner
        StartCoroutine(SetupOwnerPlayer());
    }

    private IEnumerator SetupOwnerPlayer()
    {
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        // Small delay to ensure cameras are registered
        yield return new WaitForSeconds(0.1f);

        Debug.Log($"[LocalPlayerSetup] Setting up player for {(IsHost ? "Host" : "Client")} - ClientId: {OwnerClientId}");

        // SetupPlayerInput(); // Uncomment if you need PlayerInput
        SetupCamera();

        Debug.Log($"[{(IsHost ? "Host" : "Client")}] Player setup complete for client {OwnerClientId}");
    }

    private void SetupPlayerInput()
    {
        if (playerInputPrefab == null)
        {
            Debug.LogWarning("PlayerInput prefab is not assigned.");
            return;
        }

        PlayerInput inputInstance = Instantiate(playerInputPrefab);
        inputInstance.gameObject.name = $"PlayerInput_{OwnerClientId}";
        inputInstance.transform.SetParent(transform, false);
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
        Debug.Log($"[LocalPlayerSetup] SetupCamera called for {(IsHost ? "Host" : "Client")}");

        // Determine spawn point - use cameraSpawnTransform if assigned, otherwise use player transform
        Transform spawnPoint = cameraSpawnTransform != null ? cameraSpawnTransform : transform;

        Debug.Log($"[LocalPlayerSetup] Spawn point: {spawnPoint.name} at position {spawnPoint.position}");

        // Request camera assignment through the manager
        bool success = NetworkCameraManager.TryAssignCameraToPlayer(IsHost, transform, spawnPoint);

        if (success)
        {
            Debug.Log($"[{(IsHost ? "Host" : "Client")}] Camera SUCCESSFULLY assigned for player {OwnerClientId}");
        }
        else
        {
            Debug.LogError($"[{(IsHost ? "Host" : "Client")}] FAILED to assign camera for player {OwnerClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        // Optional: You could implement camera unassignment here if needed
        // This would require tracking which camera was assigned to this player
        base.OnNetworkDespawn();
    }
}