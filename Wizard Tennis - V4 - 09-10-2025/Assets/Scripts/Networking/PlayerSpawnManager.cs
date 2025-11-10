using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpawnManager : MonoBehaviour
{
    public List<Transform> spawnPoints;

    private int nextSpawnIndex = 0;

    public Transform GetNextSpawnPoint()
    {
        if (spawnPoints.Count == 0) return null;

        Transform spawn = spawnPoints[nextSpawnIndex];
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;
        return spawn;
    }
}
