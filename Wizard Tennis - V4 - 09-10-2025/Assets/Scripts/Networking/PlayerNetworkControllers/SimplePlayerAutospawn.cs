using UnityEngine;
using System.Collections;

public class SimpleSpawnSetter : MonoBehaviour
{
    [Header("Tag-based spawn selection")]
    [SerializeField] private string spawnTag = "SpawnPoint";

    [Header("Delay before applying spawn (seconds)")]
    [SerializeField] private float spawnDelay = 0.15f;

    private bool hasSpawned = false;

    private void Start()
    {
        StartCoroutine(ApplySpawnPosition());
    }

    private IEnumerator ApplySpawnPosition()
    {
        // Safety delay so networking or other systems don’t override it
        yield return new WaitForSeconds(spawnDelay);

        if (hasSpawned) yield break;

        GameObject[] spawns = GameObject.FindGameObjectsWithTag(spawnTag);
        if (spawns.Length == 0)
        {
            Debug.LogError("NO SPAWNPOINTS FOUND WITH TAG: " + spawnTag);
            yield break;
        }

        // Pick the first one (or random if you want)
        Transform targetSpawn = spawns[0].transform;

        transform.position = targetSpawn.position;
        transform.rotation = Quaternion.identity; // (0,0,0 rotation always)

        Debug.Log($"Spawn assigned to {targetSpawn.name} at {targetSpawn.position}");
        hasSpawned = true;
    }
}
