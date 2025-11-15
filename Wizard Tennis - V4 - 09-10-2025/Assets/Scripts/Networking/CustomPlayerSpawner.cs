using UnityEngine;
using Unity.Netcode;

public class CustomPlayerSpawner : MonoBehaviour
{
    public NetworkObject playerPrefab;
    public PlayerSpawnManager spawnManager;

    private void Start()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SpawnPlayer(NetworkManager.Singleton.LocalClientId);
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
            SpawnPlayer(clientId);
    }

    private void SpawnPlayer(ulong clientId)
    {
        Transform spawnPoint = spawnManager.GetNextSpawnPoint();
        if (spawnPoint == null) return;

        // Get the prefab height if it has a CapsuleCollider
        float yOffset = 0f;
        var collider = playerPrefab.GetComponent<Collider>();
        if (collider != null)
        {
            yOffset = collider.bounds.extents.y + 0.1f; // half-height + small buffer
        }

        Vector3 spawnPos = spawnPoint.position + Vector3.up * yOffset;
        Quaternion spawnRot = spawnPoint.rotation;

        NetworkObject playerInstance = Instantiate(playerPrefab, spawnPos, spawnRot);
        playerInstance.SpawnAsPlayerObject(clientId);
    }
}
