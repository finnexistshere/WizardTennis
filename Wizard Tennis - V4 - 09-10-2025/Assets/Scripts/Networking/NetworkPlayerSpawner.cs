using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private NetworkSpawnManager spawnManager;

    private void Awake()
    {
        spawnManager = FindObjectOfType<NetworkSpawnManager>();

        if (spawnManager == null)
        {
            Debug.LogError("No NetworkSpawnManager found in scene!");
        }
    }

    public void SpawnPlayer(ulong clientId)
    {
        Debug.Log($"Spawning player for client {clientId}");

        Quaternion rot;
        Vector3 pos = spawnManager.GetNextSpawnPosition(out rot);

        Debug.Log($"Spawn position: {pos}");

        GameObject obj = Instantiate(playerPrefab, pos, rot);
        obj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}
