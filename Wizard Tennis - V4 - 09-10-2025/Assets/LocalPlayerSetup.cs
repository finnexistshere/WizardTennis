using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Camera Prefab")]
    public GameObject playerCameraPrefab;

    private GameObject spawnedCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        StartCoroutine(SetupWhenReady());
    }

    private IEnumerator SetupWhenReady()
    {
        // Wait until this client owns the object and it is fully spawned
        yield return new WaitUntil(() => IsOwner && NetworkObject.IsSpawned);

        // Wait a couple of frames for NetworkTransform to sync
        yield return null;
        yield return null;

        SetupCamera();

        Debug.Log($"[{(IsHost ? "Host" : "Client")}] Player camera setup complete for {OwnerClientId}");
    }

    private void SetupCamera()
    {
        if (playerCameraPrefab == null)
        {
            Debug.LogWarning("Player Camera Prefab not assigned!");
            return;
        }

        spawnedCamera = Instantiate(playerCameraPrefab);
        spawnedCamera.transform.SetParent(null);

        // Force camera to match player position
        spawnedCamera.transform.position = transform.position;
        spawnedCamera.transform.rotation = transform.rotation;

        var sway = spawnedCamera.GetComponent<CameraElasticSway>();
        if (sway != null)
        {
            sway.player = transform;
            sway.spawnPoint = transform;
        }

        spawnedCamera.SetActive(true);
    }
}
