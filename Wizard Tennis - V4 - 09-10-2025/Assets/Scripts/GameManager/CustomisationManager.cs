using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class CustomisationManager : MonoBehaviour
{
    public static CustomisationManager Instance;

    [Header("=== UI References (Assign in Inspector) ===")]
    [Tooltip("Material selection dropdown")]
    [SerializeField] private TMP_Dropdown materialDropdown;

    [Tooltip("Head selection dropdown")]
    [SerializeField] private TMP_Dropdown headDropdown;

    [Tooltip("Preview image for selected material (optional)")]
    [SerializeField] private Image materialPreviewImage;

    [Tooltip("Preview image for selected head (optional)")]
    [SerializeField] private Image headPreviewImage;

    [Header("=== UI Auto-Find Settings ===")]
    [Tooltip("Name of the material dropdown GameObject (for auto-finding)")]
    [SerializeField] private string materialDropdownName = "MaterialDropdown";

    [Tooltip("Name of the head dropdown GameObject (for auto-finding)")]
    [SerializeField] private string headDropdownName = "HeadDropdown";

    [Tooltip("Name of the material preview image GameObject (for auto-finding)")]
    [SerializeField] private string materialPreviewImageName = "MaterialPreviewImage";

    [Tooltip("Name of the head preview image GameObject (for auto-finding)")]
    [SerializeField] private string headPreviewImageName = "HeadPreviewImage";

    [Tooltip("Name of the display object GameObject (for auto-finding)")]
    [SerializeField] private string displayObjectName = "MaterialDisplayObject";

    [Tooltip("Enable automatic UI finding when references are lost")]
    [SerializeField] private bool autoFindUI = true;

    [Header("=== Head Swap Settings ===")]
    [Tooltip("Array of head prefabs to swap")]
    [SerializeField] private GameObject[] availableHeads;

    [Tooltip("Names for each head (must match array length)")]
    [SerializeField] private string[] headNames;

    [Tooltip("Preview sprites for each head (optional, must match array length)")]
    [SerializeField] private Sprite[] headPreviewSprites;

    [Tooltip("Name of the original head object to find and replace (e.g., 'Head' or 'sk_wizard_head')")]
    [SerializeField] private string originalHeadName = "Head";

    [Tooltip("Position offset from original head (adjust if heads don't align)")]
    [SerializeField] private Vector3 headPositionOffset = Vector3.zero;

    [Tooltip("Rotation offset from original head (adjust if heads don't align)")]
    [SerializeField] private Vector3 headRotationOffset = Vector3.zero;

    [Tooltip("Scale override for swapped heads (Vector3.one = use prefab scale)")]
    [SerializeField] private Vector3 headScaleOverride = Vector3.one;

    [Tooltip("Apply material to swapped heads")]
    [SerializeField] private bool applyMaterialToHead = true;

    [SerializeField] private int selectedHeadIndex = 0;

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

    [Header("=== Custom Colour Filtering ===")]
    [Tooltip("Tag for objects that should receive custom coloring")]
    [SerializeField] private string customColourTargetTag = "CustomColour";

    [Tooltip("Layers for objects that should receive custom coloring")]
    [SerializeField] private LayerMask customColourLayerMask = ~0;

    [Tooltip("Material slot index to apply custom color (-1 = all slots)")]
    [SerializeField] private int customColourMaterialSlot = -1;

    [Header("=== Debug ===")]
    [SerializeField] private bool debugLog = false;

    // Public Properties
    public int SelectedMaterialIndex => selectedMaterialIndex;
    public int SelectedHeadIndex => selectedHeadIndex;
    public bool IsCustomColourSelected => selectedMaterialIndex >= availableMaterials.Length;

    // Custom color values (HSV)
    private float customHue = 0f;
    private float customSaturation = 1f;
    private float customValue = 1f;
    private float customAlpha = 1f;

    private Material runtimeCustomMaterial;
    private readonly Dictionary<(Material, Color), Material> recolouredMaterialCache = new();

    // Head swap tracking
    private GameObject currentSwappedHead;
    private Transform originalHeadParent;
    private Vector3 originalHeadLocalPosition;
    private Quaternion originalHeadLocalRotation;
    private Vector3 originalHeadLocalScale;

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

    // Events
    public delegate void MaterialChangedHandler(Material newMaterial, int index);
    public event MaterialChangedHandler OnMaterialChanged;

    public delegate void HeadChangedHandler(GameObject newHead, int index);
    public event HeadChangedHandler OnHeadChanged;

    public delegate void CustomColourModeChanged(bool active);
    public event CustomColourModeChanged OnCustomColourModeChanged;

    public delegate void CustomColourChanged(Color color);
    public event CustomColourChanged OnCustomColourChanged;

    // Track if UI is currently hooked
    private bool isUIHooked = false;
    private GameObject cachedPlayer;
    private ColorSliderUI_HSV colorSliderUI;
    private float lastUICheckTime = 0f;
    private const float UI_CHECK_INTERVAL = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        ValidateMaterialArrays();
        ValidateHeadArrays();

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (HasUIReferences())
        {
            HookUI();
        }

        // Apply customization in sequence: Head first, then material
        StartCoroutine(ApplyCustomizationSequence());
    }

    private void Update()
    {
        // Throttle UI checks
        if (Time.unscaledTime - lastUICheckTime < UI_CHECK_INTERVAL)
            return;

        lastUICheckTime = Time.unscaledTime;

        if (autoFindUI && !isUIHooked)
        {
            TryFindAndHookUI();
        }

        if (autoFindUI && displayObject == null)
        {
            TryFindDisplayObject();
        }

        if (isUIHooked && !HasValidUIReferences())
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] UI references lost. Will attempt to re-find...");
            UnhookUI();
        }

        // Find and update ColorSliderUI_HSV
        if (colorSliderUI == null)
        {
            colorSliderUI = FindObjectOfType<ColorSliderUI_HSV>();
            if (colorSliderUI != null && debugLog)
                Debug.Log("[CustomisationManager] Found ColorSliderUI_HSV component");
        }

        if (colorSliderUI != null)
        {
            colorSliderUI.TryFindSliders();
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (debugLog)
            Debug.Log($"[CustomisationManager] Scene loaded: {scene.name}. Resetting references...");

        // Force full reset of all references
        UnhookUI();
        displayObject = null;
        colorSliderUI = null;
        cachedPlayer = null;
        currentSwappedHead = null;
        originalHeadParent = null;

        // Wait a frame then try to reconnect and apply customization
        StartCoroutine(DelayedSceneSetup());
    }

    private IEnumerator DelayedSceneSetup()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();
        yield return null;

        if (debugLog)
            Debug.Log("[CustomisationManager] Attempting to reconnect UI after scene load...");

        if (autoFindUI)
        {
            TryFindAndHookUI();
            TryFindDisplayObject();

            // Find color slider UI
            colorSliderUI = FindObjectOfType<ColorSliderUI_HSV>();
            if (colorSliderUI != null)
            {
                if (debugLog)
                    Debug.Log("[CustomisationManager] Found ColorSliderUI_HSV after scene load");

                colorSliderUI.TryFindSliders();
                colorSliderUI.RefreshFromManager();
            }
        }

        // Apply customization in sequence
        yield return StartCoroutine(ApplyCustomizationSequence());

        // Broadcast current state
        if (IsCustomColourSelected)
        {
            OnCustomColourModeChanged?.Invoke(true);
            Color currentColor = GetCustomColorRGB();
            OnCustomColourChanged?.Invoke(currentColor);
        }
        else
        {
            OnCustomColourModeChanged?.Invoke(false);
        }

        if (debugLog)
            Debug.Log($"[CustomisationManager] Scene setup complete. UI Hooked: {isUIHooked}, Custom Mode: {IsCustomColourSelected}");
    }

    /// <summary>
    /// Apply customization in correct sequence: Head swap FIRST, then material
    /// </summary>
    private IEnumerator ApplyCustomizationSequence()
    {
        if (debugLog)
            Debug.Log("[CustomisationManager] === STARTING CUSTOMIZATION SEQUENCE ===");

        // STEP 1: Find player
        GameObject player = FindPlayer();
        if (player == null)
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] Cannot apply customization - player not found");
            yield break;
        }

        // STEP 2: Apply head swap FIRST
        if (debugLog)
            Debug.Log("[CustomisationManager] Step 1: Applying head swap...");

        ApplyHeadSwapToPlayer();
        yield return null; // Wait one frame for head to be instantiated

        // STEP 3: Apply material SECOND (after head is swapped)
        if (debugLog)
            Debug.Log("[CustomisationManager] Step 2: Applying material...");

        ApplyMaterialToPlayer();

        if (debugLog)
            Debug.Log("[CustomisationManager] === CUSTOMIZATION SEQUENCE COMPLETE ===");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookUI();

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

            if (currentSwappedHead != null)
            {
                Destroy(currentSwappedHead);
                currentSwappedHead = null;
            }

            Instance = null;
        }
    }

    #region Validation

    private void ValidateMaterialArrays()
    {
        if (availableMaterials == null || availableMaterials.Length == 0)
        {
            Debug.LogWarning("[CustomisationManager] No materials assigned!");
            availableMaterials = new Material[0];
        }

        int totalOptions = availableMaterials.Length + (customColourBaseMaterial != null ? 1 : 0);

        if (materialNames == null || materialNames.Length != totalOptions)
        {
            Debug.LogWarning("[CustomisationManager] Material names array doesn't match. Auto-generating names.");
            materialNames = new string[totalOptions];

            for (int i = 0; i < availableMaterials.Length; i++)
            {
                materialNames[i] = availableMaterials[i] != null ? availableMaterials[i].name : $"Material {i}";
            }

            if (customColourBaseMaterial != null)
            {
                materialNames[availableMaterials.Length] = "Custom Colors";
            }
        }

        if (materialPreviewSprites != null && materialPreviewSprites.Length != totalOptions)
        {
            Debug.LogWarning("[CustomisationManager] Material preview sprites array doesn't match.");
        }

        selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, totalOptions - 1);
    }

    private void ValidateHeadArrays()
    {
        if (availableHeads == null || availableHeads.Length == 0)
        {
            Debug.LogWarning("[CustomisationManager] No heads assigned!");
            availableHeads = new GameObject[0];
        }

        if (headNames == null || headNames.Length != availableHeads.Length)
        {
            Debug.LogWarning("[CustomisationManager] Head names array doesn't match. Auto-generating names.");
            headNames = new string[availableHeads.Length];

            for (int i = 0; i < availableHeads.Length; i++)
            {
                headNames[i] = availableHeads[i] != null ? availableHeads[i].name : $"Head {i}";
            }
        }

        if (headPreviewSprites != null && headPreviewSprites.Length != availableHeads.Length)
        {
            Debug.LogWarning("[CustomisationManager] Head preview sprites array doesn't match.");
        }

        selectedHeadIndex = Mathf.Clamp(selectedHeadIndex, 0, availableHeads.Length - 1);
    }

    #endregion

    #region UI Management

    private bool HasUIReferences() => materialDropdown != null || headDropdown != null;
    private bool HasValidUIReferences() =>
        (materialDropdown == null || materialDropdown.gameObject != null) &&
        (headDropdown == null || headDropdown.gameObject != null);

    public void OnCustomisationMenuOpened(
        TMP_Dropdown matDropdown = null,
        TMP_Dropdown headDrop = null,
        Image matPreviewImage = null,
        Image headPreview = null)
    {
        if (debugLog)
            Debug.Log("[CustomisationManager] Customisation menu opened");

        UnhookUI();
        if (matDropdown != null) materialDropdown = matDropdown;
        if (headDrop != null) headDropdown = headDrop;
        if (matPreviewImage != null) materialPreviewImage = matPreviewImage;
        if (headPreview != null) headPreviewImage = headPreview;
        HookUI();

        // Refresh color slider UI
        if (colorSliderUI != null)
        {
            colorSliderUI.RefreshFromManager();
        }
    }

    public void OnCustomisationMenuClosed()
    {
        if (debugLog)
            Debug.Log("[CustomisationManager] Customisation menu closed");
    }

    public void ForceRehookUI()
    {
        if (debugLog)
            Debug.Log("[CustomisationManager] Forcing UI re-hook...");

        UnhookUI();

        if (HasUIReferences())
        {
            HookUI();
        }

        // Refresh color sliders
        if (colorSliderUI != null)
        {
            colorSliderUI.TryFindSliders();
            colorSliderUI.RefreshFromManager();
        }
    }

    private void TryFindAndHookUI()
    {
        bool foundAny = false;

        // Find material dropdown
        if (materialDropdown == null && !string.IsNullOrEmpty(materialDropdownName))
        {
            GameObject dropdownObj = GameObject.Find(materialDropdownName);
            if (dropdownObj != null)
            {
                materialDropdown = dropdownObj.GetComponent<TMP_Dropdown>();
                if (materialDropdown != null)
                {
                    if (debugLog)
                        Debug.Log($"[CustomisationManager] Found material dropdown: {materialDropdownName}");
                    foundAny = true;
                }
            }
        }

        // Find head dropdown
        if (headDropdown == null && !string.IsNullOrEmpty(headDropdownName))
        {
            GameObject dropdownObj = GameObject.Find(headDropdownName);
            if (dropdownObj != null)
            {
                headDropdown = dropdownObj.GetComponent<TMP_Dropdown>();
                if (headDropdown != null)
                {
                    if (debugLog)
                        Debug.Log($"[CustomisationManager] Found head dropdown: {headDropdownName}");
                    foundAny = true;
                }
            }
        }

        // Find preview images
        if (materialPreviewImage == null && !string.IsNullOrEmpty(materialPreviewImageName))
        {
            GameObject previewObj = GameObject.Find(materialPreviewImageName);
            if (previewObj != null)
            {
                materialPreviewImage = previewObj.GetComponent<Image>();
                if (materialPreviewImage != null)
                {
                    if (debugLog)
                        Debug.Log($"[CustomisationManager] Found material preview image: {materialPreviewImageName}");
                    foundAny = true;
                }
            }
        }

        if (headPreviewImage == null && !string.IsNullOrEmpty(headPreviewImageName))
        {
            GameObject previewObj = GameObject.Find(headPreviewImageName);
            if (previewObj != null)
            {
                headPreviewImage = previewObj.GetComponent<Image>();
                if (headPreviewImage != null)
                {
                    if (debugLog)
                        Debug.Log($"[CustomisationManager] Found head preview image: {headPreviewImageName}");
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
                if (debugLog)
                    Debug.Log($"[CustomisationManager] Found display object: {displayObjectName}");
                displayObject.SetActive(false);
                ApplyMaterialToDisplayObject();
            }
        }
    }

    private void HookUI()
    {
        if (isUIHooked)
        {
            if (debugLog)
                Debug.Log("[CustomisationManager] UI already hooked, unhooking first...");
            UnhookUI();
        }

        // Hook material dropdown
        if (materialDropdown != null)
        {
            materialDropdown.onValueChanged.RemoveAllListeners();

            if (materialDropdown.options.Count == 0 || materialDropdown.options.Count != materialNames.Length)
            {
                PopulateMaterialDropdown();
            }

            materialDropdown.SetValueWithoutNotify(selectedMaterialIndex);
            materialDropdown.onValueChanged.AddListener(OnMaterialDropdownValueChanged);

            if (debugLog)
                Debug.Log($"[CustomisationManager] Hooked material dropdown, set to index {selectedMaterialIndex}");
        }

        // Hook head dropdown
        if (headDropdown != null)
        {
            headDropdown.onValueChanged.RemoveAllListeners();

            if (headDropdown.options.Count == 0 || headDropdown.options.Count != headNames.Length)
            {
                PopulateHeadDropdown();
            }

            headDropdown.SetValueWithoutNotify(selectedHeadIndex);
            headDropdown.onValueChanged.AddListener(OnHeadDropdownValueChanged);

            if (debugLog)
                Debug.Log($"[CustomisationManager] Hooked head dropdown, set to index {selectedHeadIndex}");
        }

        UpdateMaterialPreview();
        UpdateHeadPreview();
        isUIHooked = true;

        // Notify that we're in custom mode if applicable
        if (IsCustomColourSelected)
        {
            OnCustomColourModeChanged?.Invoke(true);
        }
    }

    private void OnMaterialDropdownValueChanged(int index)
    {
        if (debugLog)
            Debug.Log($"[CustomisationManager] Material dropdown value changed to: {index}");
        SetMaterial(index);
    }

    private void OnHeadDropdownValueChanged(int index)
    {
        if (debugLog)
            Debug.Log($"[CustomisationManager] Head dropdown value changed to: {index}");
        SetHead(index);
    }

    private void UnhookUI()
    {
        if (!isUIHooked) return;

        if (materialDropdown != null)
        {
            materialDropdown.onValueChanged.RemoveAllListeners();
        }

        if (headDropdown != null)
        {
            headDropdown.onValueChanged.RemoveAllListeners();
        }

        isUIHooked = false;

        if (debugLog)
            Debug.Log("[CustomisationManager] UI unhooked");
    }

    private void PopulateMaterialDropdown()
    {
        if (materialDropdown == null || materialNames == null) return;

        materialDropdown.ClearOptions();
        var options = new List<string>(materialNames);
        materialDropdown.AddOptions(options);

        if (debugLog)
            Debug.Log($"[CustomisationManager] Populated material dropdown with {options.Count} options");
    }

    private void PopulateHeadDropdown()
    {
        if (headDropdown == null || headNames == null) return;

        headDropdown.ClearOptions();
        var options = new List<string>(headNames);
        headDropdown.AddOptions(options);

        if (debugLog)
            Debug.Log($"[CustomisationManager] Populated head dropdown with {options.Count} options");
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

    private void UpdateHeadPreview()
    {
        if (headPreviewImage == null) return;

        if (headPreviewSprites != null &&
            selectedHeadIndex >= 0 &&
            selectedHeadIndex < headPreviewSprites.Length &&
            headPreviewSprites[selectedHeadIndex] != null)
        {
            headPreviewImage.sprite = headPreviewSprites[selectedHeadIndex];
            headPreviewImage.gameObject.SetActive(true);
        }
        else
        {
            headPreviewImage.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Display Object Management

    public void SetDisplayObject(GameObject obj)
    {
        displayObject = obj;
        if (displayObject != null)
        {
            if (debugLog)
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
            if (debugLog)
                Debug.Log($"[CustomisationManager] Display object activated");
        }
    }

    public void DeactivateDisplayObject()
    {
        if (displayObject != null)
        {
            displayObject.SetActive(false);
            if (debugLog)
                Debug.Log($"[CustomisationManager] Display object deactivated");
        }
    }

    public void ToggleDisplayObject()
    {
        if (displayObject != null)
        {
            displayObject.SetActive(!displayObject.activeSelf);
        }
    }

    private void ApplyMaterialToDisplayObject()
    {
        if (displayObject == null || SelectedMaterial == null)
            return;

        int appliedCount = 0;

        if (ShouldApplyToObject(displayObject))
        {
            appliedCount += ApplyMaterialToRenderers(displayObject, true);
        }

        if (applyToDisplayChildren)
        {
            Renderer[] childRenderers = displayObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                if (renderer.gameObject != displayObject && ShouldApplyToObject(renderer.gameObject))
                {
                    appliedCount += ApplyMaterialToRenderer(renderer, true);
                }
            }
        }

        if (debugLog)
            Debug.Log($"[CustomisationManager] Applied to {appliedCount} renderer(s) on display object");
    }

    public GameObject GetDisplayObject() => displayObject;
    public bool IsDisplayObjectActive() => displayObject != null && displayObject.activeSelf;

    #endregion

    #region Head Swap Management

    public void SetHead(int index)
    {
        if (index < 0 || index >= availableHeads.Length)
        {
            Debug.LogWarning($"[CustomisationManager] Invalid head index: {index}");
            return;
        }

        selectedHeadIndex = index;
        PlayerPrefs.SetInt("SelectedHead", selectedHeadIndex);
        PlayerPrefs.Save();

        if (debugLog)
            Debug.Log($"[CustomisationManager] Head changed to: {headNames[index]}");

        UpdateHeadPreview();

        if (headDropdown != null && headDropdown.value != index)
        {
            headDropdown.SetValueWithoutNotify(index);
        }

        // Apply head swap then material in sequence
        StartCoroutine(ApplyCustomizationSequence());

        OnHeadChanged?.Invoke(GetCurrentHead(), selectedHeadIndex);
    }

    /// <summary>
    /// Applies head swap to the player character
    /// </summary>
    public void ApplyHeadSwapToPlayer()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] Cannot apply head swap - player not found");
            return;
        }

        ApplyHeadSwapToGameObject(player);
    }

    /// <summary>
    /// Applies head swap to a specific GameObject
    /// </summary>
    public void ApplyHeadSwapToGameObject(GameObject target)
    {
        if (target == null)
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] Cannot apply head swap - target is null");
            return;
        }

        if (availableHeads == null || availableHeads.Length == 0)
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] No heads available to swap");
            return;
        }

        if (selectedHeadIndex < 0 || selectedHeadIndex >= availableHeads.Length)
        {
            Debug.LogWarning($"[CustomisationManager] Invalid head index: {selectedHeadIndex}");
            return;
        }

        GameObject headPrefab = availableHeads[selectedHeadIndex];
        if (headPrefab == null)
        {
            Debug.LogWarning($"[CustomisationManager] Head prefab at index {selectedHeadIndex} is null");
            return;
        }

        if (debugLog)
            Debug.Log($"[CustomisationManager] === STARTING HEAD SWAP on {target.name} ===");

        // STEP 1: Find original head
        Transform originalHead = FindOriginalHead(target);
        if (originalHead == null)
        {
            Debug.LogError($"[CustomisationManager] Could not find original head '{originalHeadName}' in {target.name}");
            return;
        }

        if (debugLog)
            Debug.Log($"[CustomisationManager] Found original head: {originalHead.name}");

        // STEP 2: Store original head transform data
        originalHeadParent = originalHead.parent;
        originalHeadLocalPosition = originalHead.localPosition;
        originalHeadLocalRotation = originalHead.localRotation;
        originalHeadLocalScale = originalHead.localScale;

        if (debugLog)
        {
            Debug.Log($"[CustomisationManager] Original head parent: {originalHeadParent?.name ?? "NULL"}");
            Debug.Log($"[CustomisationManager] Original local position: {originalHeadLocalPosition}");
            Debug.Log($"[CustomisationManager] Original local rotation: {originalHeadLocalRotation.eulerAngles}");
        }

        // STEP 3: Remove/hide original head
        originalHead.gameObject.SetActive(false);

        if (debugLog)
            Debug.Log($"[CustomisationManager] Disabled original head: {originalHead.name}");

        // STEP 4: Destroy old swapped head if exists
        if (currentSwappedHead != null)
        {
            if (debugLog)
                Debug.Log($"[CustomisationManager] Destroying previous swapped head: {currentSwappedHead.name}");
            Destroy(currentSwappedHead);
            currentSwappedHead = null;
        }

        // STEP 5: Instantiate new head
        currentSwappedHead = Instantiate(headPrefab, originalHeadParent);
        currentSwappedHead.name = $"{headPrefab.name}_Swapped";

        if (debugLog)
            Debug.Log($"[CustomisationManager] Instantiated new head: {currentSwappedHead.name}");

        // STEP 6: Position new head
        currentSwappedHead.transform.localPosition = originalHeadLocalPosition + headPositionOffset;
        currentSwappedHead.transform.localRotation = originalHeadLocalRotation * Quaternion.Euler(headRotationOffset);

        // Apply scale
        if (headScaleOverride != Vector3.one)
        {
            currentSwappedHead.transform.localScale = headScaleOverride;
        }
        else
        {
            currentSwappedHead.transform.localScale = originalHeadLocalScale;
        }

        if (debugLog)
        {
            Debug.Log($"[CustomisationManager] New head position: {currentSwappedHead.transform.localPosition}");
            Debug.Log($"[CustomisationManager] New head rotation: {currentSwappedHead.transform.localRotation.eulerAngles}");
            Debug.Log($"[CustomisationManager] New head scale: {currentSwappedHead.transform.localScale}");
        }

        // STEP 7: Apply material to new head if enabled
        if (applyMaterialToHead && SelectedMaterial != null)
        {
            if (debugLog)
                Debug.Log("[CustomisationManager] Applying material to swapped head...");

            Renderer[] renderers = currentSwappedHead.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                ApplyMaterialToRenderer(renderer, false);
            }

            if (debugLog)
                Debug.Log($"[CustomisationManager] Applied material to {renderers.Length} renderer(s) on new head");
        }

        if (debugLog)
            Debug.Log($"[CustomisationManager] === HEAD SWAP COMPLETE ===");
    }

    /// <summary>
    /// Finds the original head object to replace
    /// </summary>
    private Transform FindOriginalHead(GameObject target)
    {
        if (string.IsNullOrEmpty(originalHeadName))
        {
            Debug.LogWarning("[CustomisationManager] Original head name not set!");
            return null;
        }

        // Search by exact name first
        Transform[] allChildren = target.GetComponentsInChildren<Transform>(true);

        // Exact match
        foreach (Transform child in allChildren)
        {
            if (child.name == originalHeadName)
            {
                return child;
            }
        }

        // Contains match (e.g., "sk_wizard_head" contains "head")
        foreach (Transform child in allChildren)
        {
            if (child.name.ToLower().Contains(originalHeadName.ToLower()))
            {
                if (debugLog)
                    Debug.Log($"[CustomisationManager] Found head by partial match: {child.name}");
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the currently active head GameObject (swapped or original)
    /// </summary>
    public GameObject GetCurrentHead()
    {
        return currentSwappedHead;
    }

    /// <summary>
    /// Restores the original head (removes swap)
    /// </summary>
    public void RestoreOriginalHead()
    {
        GameObject player = FindPlayer();
        if (player == null) return;

        Transform originalHead = FindOriginalHead(player);
        if (originalHead != null)
        {
            originalHead.gameObject.SetActive(true);
            if (debugLog)
                Debug.Log("[CustomisationManager] Restored original head");
        }

        if (currentSwappedHead != null)
        {
            Destroy(currentSwappedHead);
            currentSwappedHead = null;
            if (debugLog)
                Debug.Log("[CustomisationManager] Removed swapped head");
        }
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

        if (debugLog)
            Debug.Log($"[CustomisationManager] Material changed to: {materialNames[index]} (Custom Mode: {isCustom})");

        UpdateMaterialPreview();

        if (materialDropdown != null && materialDropdown.value != index)
        {
            materialDropdown.SetValueWithoutNotify(index);
        }

        ApplyMaterialToPlayer();

        OnMaterialChanged?.Invoke(SelectedMaterial, selectedMaterialIndex);

        if (wasCustom != isCustom)
        {
            OnCustomColourModeChanged?.Invoke(isCustom);

            // Refresh color slider UI when mode changes
            if (colorSliderUI != null)
            {
                colorSliderUI.RefreshFromManager();
            }
        }

        ApplyMaterialToDisplayObject();
    }

    #endregion

    #region Custom Color (HSV)

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

        if (debugLog)
            Debug.Log("[CustomisationManager] Created runtime custom material");
    }

    private void ApplyCustomColourToMaterial()
    {
        if (runtimeCustomMaterial == null)
            return;

        Color c = Color.HSVToRGB(customHue, customSaturation, customValue);
        c.a = customAlpha;

        if (runtimeCustomMaterial.HasProperty("_BaseColor"))
            runtimeCustomMaterial.SetColor("_BaseColor", c);
        if (runtimeCustomMaterial.HasProperty("_Color"))
            runtimeCustomMaterial.SetColor("_Color", c);
        if (runtimeCustomMaterial.HasProperty("_TintColor"))
            runtimeCustomMaterial.SetColor("_TintColor", c);
    }

    public void SetCustomColorHSV(float h, float s, float v, float a = 1f)
    {
        if (!IsCustomColourSelected)
        {
            if (debugLog)
                Debug.LogWarning($"[CustomisationManager] SetCustomColorHSV called but custom colour not selected! Current index: {selectedMaterialIndex}");
            return;
        }

        customHue = Mathf.Clamp01(h);
        customSaturation = Mathf.Clamp01(s);
        customValue = Mathf.Clamp01(v);
        customAlpha = Mathf.Clamp01(a);

        if (debugLog)
            Debug.Log($"[CustomisationManager] *** SETTING CUSTOM COLOR HSV *** H={h:F2}, S={s:F2}, V={v:F2}, A={a:F2}");

        SaveCustomColor();

        EnsureRuntimeCustomMaterial();
        ApplyCustomColourToMaterial();

        Color rgb = Color.HSVToRGB(customHue, customSaturation, customValue);
        rgb.a = customAlpha;

        if (debugLog)
            Debug.Log($"[CustomisationManager] Applying custom color to player and display object...");

        ApplyMaterialToPlayer();
        ApplyMaterialToDisplayObject();

        OnCustomColourChanged?.Invoke(rgb);

        if (debugLog)
            Debug.Log($"[CustomisationManager] Custom color applied successfully!");
    }

    public void SetCustomColorRGB(Color color)
    {
        if (!IsCustomColourSelected)
            return;

        Color.RGBToHSV(color, out customHue, out customSaturation, out customValue);
        customAlpha = color.a;

        if (debugLog)
            Debug.Log($"[CustomisationManager] Custom color RGB set: {color}");

        SaveCustomColor();

        EnsureRuntimeCustomMaterial();
        ApplyCustomColourToMaterial();

        ApplyMaterialToPlayer();
        ApplyMaterialToDisplayObject();

        OnCustomColourChanged?.Invoke(color);
    }

    public Color GetCustomColorRGB()
    {
        Color c = Color.HSVToRGB(customHue, customSaturation, customValue);
        c.a = customAlpha;
        return c;
    }

    public void GetCustomColorHSV(out float h, out float s, out float v, out float a)
    {
        h = customHue;
        s = customSaturation;
        v = customValue;
        a = customAlpha;
    }

    private void SaveCustomColor()
    {
        PlayerPrefs.SetFloat("CustomHue", customHue);
        PlayerPrefs.SetFloat("CustomSaturation", customSaturation);
        PlayerPrefs.SetFloat("CustomValue", customValue);
        PlayerPrefs.SetFloat("CustomAlpha", customAlpha);
        PlayerPrefs.Save();
    }

    private void LoadCustomColor()
    {
        customHue = PlayerPrefs.GetFloat("CustomHue", 0f);
        customSaturation = PlayerPrefs.GetFloat("CustomSaturation", 1f);
        customValue = PlayerPrefs.GetFloat("CustomValue", 1f);
        customAlpha = PlayerPrefs.GetFloat("CustomAlpha", 1f);

        if (debugLog)
            Debug.Log($"[CustomisationManager] Loaded custom color: H={customHue:F2}, S={customSaturation:F2}, V={customValue:F2}");
    }

    #endregion

    #region Material Application

    private bool ShouldApplyCustomColour(Renderer renderer)
    {
        if (renderer == null)
            return false;

        GameObject obj = renderer.gameObject;

        if (!string.IsNullOrEmpty(customColourTargetTag) &&
            !obj.CompareTag(customColourTargetTag))
            return false;

        if (((1 << obj.layer) & customColourLayerMask) == 0)
            return false;

        return true;
    }

    public void ApplyMaterialToPlayer()
    {
        GameObject player = FindPlayer();
        if (player == null)
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] Player not found.");
            return;
        }

        ApplyMaterialToGameObject(player);
    }

    public void ApplyMaterialToGameObject(GameObject target)
    {
        if (target == null || SelectedMaterial == null)
        {
            if (debugLog)
                Debug.LogWarning("[CustomisationManager] Cannot apply material - target or material is null.");
            return;
        }

        int appliedCount = 0;

        if (ShouldApplyToObject(target))
        {
            appliedCount += ApplyMaterialToRenderers(target, false);
        }

        if (applyToChildren)
        {
            Renderer[] childRenderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                if (renderer.gameObject != target && ShouldApplyToObject(renderer.gameObject))
                {
                    appliedCount += ApplyMaterialToRenderer(renderer, false);
                }
            }
        }

        if (debugLog)
            Debug.Log($"[CustomisationManager] Applied to {appliedCount} renderer(s) on {target.name}");
    }

    private bool ShouldApplyToObject(GameObject obj)
    {
        if (customizationLayerMask != ~0 && ((1 << obj.layer) & customizationLayerMask) == 0)
            return false;

        if (!string.IsNullOrEmpty(childFilterTag) && !obj.CompareTag(childFilterTag))
            return false;

        return true;
    }

    private int ApplyMaterialToRenderers(GameObject obj, bool isDisplayObject)
    {
        int count = 0;
        Renderer[] renderers = obj.GetComponents<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            count += ApplyMaterialToRenderer(renderer, isDisplayObject);
        }
        return count;
    }

    private int ApplyMaterialToRenderer(Renderer renderer, bool isDisplayObject)
    {
        if (renderer == null)
            return 0;

        Material baseMat = SelectedMaterial;
        if (baseMat == null)
            return 0;

        Material[] mats = renderer.sharedMaterials;

        if (!IsCustomColourSelected)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i] = baseMat;

            renderer.sharedMaterials = mats;
            return mats.Length;
        }

        EnsureRuntimeCustomMaterial();
        ApplyCustomColourToMaterial();

        bool shouldRecolour = ShouldApplyCustomColour(renderer);

        for (int i = 0; i < mats.Length; i++)
        {
            if (customColourMaterialSlot >= 0 && i != customColourMaterialSlot)
            {
                mats[i] = baseMat;
                continue;
            }

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

    private GameObject FindPlayer()
    {
        if (cachedPlayer != null)
            return cachedPlayer;

        if (!string.IsNullOrEmpty(playerTag))
        {
            cachedPlayer = GameObject.FindGameObjectWithTag(playerTag);
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
            Debug.LogWarning("[CustomisationManager] Attempted to register null GameObject.");
            return;
        }

        cachedPlayer = player;
        if (debugLog)
            Debug.Log($"[CustomisationManager] Registered '{player.name}' as player target.");

        // Apply in sequence
        StartCoroutine(ApplyCustomizationSequence());
    }

    #endregion

    #region Loading and Saving

    private void LoadSettings()
    {
        selectedMaterialIndex = PlayerPrefs.GetInt("SelectedMaterial", 0);
        selectedHeadIndex = PlayerPrefs.GetInt("SelectedHead", 0);

        if (availableMaterials != null)
        {
            int maxIndex = availableMaterials.Length + (customColourBaseMaterial != null ? 1 : 0) - 1;
            selectedMaterialIndex = Mathf.Clamp(selectedMaterialIndex, 0, maxIndex);
        }

        if (availableHeads != null && availableHeads.Length > 0)
        {
            selectedHeadIndex = Mathf.Clamp(selectedHeadIndex, 0, availableHeads.Length - 1);
        }

        LoadCustomColor();

        if (debugLog)
            Debug.Log($"[CustomisationManager] Loaded settings - Material: {selectedMaterialIndex}, Head: {selectedHeadIndex}");
    }

    public void ResetToDefault()
    {
        SetHead(0);
        SetMaterial(0);

        if (debugLog)
            Debug.Log("[CustomisationManager] Reset to default");
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

    public string GetSelectedHeadName()
    {
        if (headNames != null && selectedHeadIndex >= 0 && selectedHeadIndex < headNames.Length)
        {
            return headNames[selectedHeadIndex];
        }
        return "Unknown";
    }

    public int GetMaterialCount() => availableMaterials?.Length ?? 0;
    public int GetHeadCount() => availableHeads?.Length ?? 0;

    public Material GetMaterialAtIndex(int index)
    {
        if (availableMaterials != null && index >= 0 && index < availableMaterials.Length)
        {
            return availableMaterials[index];
        }
        return null;
    }

    public GameObject GetHeadAtIndex(int index)
    {
        if (availableHeads != null && index >= 0 && index < availableHeads.Length)
        {
            return availableHeads[index];
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

    public string GetHeadNameAtIndex(int index)
    {
        if (headNames != null && index >= 0 && index < headNames.Length)
        {
            return headNames[index];
        }
        return "Unknown";
    }

    #endregion

    #region Context Menu Helpers
#if UNITY_EDITOR
    [ContextMenu("Apply Head Swap Now")]
    private void DebugApplyHeadSwap()
    {
        ApplyHeadSwapToPlayer();
    }

    [ContextMenu("Apply Material Now")]
    private void DebugApplyMaterial()
    {
        ApplyMaterialToPlayer();
    }

    [ContextMenu("Apply Full Customization Sequence")]
    private void DebugApplyFullSequence()
    {
        StartCoroutine(ApplyCustomizationSequence());
    }

    [ContextMenu("Restore Original Head")]
    private void DebugRestoreOriginalHead()
    {
        RestoreOriginalHead();
    }
#endif
    #endregion
}