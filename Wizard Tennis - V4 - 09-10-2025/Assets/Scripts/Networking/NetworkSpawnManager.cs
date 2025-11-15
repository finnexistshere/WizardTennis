using UnityEngine;
using System.Collections.Generic;

public class NetworkSpawnManager : MonoBehaviour
{
    public List<Transform> spawnPoints = new List<Transform>();

    private int nextSpawnIndex = 0;

    public Vector3 GetNextSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("No spawn points assigned! Using world zero.");
            return Vector3.zero;
        }

        Vector3 pos = spawnPoints[nextSpawnIndex].position;
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;
        return pos;
    }

    public Quaternion GetNextSpawnRotation()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
            return Quaternion.identity;

        return spawnPoints[nextSpawnIndex].rotation;
    }
}
