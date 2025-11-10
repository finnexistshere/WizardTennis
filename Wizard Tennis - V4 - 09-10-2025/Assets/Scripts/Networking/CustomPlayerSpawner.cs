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

        NetworkObject playerInstance = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        playerInstance.SpawnAsPlayerObject(clientId);
    }
}
