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

    [Header("=== Custom Colour Base ===")]
    [Tooltip("Base material for custom coloring (must have _Color property)")]
    [SerializeField] private Material customColourBaseMaterial;

    [Header("=== Player References ===")]
    [Tooltip("Tag used to find the player GameObject")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Apply material to child objects as well")]
    [SerializeField] private bool applyToChildren = true;

    [Tooltip("Only apply to objects with specific tag (leave empty for all children)")]
    [SerializeField] private string childFilterTag = "";

    [Tooltip("Layer mask for objects to customize (0 = all layers)")]
    [SerializeField] private LayerMask customizationLayerMask = ~0;

    [Header("=== Material Channels ===")]
    [Tooltip("Define color channels for custom material mode")]
    [SerializeField] private List<MaterialChannel> materialChannels = new();

    [System.Serializable]
    public class MaterialChannel
    {
        [Tooltip("Editor-friendly name")]
        public string channelName = "Channel";

        [Tooltip("Only apply to objects with this tag (leave empty for any)")]
        public string targetTag = "";

        [Tooltip("Only apply to objects on these layers")]
        public LayerMask targetLayers = ~0;

        [Tooltip("Material slot index (-1 = all slots)")]
        public int materialSlot = -1;

        [Tooltip("Color applied to this channel (always includes alpha = 1)")]
        public Color color = Color.white;
    }

    private readonly Dictionary<(Material, Color), Material> recolouredMaterialCache = new();

    // Public Properties
    public int SelectedMaterialIndex => selectedMaterialIndex;

    [Header("=== Custom Colour Filtering ===")]
    [SerializeField] private string customColourTargetTag = "CustomColour";
    [SerializeField] private LayerMask customColourLayerMask = ~0;
    [SerializeField] private int customColourMaterialSlot = -1; // -1 = all slots

    public Material SelectedMaterial
    {
        get
        {
            if (IsCustomColourSelected)
            {
                EnsureRuntimeCustomMaterial();
                ApplyCustomColourToMaterial();
                return runtimeCustomMaterial;
            }

            if (availableMaterials != null &&
                selectedMaterialIndex >= 0 &&
                selectedMaterialIndex < availableMaterials.Length)
            {
                return availableMaterials[selectedMaterialIndex];
            }

            return null;
        }
    }

    // Check if custom color mode is selected
    public bool IsCustomColourSelected => selectedMaterialIndex >= availableMaterials.Length;

    // Events
    public delegate void MaterialChangedHandler(Material newMaterial, int index);
    public event MaterialChangedHandler OnMaterialChanged;

    public delegate void CustomColourModeChanged(bool active);
    public event CustomColourModeChanged OnCustomColourModeChanged;

    // Track if UI is currently hooked
    private bool isUIHooked = false;
    private GameObject cachedPlayer;

    private Material runtimeCustomMaterial;
    [SerializeField] private Color customColour = Color.white;

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

    private void EnsureRuntimeCustomMaterial()
    {
        if (runtimeCustomMaterial != null)
            return;

        if (customColourBaseMaterial == null)
        {
            Debug.LogError("[CustomisationManager] No Custom Colour Base Material assigned!");
            return;
        }

        runtimeCustomMaterial = new Material(customColourBaseMaterial);
        runtimeCustomMaterial.name = customColourBaseMaterial.name + "_RuntimeInstance";
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
        displayObject = null;

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
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookUI();

            // Clean up cached materials
            foreach (var mat in recolouredMaterialCache.Values)
            {
                if (mat != null)
                    Destroy(mat);
            }
            recolouredMaterialCache.Clear();

            if (runtimeCustomMaterial != null)
            {
                Destroy(runtimeCustomMaterial);
                runtimeCustomMaterial = null;
            }

            Instance = null;
        }
    }

    #region Validation

    private void ValidateMaterialArrays()
    {
        if (availableMaterials == null || availableMaterials.Length == 0)
        {
            Debug.LogWarning("[CustomisationManager] No materials assigned! Please assign materials in the inspector.");
            availableMaterials = new Material[0];
        }

        // Create material names array (base materials + "Custom Colors")
        int totalOptions = availableMaterials.Length + (customColourBaseMaterial != null ? 1 : 0);

        if (materialNames == null || materialNames.Length != totalOptions)
        {
            Debug.LogWarning("[CustomisationManager] Material names array doesn't match. Auto-generating names.");
            materialNames = new string[totalOptions];

            for (int i = 0; i < availableMaterials.Length; i++)
            {
                materialNames[i] = availableMaterials[i] != null ? availableMaterials[i].name : $"Material {i}";
            }

            // Add custom color option at the end
            if (customColourBaseMaterial != null)
            {
                materialNames[availableMaterials.Length] = "Custom Colors";
            }
        }

        // Validate preview sprites array
        if (materialPreviewSprites != null && materialPreviewSprites.Length != totalOptions)
        {
            Debug.LogWarning("[CustomisationManager] Material preview sprites array doesn't match materials array length.");
        }

        // Clamp selected index
        selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, totalOptions - 1);
    }

    #endregion

    #region UI Management

    private bool HasUIReferences()
    {
        return materialDropdown != null;
    }

    private bool HasValidUIReferences()
    {
        return materialDropdown != null && materialDropdown.gameObject != null;
    }

    public void OnCustomisationMenuOpened(TMP_Dropdown dropdown = null, Image previewImage = null)
    {
        UnhookUI();

        if (dropdown != null) materialDropdown = dropdown;
        if (previewImage != null) materialPreviewImage = previewImage;

        HookUI();
    }

    public void OnCustomisationMenuClosed()
    {
        // Don't unhook if UI was assigned in inspector
    }

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

    private void TryFindAndHookUI()
    {
        bool foundAny = false;

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

        if (foundAny && HasUIReferences())
        {
            HookUI();
        }
    }

    private void TryFindDisplayObject()
    {
        if (displayObject == null && !string.IsNullOrEmpty(displayObjectName))
        {
            displayObject = GameObject.Find(displayObjectName);
            if (displayObject != null)
            {
                Debug.Log($"[CustomisationManager] Found display object: {displayObjectName}");
                displayObject.SetActive(false);
                ApplyMaterialToDisplayObject();
            }
        }
    }

    private void HookUI()
    {
        if (isUIHooked) return;

        if (materialDropdown != null)
        {
            materialDropdown.onValueChanged.RemoveAllListeners();

            if (materialDropdown.options.Count == 0)
            {
                PopulateMaterialDropdown();
            }

            materialDropdown.value = selectedMaterialIndex;
            materialDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        UpdateMaterialPreview();
        isUIHooked = true;
    }

    private void OnDropdownValueChanged(int index)
    {
        SetMaterial(index);
    }

    private void UnhookUI()
    {
        if (!isUIHooked) return;

        if (materialDropdown != null)
        {
            materialDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }

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

    private bool ShouldApplyCustomColour(Renderer renderer)
    {
        if (renderer == null)
            return false;

        GameObject obj = renderer.gameObject;

        // Tag filter
        if (!string.IsNullOrEmpty(customColourTargetTag) &&
            !obj.CompareTag(customColourTargetTag))
            return false;

        // Layer filter
        if (((1 << obj.layer) & customColourLayerMask) == 0)
            return false;

        return true;
    }

    #endregion

    #region Display Object Management

    public void SetDisplayObject(GameObject obj)
    {
        displayObject = obj;
        if (displayObject != null)
        {
            Debug.Log($"[CustomisationManager] Display object set to: {displayObject.name}");
            displayObject.SetActive(false);
            ApplyMaterialToDisplayObject();
        }
    }

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
                appliedCount += ApplyMaterialToRenderer(renderer);
            }
        }

        Debug.Log($"[CustomisationManager] Applied material to {appliedCount} renderer(s) on display object '{displayObject.name}'");
    }

    public GameObject GetDisplayObject()
    {
        return displayObject;
    }

    public bool IsDisplayObjectActive()
    {
        return displayObject != null && displayObject.activeSelf;
    }

    #endregion

    #region Material Management

    public void SetMaterial(int index)
    {
        bool wasCustom = IsCustomColourSelected;

        if (index < 0 || index >= materialNames.Length)
        {
            Debug.LogWarning($"[CustomisationManager] Invalid material index: {index}");
            return;
        }

        selectedMaterialIndex = index;
        PlayerPrefs.SetInt("SelectedMaterial", selectedMaterialIndex);
        PlayerPrefs.Save();

        bool isCustom = IsCustomColourSelected;

        Debug.Log($"[CustomisationManager] Material changed to: {materialNames[index]} (Custom Mode: {isCustom})");

        UpdateMaterialPreview();

        if (materialDropdown != null && materialDropdown.value != index)
            materialDropdown.value = index;

        // Apply to player if in scene
        ApplyMaterialToPlayer();

        // Fire events
        OnMaterialChanged?.Invoke(SelectedMaterial, selectedMaterialIndex);

        if (wasCustom != isCustom)
        {
            OnCustomColourModeChanged?.Invoke(isCustom);
        }

        // Apply to display object if it exists
        ApplyMaterialToDisplayObject();
    }

    private void ApplyCustomColourToMaterial()
    {
        if (runtimeCustomMaterial == null)
            return;

        Color c = customColour;
        c.a = 1f;

        if (runtimeCustomMaterial.HasProperty("_BaseColor"))
            runtimeCustomMaterial.SetColor("_BaseColor", c);

        if (runtimeCustomMaterial.HasProperty("_Color"))
            runtimeCustomMaterial.SetColor("_Color", c);

        if (runtimeCustomMaterial.HasProperty("_TintColor"))
            runtimeCustomMaterial.SetColor("_TintColor", c);
    }

    public void SetCustomColour(Color color)
    {
        if (!IsCustomColourSelected)
            return;

        color.a = 1f;
        customColour = color;

        EnsureRuntimeCustomMaterial();
        ApplyCustomColourToMaterial();

        ApplyMaterialToPlayer();
        ApplyMaterialToDisplayObject();
    }

    public Color GetCustomColour() => customColour;

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

    public MaterialChannel GetChannel(int index)
    {
        if (materialChannels == null || index < 0 || index >= materialChannels.Count)
            return null;

        return materialChannels[index];
    }

    public void SetChannelColor(int channelIndex, Color color)
    {
        if (!IsCustomColourSelected)
            return;

        if (materialChannels == null || materialChannels.Count == 0)
        {
            Debug.LogWarning("[CustomisationManager] No material channels defined.");
            return;
        }

        if (channelIndex < 0 || channelIndex >= materialChannels.Count)
        {
            Debug.LogWarning(
                $"[CustomisationManager] Invalid channel index: {channelIndex} (Channel count: {materialChannels.Count})"
            );
            return;
        }

        color.a = 1f;
        materialChannels[channelIndex].color = color;

        ClearMaterialCache();
        ApplyMaterialToPlayer();
        ApplyMaterialToDisplayObject();
    }

    private void ClearMaterialCache()
    {
        foreach (var mat in recolouredMaterialCache.Values)
        {
            if (mat != null && mat != customColourBaseMaterial)
                Destroy(mat);
        }
        recolouredMaterialCache.Clear();
    }

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

        Debug.Log($"[CustomisationManager] Applied material to {appliedCount} renderer(s) on {target.name}");
    }

    private bool ShouldApplyToObject(GameObject obj)
    {
        if (customizationLayerMask != ~0 && ((1 << obj.layer) & customizationLayerMask) == 0)
        {
            return false;
        }

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
        if (renderer == null)
            return 0;

        Material baseMat = SelectedMaterial;
        if (baseMat == null)
            return 0;

        Material[] mats = renderer.sharedMaterials;

        // NON-custom mode: apply normally
        if (!IsCustomColourSelected)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i] = baseMat;

            renderer.sharedMaterials = mats;
            return mats.Length;
        }

        // CUSTOM COLOUR MODE
        EnsureRuntimeCustomMaterial();
        ApplyCustomColourToMaterial();

        bool shouldRecolour = ShouldApplyCustomColour(renderer);

        for (int i = 0; i < mats.Length; i++)
        {
            // If slot filtering is enabled
            if (customColourMaterialSlot >= 0 && i != customColourMaterialSlot)
                continue;

            if (shouldRecolour)
            {
                mats[i] = runtimeCustomMaterial;
            }
            else
            {
                mats[i] = baseMat;
            }
        }

        renderer.sharedMaterials = mats;
        return mats.Length;
    }

    private bool ChannelMatches(Renderer renderer, MaterialChannel channel)
    {
        GameObject obj = renderer.gameObject;

        if (!string.IsNullOrEmpty(channel.targetTag) && !obj.CompareTag(channel.targetTag))
            return false;

        if (((1 << obj.layer) & channel.targetLayers) == 0)
            return false;

        return true;
    }

    private Material GetRecolouredMaterial(Material baseMaterial, Color color)
    {
        if (baseMaterial == null) return null;

        // Ensure alpha is 1
        color.a = 1f;

        var key = (baseMaterial, color);

        if (recolouredMaterialCache.TryGetValue(key, out var cached))
            return cached;

        Material instance = new Material(baseMaterial);
        instance.name = $"{baseMaterial.name}_Recolor_{ColorUtility.ToHtmlStringRGBA(color)}";

        // Try multiple common color properties
        if (instance.HasProperty("_Color"))
            instance.SetColor("_Color", color);
        if (instance.HasProperty("_BaseColor"))
            instance.SetColor("_BaseColor", color);
        if (instance.HasProperty("_TintColor"))
            instance.SetColor("_TintColor", color);

        recolouredMaterialCache[key] = instance;
        return instance;
    }

    private GameObject FindPlayer()
    {
        if (cachedPlayer != null)
            return cachedPlayer;

        if (playerTag != null)
        {
            cachedPlayer = GameObject.FindGameObjectWithTag(playerTag);
        }

        if (cachedPlayer == null)
        {
            Debug.LogWarning($"[CustomisationManager] No GameObject found with tag '{playerTag}'");
        }

        return cachedPlayer;
    }

    public void ClearPlayerCache()
    {
        cachedPlayer = null;
    }

    public void RegisterAsPlayer(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("[CustomisationManager] Attempted to register null GameObject as player.");
            return;
        }

        cachedPlayer = player;
        Debug.Log($"[CustomisationManager] Registered '{player.name}' as player target.");

        ApplyMaterialToGameObject(player);
    }

    #endregion

    #region Loading and Saving

    private void LoadSettings()
    {
        selectedMaterialIndex = PlayerPrefs.GetInt("SelectedMaterial", 0);

        if (availableMaterials != null)
        {
            int maxIndex = availableMaterials.Length + (customColourBaseMaterial != null ? 1 : 0) - 1;
            selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, maxIndex);
        }

        Debug.Log($"[CustomisationManager] Loaded material index: {selectedMaterialIndex}");
    }

    public void ResetToDefault()
    {
        SetMaterial(0);
        Debug.Log("[CustomisationManager] Reset to default material");
    }

    #endregion

    #region Public Utility Methods

    public string GetSelectedMaterialName()
    {
        if (materialNames != null && selectedMaterialIndex >= 0 && selectedMaterialIndex < materialNames.Length)
        {
            return materialNames[selectedMaterialIndex];
        }
        return "Unknown";
    }

    public int GetMaterialCount()
    {
        return availableMaterials?.Length ?? 0;
    }

    public Material GetMaterialAtIndex(int index)
    {
        if (availableMaterials != null && index >= 0 && index < availableMaterials.Length)
        {
            return availableMaterials[index];
        }
        return null;
    }

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