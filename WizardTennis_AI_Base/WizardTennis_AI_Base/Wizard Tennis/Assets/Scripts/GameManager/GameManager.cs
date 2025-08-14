using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Pickup Settings")]
    public List<GameObject> pickupPrefabs;
    public float spawnInterval = 7f;
    public float spawnRadius = 25f;
    public int maxActivePickups = 4;

    [Header("Spawn Settings")]
    public Transform spawnCenter;
    private float spawnTimer;
    private List<GameObject> activePickups = new List<GameObject>();

    private void Start()
    {
        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        // Clean nulls in case pickups are destroyed
        activePickups.RemoveAll(p => p == null);

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && activePickups.Count < maxActivePickups)
        {
            SpawnPickup();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnPickup()
    {
        // Try a few times to find a valid position
        Vector3 spawnPos = Vector3.zero;
        bool validPositionFound = false;
        int attempts = 0;

        while (!validPositionFound && attempts < 20)
        {
            attempts++;
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            spawnPos = spawnCenter.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Check for existing pickups nearby
            bool tooClose = false;
            foreach (GameObject pickup in activePickups)
            {
                if (pickup != null && Vector3.Distance(pickup.transform.position, spawnPos) < 1.5f) // Min spacing
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                validPositionFound = true;
        }

        if (!validPositionFound)
            return; // Couldn’t find a free spot

        // Pick a random prefab from the list
        GameObject prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Count)];

        // Spawn and track it
        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);
        activePickups.Add(newPickup);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(spawnCenter.position, spawnRadius);
        // Show the Spawn radius for debugging purposes
    }
}