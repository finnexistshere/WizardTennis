using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Prefabs")]
    public PlayerInput playerInputPrefab;

    [Header("Camera Settings")]
    public Transform cameraSpawnTransform; // Assign in Inspector to control camera spawn point

    [Header("Customisation")]
    [Tooltip("Apply customisation on spawn")]
    public bool applyCustomisation = true;

    // NetworkVariable to sync material index across network
    private NetworkVariable<int> materialIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Non-owner clients listen for material changes
            materialIndex.OnValueChanged += OnMaterialIndexChanged;
            // Apply the current value immediately
            ApplyMaterialFromIndex(materialIndex.Value);
            return;
        }

        // Only run setup for local owner
        StartCoroutine(SetupOwnerPlayer());
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            materialIndex.OnValueChanged -= OnMaterialIndexChanged;
        }
        base.OnNetworkDespawn();
    }

    private IEnumerator SetupOwnerPlayer()
    {
        yield return new WaitUntil(() => IsSpawned && IsOwner);

        // Small delay to ensure cameras are registered
        yield return new WaitForSeconds(0.1f);

        Debug.Log($"[LocalPlayerSetup] Setting up player for {(IsHost ? "Host" : "Client")} - ClientId: {OwnerClientId}");

        // SetupPlayerInput(); // Uncomment if you need PlayerInput
        SetupCamera();
        SetupCustomisation();

        Debug.Log($"[{(IsHost ? "Host" : "Client")}] Player setup complete for client {OwnerClientId}");
    }

    private void SetupPlayerInput()
    {
        if (playerInputPrefab == null)
        {
            Debug.LogWarning("PlayerInput prefab is not assigned.");
            return;
        }

        PlayerInput inputInstance = Instantiate(playerInputPrefab);
        inputInstance.gameObject.name = $"PlayerInput_{OwnerClientId}";
        inputInstance.transform.SetParent(transform, false);
        inputInstance.enabled = true;

        try
        {
            inputInstance.ActivateInput();
            Debug.Log($"[{(IsHost ? "Host" : "Client")}] PlayerInput activated for {OwnerClientId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{(IsHost ? "Host" : "Client")}] Failed to activate PlayerInput: {e}");
        }
    }

    private void SetupCamera()
    {
        Debug.Log($"[LocalPlayerSetup] SetupCamera called for {(IsHost ? "Host" : "Client")}");

        // Determine spawn point - use cameraSpawnTransform if assigned, otherwise use player transform
        Transform spawnPoint = cameraSpawnTransform != null ? cameraSpawnTransform : transform;
        Debug.Log($"[LocalPlayerSetup] Spawn point: {spawnPoint.name} at position {spawnPoint.position}");

        // Request camera assignment through the manager
        bool success = NetworkCameraManager.TryAssignCameraToPlayer(IsHost, transform, spawnPoint);

        if (success)
        {
            Debug.Log($"[{(IsHost ? "Host" : "Client")}] Camera SUCCESSFULLY assigned for player {OwnerClientId}");
        }
        else
        {
            Debug.LogError($"[{(IsHost ? "Host" : "Client")}] FAILED to assign camera for player {OwnerClientId}");
        }
    }

    private void SetupCustomisation()
    {
        if (!applyCustomisation)
        {
            Debug.Log($"[LocalPlayerSetup] Customisation disabled for player {OwnerClientId}");
            return;
        }

        if (CustomisationManager.Instance == null)
        {
            Debug.LogWarning($"[LocalPlayerSetup] CustomisationManager not found! Cannot apply customisation.");
            return;
        }

        // Get the saved material index from CustomisationManager
        int savedMaterialIndex = CustomisationManager.Instance.SelectedMaterialIndex;

        Debug.Log($"[LocalPlayerSetup] Applying customisation for player {OwnerClientId} - Material Index: {savedMaterialIndex}");

        // Set the NetworkVariable (this will sync to all clients)
        materialIndex.Value = savedMaterialIndex;

        // Apply locally
        ApplyMaterialFromIndex(savedMaterialIndex);

        // Register this player with the CustomisationManager
        CustomisationManager.Instance.RegisterAsPlayer(gameObject);
    }

    private void OnMaterialIndexChanged(int previousValue, int newValue)
    {
        Debug.Log($"[LocalPlayerSetup] Material index changed from {previousValue} to {newValue} for player {OwnerClientId}");
        ApplyMaterialFromIndex(newValue);
    }

    private void ApplyMaterialFromIndex(int index)
    {
        if (CustomisationManager.Instance == null)
        {
            Debug.LogWarning($"[LocalPlayerSetup] Cannot apply material - CustomisationManager not found!");
            return;
        }

        Material material = CustomisationManager.Instance.GetMaterialAtIndex(index);
        if (material == null)
        {
            Debug.LogWarning($"[LocalPlayerSetup] Invalid material index: {index}");
            return;
        }

        // Apply material to all renderers on this player and children
        ApplyMaterialToAllRenderers(material);

        Debug.Log($"[LocalPlayerSetup] Applied material '{CustomisationManager.Instance.GetMaterialNameAtIndex(index)}' to player {OwnerClientId}");
    }

    private void ApplyMaterialToAllRenderers(Material material)
    {
        // Get all renderers including children
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            // Create new material array with selected material
            Material[] materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }

        Debug.Log($"[LocalPlayerSetup] Applied material to {renderers.Length} renderer(s)");
    }

    /// <summary>
    /// Public method to change material at runtime (owner only)
    /// </summary>
    public void ChangeMaterial(int newMaterialIndex)
    {
        if (!IsOwner)
        {
            Debug.LogWarning($"[LocalPlayerSetup] Only the owner can change their material!");
            return;
        }

        materialIndex.Value = newMaterialIndex;
    }

    /// <summary>
    /// Get the current material index for this player
    /// </summary>
    public int GetMaterialIndex()
    {
        return materialIndex.Value;
    }
}