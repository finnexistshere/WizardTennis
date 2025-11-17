using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Prefabs")]
    public PlayerInput playerInputPrefab;
    public GameObject playerCameraPrefab;

    private GameObject spawnedCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        StartCoroutine(SetupOwnerPlayer());
    }

    private IEnumerator SetupOwnerPlayer()
    {
        // Wait until this player object is fully spawned on this client
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        // Spawn input prefab as a plain GameObject (no NetworkObject)
        if (playerInputPrefab != null)
        {
            var inputInstance = Instantiate(playerInputPrefab);
            inputInstance.gameObject.name = $"PlayerInput_{OwnerClientId}";
            inputInstance.transform.SetParent(transform, false);

            // Activate input safely
            inputInstance.enabled = true;
            inputInstance.ActivateInput();
        }

        // Spawn camera prefab as usual
        if (playerCameraPrefab != null)
        {
            spawnedCamera = Instantiate(playerCameraPrefab);
            spawnedCamera.transform.SetParent(transform, false);
            spawnedCamera.transform.localPosition = Vector3.zero;
            spawnedCamera.transform.localRotation = Quaternion.identity;
        }

        Debug.Log($"[{(IsHost ? "Host" : "Client")}] Player setup complete for {OwnerClientId}");
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

        // Safe parenting after spawn
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
}
