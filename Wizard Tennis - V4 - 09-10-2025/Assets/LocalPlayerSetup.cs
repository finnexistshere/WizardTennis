using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Spawn Points (Assign in Inspector)")]
    public Transform player1Spawn;
    public Transform player2Spawn;

[Header("Optional Prefabs")]
    public PlayerInput playerInputPrefab;
    public GameObject playerCameraPrefab;

    public GameObject spawnedCamera;

    private void Awake()
    {
        Debug.Log($"[LocalPlayerSetup] Awake called for client {OwnerClientId}");
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[LocalPlayerSetup] OnNetworkSpawn called. IsOwner={IsOwner}, OwnerClientId={OwnerClientId}, IsServer={IsServer}");

        if (!IsOwner)
        {
            Debug.Log($"[LocalPlayerSetup] Not owner, skipping transform setup for client {OwnerClientId}");
            return;
        }

        Transform spawnPoint = GetSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[LocalPlayerSetup] Spawn point not assigned for client {OwnerClientId}");
        }
        else
        {
            Debug.Log($"[LocalPlayerSetup] Setting client {OwnerClientId} position to {spawnPoint.position} and rotation to {spawnPoint.rotation.eulerAngles}");
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        StartCoroutine(SetupOwnerPlayer());
    }

    private Transform GetSpawnPoint()
    {
        // Determine spawn point based on client ID (simple 2-player logic)
        if (OwnerClientId == 0 && player1Spawn != null)
        {
            Debug.Log($"[LocalPlayerSetup] Client {OwnerClientId} using player1Spawn");
            return player1Spawn;
        }
        else if (OwnerClientId != 0 && player2Spawn != null)
        {
            Debug.Log($"[LocalPlayerSetup] Client {OwnerClientId} using player2Spawn");
            return player2Spawn;
        }
        else
        {
            Debug.LogWarning($"[LocalPlayerSetup] No valid spawn point for client {OwnerClientId}");
            return null;
        }
    }

    private IEnumerator SetupOwnerPlayer()
    {
        // Wait until this player is fully spawned
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        Debug.Log($"[LocalPlayerSetup] Starting player input and camera setup for client {OwnerClientId}");

        SetupPlayerInput();
        SetupCamera();
    }

    private void SetupPlayerInput()
    {
        if (playerInputPrefab == null)
        {
            Debug.LogWarning($"[LocalPlayerSetup] PlayerInput prefab not assigned for client {OwnerClientId}");
            return;
        }

        PlayerInput inputInstance = Instantiate(playerInputPrefab);
        inputInstance.gameObject.name = $"PlayerInput_{OwnerClientId}";
        inputInstance.transform.SetParent(transform, false);
        inputInstance.enabled = true;

        try
        {
            inputInstance.ActivateInput();
            Debug.Log($"[LocalPlayerSetup] PlayerInput activated for client {OwnerClientId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LocalPlayerSetup] Failed to activate PlayerInput for client {OwnerClientId}: {e}");
        }
    }

    private void SetupCamera()
    {
        if (playerCameraPrefab == null)
        {
            Debug.LogWarning($"[LocalPlayerSetup] Player Camera prefab not assigned for client {OwnerClientId}");
            return;
        }

        spawnedCamera = Instantiate(playerCameraPrefab);
        spawnedCamera.transform.SetParent(null);
        spawnedCamera.transform.position = transform.position;
        spawnedCamera.transform.rotation = transform.rotation;

        var sway = spawnedCamera.GetComponent<CameraElasticSway>();
        if (sway != null)
        {
            sway.player = transform;
            sway.spawnPoint = transform;
            Debug.Log($"[LocalPlayerSetup] CameraElasticSway linked for client {OwnerClientId}");
        }

        spawnedCamera.SetActive(true);
        Debug.Log($"[LocalPlayerSetup] Camera spawned and active for client {OwnerClientId}");
    }

}
