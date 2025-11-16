using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private NetworkSpawnManager spawnManager;

    private void Awake()
    {
        spawnManager = FindObjectOfType<NetworkSpawnManager>();
    }

    public void SpawnPlayer(ulong clientId)
    {
        if (playerPrefab == null || spawnManager == null) return;

        // Get position AND rotation together
        Quaternion spawnRot;
        Vector3 spawnPos = spawnManager.GetNextSpawnPosition(out spawnRot);

        // Instantiate networked player
        GameObject player = Instantiate(playerPrefab, spawnPos, spawnRot);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
    }
}
