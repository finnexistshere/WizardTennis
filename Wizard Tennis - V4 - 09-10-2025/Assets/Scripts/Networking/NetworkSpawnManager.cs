using UnityEngine;
using System.Collections.Generic;

public class NetworkSpawnManager : MonoBehaviour
{
    public List<Transform> spawnPoints = new List<Transform>();

    private int nextSpawnIndex = 0;

    public Vector3 GetNextSpawnPosition(out Quaternion rot)
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points assigned!");
            rot = Quaternion.identity;
            return Vector3.zero;
        }

        Transform t = spawnPoints[nextSpawnIndex];

        Debug.Log($"Spawning at {t.position}");

        // increment AFTER storing
        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Count;

        rot = t.rotation;
        return t.position;
    }
}
