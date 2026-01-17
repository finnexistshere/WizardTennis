using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CustomisationManager : MonoBehaviour
{
    public static CustomisationManager Instance;

    [Header("=== UI References (Assign in Inspector) ===")]
    [Tooltip("Material selection dropdown")]
    [SerializeField] private TMP_Dropdown materialDropdown;

    [Tooltip("Preview image for selected material (optional)")]
    [SerializeField] private Image materialPreviewImage;

    [Header("=== UI Auto-Find Settings ===")]
    [Tooltip("Name of the material dropdown GameObject (for auto-finding)")]
    [SerializeField] private string materialDropdownName = "MaterialDropdown";

    [Tooltip("Name of the preview image GameObject (for auto-finding)")]
    [SerializeField] private string materialPreviewImageName = "MaterialPreviewImage";

    [Tooltip("Name of the display object GameObject (for auto-finding)")]
    [SerializeField] private string displayObjectName = "MaterialDisplayObject";

    [Tooltip("Enable automatic UI finding when references are lost")]
    [SerializeField] private bool autoFindUI = true;

    [Header("=== Display Object Settings ===")]
    [Tooltip("3D object to display material preview (assign in inspector or auto-find by name)")]
    [SerializeField] private GameObject displayObject;

    [Tooltip("Apply material to display object's children as well")]
    [SerializeField] private bool applyToDisplayChildren = true;

    [Header("=== Material Settings ===")]
    [Tooltip("Array of available materials for player customisation")]
    [SerializeField] private Material[] availableMaterials;

    [Tooltip("Names for each material (must match array length)")]
    [SerializeField] private string[] materialNames;

    [Tooltip("Preview sprites for each material (optional, must match array length)")]
    [SerializeField] private Sprite[] materialPreviewSprites;

    [SerializeField] private int selectedMaterialIndex = 0;

    [Header("=== Player References ===")]
    [Tooltip("Tag used to find the player GameObject")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Apply material to child objects as well")]
    [SerializeField] private bool applyToChildren = true;

    [Tooltip("Only apply to objects with specific tag (leave empty for all children)")]
    [SerializeField] private string childFilterTag = "";

    [Tooltip("Layer mask for objects to customize (0 = all layers)")]
    [SerializeField] private LayerMask customizationLayerMask = ~0;

    // Public Properties
    public int SelectedMaterialIndex => selectedMaterialIndex;
    public Material SelectedMaterial => (availableMaterials != null && selectedMaterialIndex >= 0 && selectedMaterialIndex < availableMaterials.Length)
        ? availableMaterials[selectedMaterialIndex]
        : null;

    // Events
    public delegate void MaterialChangedHandler(Material newMaterial, int index);
    public event MaterialChangedHandler OnMaterialChanged;

    // Track if UI is currently hooked
    private bool isUIHooked = false;
    private GameObject cachedPlayer;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        ValidateMaterialArrays();

        // Subscribe to scene loaded event
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Auto-hook UI if references are assigned in inspector
        if (HasUIReferences())
        {
            HookUI();
        }

        // Apply saved material to player if in scene
        ApplyMaterialToPlayer();
    }

    private void Update()
    {
        // Auto-find and hook UI if enabled and references are lost
        if (autoFindUI && !isUIHooked)
        {
            TryFindAndHookUI();
        }

        // Auto-find display object if enabled and reference is lost
        if (autoFindUI && displayObject == null)
        {
            TryFindDisplayObject();
        }

        // Check if hooked UI has become null (destroyed)
        if (isUIHooked && !HasValidUIReferences())
        {
            Debug.LogWarning("[CustomisationManager] UI references lost. Will attempt to re-find...");
            UnhookUI();
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log($"[CustomisationManager] Scene loaded: {scene.name}. Searching for UI...");

        // Clear UI references on scene change
        UnhookUI();
        displayObject = null; // Clear display object reference

        // Try to find UI in new scene if auto-find is enabled
        if (autoFindUI)
        {
            TryFindAndHookUI();
            TryFindDisplayObject();
        }

        // Apply saved material to player if in scene
        ApplyMaterialToPlayer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Unsubscribe from scene loaded event
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookUI();
            Instance = null;
        }
    }

    #region Validation

    private void ValidateMaterialArrays()
    {
        if (availableMaterials == null || availableMaterials.Length == 0)
        {
            Debug.LogWarning("[CustomisationManager] No materials assigned! Please assign materials in the inspector.");
            return;
        }

        // Auto-generate material names if not provided
        if (materialNames == null || materialNames.Length != availableMaterials.Length)
        {
            Debug.LogWarning("[CustomisationManager] Material names array doesn't match materials array. Auto-generating names.");
            materialNames = new string[availableMaterials.Length];
            for (int i = 0; i < availableMaterials.Length; i++)
            {
                materialNames[i] = availableMaterials[i] != null ? availableMaterials[i].name : $"Material {i}";
            }
        }

        // Validate preview sprites array
        if (materialPreviewSprites != null && materialPreviewSprites.Length != availableMaterials.Length)
        {
            Debug.LogWarning("[CustomisationManager] Material preview sprites array doesn't match materials array length.");
        }

        // Clamp selected index
        selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, availableMaterials.Length - 1);
    }

    #endregion

    #region UI Management

    private bool HasUIReferences()
    {
        return materialDropdown != null;
    }

    /// <summary>
    /// Check if currently hooked UI references are still valid (not destroyed)
    /// </summary>
    private bool HasValidUIReferences()
    {
        // Check if dropdown still exists
        if (materialDropdown != null && materialDropdown.gameObject != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Hook UI elements for runtime control
    /// </summary>
    public void OnCustomisationMenuOpened(TMP_Dropdown dropdown = null, Image previewImage = null)
    {
        UnhookUI();

        // Override inspector references if runtime references provided
        if (dropdown != null) materialDropdown = dropdown;
        if (previewImage != null) materialPreviewImage = previewImage;

        HookUI();
    }

    public void OnCustomisationMenuClosed()
    {
        // Don't unhook if UI was assigned in inspector
    }

    /// <summary>
    /// Manually force UI to re-hook (useful after scene changes or UI rebuilds)
    /// </summary>
    public void ForceRehookUI()
    {
        if (HasUIReferences())
        {
            Debug.Log("[CustomisationManager] Forcing UI re-hook...");
            UnhookUI();
            HookUI();
        }
        else
        {
            Debug.LogWarning("[CustomisationManager] Cannot re-hook UI - no UI references available.");
        }
    }

    /// <summary>
    /// Attempt to find UI elements by name and hook them
    /// </summary>
    private void TryFindAndHookUI()
    {
        bool foundAny = false;

        // Try to find material dropdown
        if (materialDropdown == null && !string.IsNullOrEmpty(materialDropdownName))
        {
            GameObject dropdownObj = GameObject.Find(materialDropdownName);
            if (dropdownObj != null)
            {
                materialDropdown = dropdownObj.GetComponent<TMP_Dropdown>();
                if (materialDropdown != null)
                {
                    Debug.Log($"[CustomisationManager] Found material dropdown: {materialDropdownName}");
                    foundAny = true;
                }
            }
        }

        // Try to find preview image
        if (materialPreviewImage == null && !string.IsNullOrEmpty(materialPreviewImageName))
        {
            GameObject previewObj = GameObject.Find(materialPreviewImageName);
            if (previewObj != null)
            {
                materialPreviewImage = previewObj.GetComponent<Image>();
                if (materialPreviewImage != null)
                {
                    Debug.Log($"[CustomisationManager] Found material preview image: {materialPreviewImageName}");
                    foundAny = true;
                }
            }
        }

        // Hook UI if we found any elements
        if (foundAny && HasUIReferences())
        {
            HookUI();
        }
    }

    /// <summary>
    /// Attempt to find the display object by name
    /// </summary>
    private void TryFindDisplayObject()
    {
        if (displayObject == null && !string.IsNullOrEmpty(displayObjectName))
        {
            displayObject = GameObject.Find(displayObjectName);
            if (displayObject != null)
            {
                Debug.Log($"[CustomisationManager] Found display object: {displayObjectName}");
                // Deactivate on find (it should start hidden)
                displayObject.SetActive(false);
                // Apply current material to display object
                ApplyMaterialToDisplayObject();
            }
        }
    }

    #endregion

    #region Display Object Management

    /// <summary>
    /// Set the display object reference manually
    /// </summary>
    public void SetDisplayObject(GameObject obj)
    {
        displayObject = obj;
        if (displayObject != null)
        {
            Debug.Log($"[CustomisationManager] Display object set to: {displayObject.name}");
            // Deactivate when manually set
            displayObject.SetActive(false);
            ApplyMaterialToDisplayObject();
        }
    }

    /// <summary>
    /// Activate the display object (make it visible)
    /// </summary>
    public void ActivateDisplayObject()
    {
        if (displayObject != null)
        {
            displayObject.SetActive(true);
            Debug.Log($"[CustomisationManager] Display object activated: {displayObject.name}");
        }
        else
        {
            Debug.LogWarning("[CustomisationManager] Cannot activate display object - reference is null.");
        }
    }

    /// <summary>
    /// Deactivate the display object (hide it)
    /// </summary>
    public void DeactivateDisplayObject()
    {
        if (displayObject != null)
        {
            displayObject.SetActive(false);
            Debug.Log($"[CustomisationManager] Display object deactivated: {displayObject.name}");
        }
        else
        {
            Debug.LogWarning("[CustomisationManager] Cannot deactivate display object - reference is null.");
        }
    }

    /// <summary>
    /// Toggle the display object's active state
    /// </summary>
    public void ToggleDisplayObject()
    {
        if (displayObject != null)
        {
            displayObject.SetActive(!displayObject.activeSelf);
            Debug.Log($"[CustomisationManager] Display object toggled to: {displayObject.activeSelf}");
        }
        else
        {
            Debug.LogWarning("[CustomisationManager] Cannot toggle display object - reference is null.");
        }
    }

    /// <summary>
    /// Apply the selected material to the display object
    /// </summary>
    private void ApplyMaterialToDisplayObject()
    {
        if (displayObject == null || SelectedMaterial == null)
        {
            return;
        }

        int appliedCount = 0;

        // Apply to display object itself
        appliedCount += ApplyMaterialToRenderers(displayObject);

        // Apply to children if enabled
        if (applyToDisplayChildren)
        {
            Renderer[] childRenderers = displayObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                // Apply to all renderers including the parent
                appliedCount += ApplyMaterialToRenderer(renderer);
            }
        }

        Debug.Log($"[CustomisationManager] Applied '{materialNames[selectedMaterialIndex]}' to {appliedCount} renderer(s) on display object '{displayObject.name}'");
    }

    /// <summary>
    /// Get the current display object reference
    /// </summary>
    public GameObject GetDisplayObject()
    {
        return displayObject;
    }

    /// <summary>
    /// Check if display object is currently active
    /// </summary>
    public bool IsDisplayObjectActive()
    {
        return displayObject != null && displayObject.activeSelf;
    }

    private void HookUI()
    {
        if (isUIHooked) return;

        if (materialDropdown != null)
        {
            materialDropdown.onValueChanged.RemoveAllListeners();

            // Populate dropdown if empty
            if (materialDropdown.options.Count == 0)
            {
                PopulateMaterialDropdown();
            }

            materialDropdown.value = selectedMaterialIndex;
            // Use AddListener for real-time updates as dropdown changes
            materialDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        UpdateMaterialPreview();
        isUIHooked = true;
    }

    /// <summary>
    /// Called when dropdown value changes - updates material in real-time
    /// </summary>
    private void OnDropdownValueChanged(int index)
    {
        SetMaterial(index);
    }

    private void UnhookUI()
    {
        if (!isUIHooked) return;

        materialDropdown?.onValueChanged.RemoveListener(OnDropdownValueChanged);

        isUIHooked = false;
    }

    private void PopulateMaterialDropdown()
    {
        if (materialDropdown == null || materialNames == null) return;

        materialDropdown.ClearOptions();
        var options = new List<string>(materialNames);
        materialDropdown.AddOptions(options);
    }

    private void UpdateMaterialPreview()
    {
        if (materialPreviewImage == null) return;

        if (materialPreviewSprites != null &&
            selectedMaterialIndex >= 0 &&
            selectedMaterialIndex < materialPreviewSprites.Length &&
            materialPreviewSprites[selectedMaterialIndex] != null)
        {
            materialPreviewImage.sprite = materialPreviewSprites[selectedMaterialIndex];
            materialPreviewImage.gameObject.SetActive(true);
        }
        else
        {
            materialPreviewImage.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Material Management

    /// <summary>
    /// Set the selected material by index
    /// </summary>
    public void SetMaterial(int index)
    {
        if (availableMaterials == null || index < 0 || index >= availableMaterials.Length)
        {
            Debug.LogWarning($"[CustomisationManager] Invalid material index: {index}");
            return;
        }

        selectedMaterialIndex = index;
        PlayerPrefs.SetInt("SelectedMaterial", selectedMaterialIndex);
        PlayerPrefs.Save();

        Debug.Log($"[CustomisationManager] Material changed to: {materialNames[index]}");

        UpdateMaterialPreview();

        if (materialDropdown != null && materialDropdown.value != index)
            materialDropdown.value = index;

        // Apply to player if in scene
        ApplyMaterialToPlayer();

        // Fire event
        OnMaterialChanged?.Invoke(SelectedMaterial, selectedMaterialIndex);

        // Apply to display object if it exists
        ApplyMaterialToDisplayObject();
    }

    /// <summary>
    /// Set material by material reference (finds index automatically)
    /// </summary>
    public void SetMaterial(Material material)
    {
        if (material == null || availableMaterials == null) return;

        for (int i = 0; i < availableMaterials.Length; i++)
        {
            if (availableMaterials[i] == material)
            {
                SetMaterial(i);
                return;
            }
        }

        Debug.LogWarning($"[CustomisationManager] Material '{material.name}' not found in available materials.");
    }

    /// <summary>
    /// Apply the selected material to the player GameObject and its children
    /// </summary>
    public void ApplyMaterialToPlayer()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            Debug.LogWarning("[CustomisationManager] Player not found in scene. Material will be applied when player spawns.");
            return;
        }

        ApplyMaterialToGameObject(player);
    }

    /// <summary>
    /// Apply material to a specific GameObject and optionally its children
    /// </summary>
    public void ApplyMaterialToGameObject(GameObject target)
    {
        if (target == null || SelectedMaterial == null)
        {
            Debug.LogWarning("[CustomisationManager] Cannot apply material - target or material is null.");
            return;
        }

        int appliedCount = 0;

        // Apply to target object
        if (ShouldApplyToObject(target))
        {
            appliedCount += ApplyMaterialToRenderers(target);
        }

        // Apply to children if enabled
        if (applyToChildren)
        {
            Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                if (renderer.gameObject != target && ShouldApplyToObject(renderer.gameObject))
                {
                    appliedCount += ApplyMaterialToRenderer(renderer);
                }
            }
        }

        Debug.Log($"[CustomisationManager] Applied '{materialNames[selectedMaterialIndex]}' to {appliedCount} renderer(s) on {target.name}");
    }

    private bool ShouldApplyToObject(GameObject obj)
    {
        // Check layer mask
        if (customizationLayerMask != ~0 && ((1 << obj.layer) & customizationLayerMask) == 0)
        {
            return false;
        }

        // Check child filter tag if specified
        if (!string.IsNullOrEmpty(childFilterTag) && !obj.CompareTag(childFilterTag))
        {
            return false;
        }

        return true;
    }

    private int ApplyMaterialToRenderers(GameObject obj)
    {
        int count = 0;
        Renderer[] renderers = obj.GetComponents<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            count += ApplyMaterialToRenderer(renderer);
        }
        return count;
    }

    private int ApplyMaterialToRenderer(Renderer renderer)
    {
        if (renderer == null) return 0;

        // Create new material array with selected material
        Material[] materials = new Material[renderer.sharedMaterials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = SelectedMaterial;
        }
        renderer.sharedMaterials = materials;

        return materials.Length;
    }

    private GameObject FindPlayer()
    {
        // Use cached player if still valid
        if (cachedPlayer != null)
            return cachedPlayer;

        // Find by tag
        cachedPlayer = GameObject.FindGameObjectWithTag(playerTag);

        if (cachedPlayer == null)
        {
            Debug.LogWarning($"[CustomisationManager] No GameObject found with tag '{playerTag}'");
        }

        return cachedPlayer;
    }

    /// <summary>
    /// Clear cached player reference (call this when player is destroyed/respawned)
    /// </summary>
    public void ClearPlayerCache()
    {
        cachedPlayer = null;
    }

    /// <summary>
    /// Register the calling GameObject as the player target for material customization.
    /// Call this from the player's Start() or Awake() method to ensure materials are applied.
    /// </summary>
    /// <param name="player">The GameObject registering itself as the player</param>
    public void RegisterAsPlayer(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("[CustomisationManager] Attempted to register null GameObject as player.");
            return;
        }

        cachedPlayer = player;
        Debug.Log($"[CustomisationManager] Registered '{player.name}' as player target.");

        // Immediately apply the selected material
        ApplyMaterialToGameObject(player);
    }

    #endregion

    #region Loading and Saving

    private void LoadSettings()
    {
        selectedMaterialIndex = PlayerPrefs.GetInt("SelectedMaterial", 0);

        // Validate loaded index
        if (availableMaterials != null)
        {
            selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, availableMaterials.Length - 1);
        }

        Debug.Log($"[CustomisationManager] Loaded material index: {selectedMaterialIndex}");
    }

    /// <summary>
    /// Reset to default material (index 0)
    /// </summary>
    public void ResetToDefault()
    {
        SetMaterial(0);
        Debug.Log("[CustomisationManager] Reset to default material");
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    /// Get the name of the currently selected material
    /// </summary>
    public string GetSelectedMaterialName()
    {
        if (materialNames != null && selectedMaterialIndex >= 0 && selectedMaterialIndex < materialNames.Length)
        {
            return materialNames[selectedMaterialIndex];
        }
        return "Unknown";
    }

    /// <summary>
    /// Get total number of available materials
    /// </summary>
    public int GetMaterialCount()
    {
        return availableMaterials?.Length ?? 0;
    }

    /// <summary>
    /// Get material by index
    /// </summary>
    public Material GetMaterialAtIndex(int index)
    {
        if (availableMaterials != null && index >= 0 && index < availableMaterials.Length)
        {
            return availableMaterials[index];
        }
        return null;
    }

    /// <summary>
    /// Get material name by index
    /// </summary>
    public string GetMaterialNameAtIndex(int index)
    {
        if (materialNames != null && index >= 0 && index < materialNames.Length)
        {
            return materialNames[index];
        }
        return "Unknown";
    }

    #endregion
}