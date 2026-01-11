using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public Transform[] spawnPoints;

    private void OnEnable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return;

        if (client.PlayerObject == null)
            return;

        NetworkObject playerObject = client.PlayerObject;

        // Only the server sets spawn positions!
        if (NetworkManager.Singleton.IsServer)
            AssignSpawnPosition(playerObject);
    }

    private void AssignSpawnPosition(NetworkObject playerObject)
    {
        // Get index based on clientID so first = left, second = right
        int index = (int)playerObject.OwnerClientId % spawnPoints.Length;

        playerObject.transform.position = spawnPoints[index].position;
        playerObject.transform.rotation = spawnPoints[index].rotation;
    }
}
