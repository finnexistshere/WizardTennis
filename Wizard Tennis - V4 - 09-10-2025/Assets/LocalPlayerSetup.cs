using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Prefabs")]
    public PlayerInput playerInputPrefab;
    public GameObject playerCameraPrefab;

    [Header("Camera Settings")]
    public Transform cameraSpawnTransform; // Assign in Inspector to control camera spawn

    private GameObject spawnedCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // Only run for local owner
        StartCoroutine(SetupOwnerPlayer());
    }

    private IEnumerator SetupOwnerPlayer()
    {
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        // SetupPlayerInput();
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
        if (playerCameraPrefab == null)
        {
            Debug.LogWarning("Player Camera Prefab not assigned!");
            return;
        }

        // Spawn camera only for this client
        spawnedCamera = Instantiate(playerCameraPrefab);

        // Detach from prefab
        spawnedCamera.transform.SetParent(null);

        // Set initial position/rotation
        if (cameraSpawnTransform != null)
        {
            spawnedCamera.transform.position = cameraSpawnTransform.position;
            spawnedCamera.transform.rotation = cameraSpawnTransform.rotation;
        }
        else
        {
            // Default to player position if no transform assigned
            spawnedCamera.transform.position = transform.position;
            spawnedCamera.transform.rotation = transform.rotation;
        }

        // Initialize CameraElasticSway
        var sway = spawnedCamera.GetComponent<CameraElasticSway>();
        if (sway != null)
        {
            sway.player = transform;

            // Use cameraSpawnTransform if assigned, otherwise default to player
            sway.spawnPoint = cameraSpawnTransform != null ? cameraSpawnTransform : transform;
        }

        spawnedCamera.SetActive(true);
        Debug.Log($"[{(IsHost ? "Host" : "Client")}] CameraElasticSway spawned for player {OwnerClientId}");
    }
}
