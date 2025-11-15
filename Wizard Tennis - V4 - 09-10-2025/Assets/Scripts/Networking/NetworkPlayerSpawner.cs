using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    [Header("Rotation Fix")]
    public Vector3 rotationOffset = new Vector3(0, 0, 0); // adjust here

    private IEnumerator Start()
    {
        yield return null;

        if (!IsServer) yield break;

        NetworkSpawnManager spawnManager = FindObjectOfType<NetworkSpawnManager>();

        if (spawnManager == null)
        {
            Debug.LogWarning("NetworkSpawnManager not found — spawning at Vector3.zero");
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.Euler(rotationOffset);
        }
        else
        {
            Vector3 spawnPos = spawnManager.GetNextSpawnPosition();
            Quaternion spawnRot = spawnManager.GetNextSpawnRotation();

            // Apply correction
            Quaternion adjustedRot = spawnRot * Quaternion.Euler(rotationOffset);

            transform.SetPositionAndRotation(spawnPos, adjustedRot);
        }
    }
}
