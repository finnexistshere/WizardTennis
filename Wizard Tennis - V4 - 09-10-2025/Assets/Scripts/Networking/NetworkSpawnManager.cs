using UnityEngine;
using System.Collections.Generic;

public class NetworkSpawnManager : MonoBehaviour
{
    public List<Transform> spawnPoints = new List<Transform>();
    private int nextSpawnIndex = 0;

    /// <summary>
    /// Returns the next spawn position AND rotation, advancing the index.
    /// </summary>
    public Vector3 GetNextSpawnPosition(out Quaternion rotation)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("No spawn points assigned! Using world zero.");
            rotation = Quaternion.identity;
            return Vector3.zero;
        }

        Vector3 pos = spawnPoints[nextSpawnIndex].position;
        rotation = spawnPoints[nextSpawnIndex].rotation;

        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;
        return pos;
    }
}
