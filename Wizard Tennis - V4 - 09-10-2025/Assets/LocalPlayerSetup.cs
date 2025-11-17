using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Prefabs")]
    public PlayerInput playerInputPrefab;
    public GameObject playerCameraPrefab;

    private GameObject spawnedCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // Only run for local owner
        StartCoroutine(SetupOwnerPlayer());
    }

    private IEnumerator SetupOwnerPlayer()
    {
        // Wait until this player object is fully spawned and owned
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        // Setup input
        SetupPlayerInput();

        // Setup camera
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

        // Parent under this player
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

        // Set initial position/rotation to match spawn
        spawnedCamera.transform.position = transform.position;
        spawnedCamera.transform.rotation = transform.rotation;

        // Initialize CameraElasticSway
        var sway = spawnedCamera.GetComponent<CameraElasticSway>();
        if (sway != null)
        {
            sway.player = transform;
            sway.spawnPoint = transform; // optional, makes it start at spawn
        }

        spawnedCamera.SetActive(true);

        Debug.Log($"[{(IsHost ? "Host" : "Client")}] CameraElasticSway spawned for player {OwnerClientId}");
    }
}
