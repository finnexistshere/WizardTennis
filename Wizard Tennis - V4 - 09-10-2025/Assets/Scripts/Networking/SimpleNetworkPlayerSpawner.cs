using UnityEngine;
using Unity.Netcode;

public class SimpleNetworkPlayerSpawner : NetworkBehaviour
{
    [Header("Assign spawn positions in inspector")]
    public Transform spawnPositionClient1;
    public Transform spawnPositionClient2;

    private void Awake()
    {
        if (!IsServer) return; // Only server sets initial positions

        Vector3 spawnPos = Vector3.zero;
        ulong clientId = NetworkManager.Singleton.LocalClientId;

        // Determine which spawn position to use
        if (clientId == 0 && spawnPositionClient1 != null)
            spawnPos = spawnPositionClient1.position;
        else if (clientId == 1 && spawnPositionClient2 != null)
            spawnPos = spawnPositionClient2.position;
        else
            spawnPos = new Vector3(0, 2, 0); // fallback

        // Set position immediately
        transform.position = spawnPos;
    }
}
