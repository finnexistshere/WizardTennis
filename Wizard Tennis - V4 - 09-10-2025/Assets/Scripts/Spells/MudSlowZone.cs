using UnityEngine;
using System.Collections.Generic;

// for use in the Networked Scene

// This TECHNICALLY can also impact the player in the singleplayer scene, however there are exactly Zero cases where the mud will be on the player side of the court 

// So this should be fine

public class MudSlowZone : MonoBehaviour
{
    [Header("Slow Effect")]
    [Range(0f, 1f)]
    public float slowMultiplier = 0.5f; // Player moves at 50% speed

    [Header("Visual")]
    public float lifetime = 5f; // How long the pit lasts

    // Track which players are currently slowed by THIS pit
    private Dictionary<MainCharacterMovement, float> slowedPlayers = new Dictionary<MainCharacterMovement, float>();

    private void Start()
    {
        // Auto-destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's a player
        var movement = other.GetComponent<MainCharacterMovement>();
        if (movement != null && !slowedPlayers.ContainsKey(movement))
        {
            Debug.Log($"[MudSlowZone] {other.name} entered mud pit - slowing to {slowMultiplier * 100}%");

            // Store original speed
            float originalSpeed = movement.speed;
            slowedPlayers[movement] = originalSpeed;

            // Apply slow
            movement.speed = originalSpeed * slowMultiplier;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Restore speed when leaving mud
        var movement = other.GetComponent<MainCharacterMovement>();
        if (movement != null && slowedPlayers.ContainsKey(movement))
        {
            Debug.Log($"[MudSlowZone] {other.name} exited mud pit - restoring speed");

            // Restore original speed
            movement.speed = slowedPlayers[movement];
            slowedPlayers.Remove(movement);
        }
    }

    private void OnDestroy()
    {
        // Clean up any players still in the zone when pit disappears
        Debug.Log($"[MudSlowZone] Pit destroyed - restoring speed for {slowedPlayers.Count} players");

        foreach (var kvp in slowedPlayers)
        {
            if (kvp.Key != null)
            {
                Debug.Log($"[MudSlowZone] Restoring {kvp.Key.name}'s speed from mud destruction");
                kvp.Key.speed = kvp.Value; // Restore original speed
            }
        }

        slowedPlayers.Clear();
    }
}