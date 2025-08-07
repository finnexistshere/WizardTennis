using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Pickup Settings")]
    public GameObject pickupPrefab;
    public float spawnInterval = 5f;

    [Header("Buff/Debuff Values")]
    public float buffValue = 0.2f;
    public float debuffValue = -0.2f;

    [Header("Spawn Points")]
    public Transform spawnPointParent;
    public List<Transform> spawnPoints = new List<Transform>();

    private float spawnTimer;

    private void Start()
    {
        // Populate spawn points from children of spawnPointParent
        foreach (Transform child in spawnPointParent)
        {
            spawnPoints.Add(child);
        }

        spawnTimer = spawnInterval;
    }

    private void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnPickup();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnPickup()
    {
        if (spawnPoints.Count == 0) return;

        // Choose random point
        Transform point = spawnPoints[Random.Range(0, spawnPoints.Count)];

        // Spawn pickup
        GameObject pickup = Instantiate(pickupPrefab, point.position, Quaternion.identity);

        // Set whether it's a buff or debuff randomly
        PickupEffect pickupEffect = pickup.GetComponent<PickupEffect>();
        if (pickupEffect != null)
        {
            bool isBuff = Random.value > 0.5f;
            pickupEffect.type = isBuff ? PickupEffect.EffectType.Buff : PickupEffect.EffectType.Debuff;
            pickupEffect.value = isBuff ? buffValue : debuffValue;
        }
    }

}
