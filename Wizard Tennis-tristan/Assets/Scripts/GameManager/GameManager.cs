using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Pickup Settings")]
    public GameObject[] pickupPrefabs;
    public float spawnInterval = 5f;

    [Header("Spawn Points")]
    public Collider playerSpellArea;
    public Collider oppSpellArea;

    private float spawnTimer;
    private bool isPlayersSpell = true; // whose turn it is to get a spell in their court
    public bool spellSpawned = false;

    private void Start()
    {
        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            if (!spellSpawned)
            {
                SpawnPickup();
                spellSpawned = true;
            }
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnPickup()
    {
        Collider currentSpellArea = null;
        if (isPlayersSpell)
            currentSpellArea = playerSpellArea;
        else
            currentSpellArea = oppSpellArea;
        isPlayersSpell = !isPlayersSpell;

            // Choose random point
            Vector3 point = new Vector3(
            Random.Range(currentSpellArea.bounds.min.x, currentSpellArea.bounds.max.x),
            Random.Range(currentSpellArea.bounds.min.y, currentSpellArea.bounds.max.y),
            Random.Range(currentSpellArea.bounds.min.z, currentSpellArea.bounds.max.z)
        );

        // Spawn pickup
        GameObject pickup = Instantiate(pickupPrefabs[Random.Range(0, pickupPrefabs.Length)], point, Quaternion.identity);
    }

}
