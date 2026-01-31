using UnityEngine;
using Unity.Netcode;
using NUnit.Framework;
using System.Collections.Generic;

/// <summary>
/// Handles networked player customisation - syncs material and color choices across clients
/// Attach this to your networked player prefab
/// </summary>
public class NetworkedPlayerCustomisation : NetworkBehaviour
{
    [Header("Customisation Settings")]
    [Tooltip("Apply customisation on spawn")]
    [SerializeField] private bool applyOnSpawn = true;

    [Tooltip("Apply material to child objects as well")]
    [SerializeField] private bool applyToChildren = true;

    [Tooltip("Only apply to objects with specific tag (leave empty for all children)")]
    [SerializeField] private string childFilterTag = "";

    [Tooltip("Layer mask for objects to customize")]
    [SerializeField] private LayerMask customizationLayerMask = ~0;

    [Header("Custom Color Filtering")]
    [Tooltip("Tag for objects that should receive custom coloring")]
    [SerializeField] private string customColourTargetTag = "CustomColour";

    [Tooltip("Layers for objects that should receive custom coloring")]
    [SerializeField] private LayerMask customColourLayerMask = ~0;

    [Tooltip("Material slot to apply custom color (-1 = all slots)")]
    [SerializeField] private int customColourMaterialSlot = -1;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    [Header("Spell Customisation")]
    //public List<GameObject> playerPrefabs;

    // NetworkVariables to sync customisation across clients
    private NetworkVariable<int> materialIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // Custom color stored as floats (HSV + Alpha)
    private NetworkVariable<float> customHue = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<float> customSaturation = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<float> customValue = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<float> customAlpha = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Material runtimeCustomMaterial;
    private bool hasAppliedCustomisation = false;

    public NetworkVariable<bool> fireball = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> ice = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> lightning = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> shadow = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> green = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> stone = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> chronos = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> gemini = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> pisces = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> jolly = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> blink = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> warp = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> tether = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> mud = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> gambit = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    public NetworkVariable<bool> gorbino = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Owner: Load saved customisation and sync to network
            LoadAndSyncCustomisation();
        }
        else
        {
            // Non-owner: Listen for changes from owner
            materialIndex.OnValueChanged += OnMaterialIndexChanged;
            customHue.OnValueChanged += OnCustomColorChanged;
            customSaturation.OnValueChanged += OnCustomColorChanged;
            customValue.OnValueChanged += OnCustomColorChanged;
            customAlpha.OnValueChanged += OnCustomColorChanged;
        }

        // Apply customisation for both owner and non-owner
        if (applyOnSpawn)
        {
            ApplyCustomisation();
        }

        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Spawned for {(IsOwner ? "Owner" : "Remote")} - ClientId: {OwnerClientId}");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            materialIndex.OnValueChanged -= OnMaterialIndexChanged;
            customHue.OnValueChanged -= OnCustomColorChanged;
            customSaturation.OnValueChanged -= OnCustomColorChanged;
            customValue.OnValueChanged -= OnCustomColorChanged;
            customAlpha.OnValueChanged -= OnCustomColorChanged;
        }

        // Clean up runtime material
        if (runtimeCustomMaterial != null)
        {
            Destroy(runtimeCustomMaterial);
            runtimeCustomMaterial = null;
        }

        base.OnNetworkDespawn();
    }

    private void LoadAndSyncCustomisation()
    {
        if (!IsOwner || CustomisationManager.Instance == null)
            return;

        // Load material index
        int savedIndex = CustomisationManager.Instance.SelectedMaterialIndex;
        materialIndex.Value = savedIndex;

        // Load custom color if in custom mode
        if (IsCustomColorMode(savedIndex))
        {
            CustomisationManager.Instance.GetCustomColorHSV(out float h, out float s, out float v, out float a);
            customHue.Value = h;
            customSaturation.Value = s;
            customValue.Value = v;
            customAlpha.Value = a;
        }

        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Loaded customisation - Material: {savedIndex}, HSV: ({customHue.Value:F2}, {customSaturation.Value:F2}, {customValue.Value:F2})");

        if (OptionsManager.Instance != null)
        {
            if (!OptionsManager.Instance.FireballBool) fireball.Value = false;
            if (!OptionsManager.Instance.IceBool) ice.Value = false;
            if (!OptionsManager.Instance.LightningBool) lightning.Value = false;
            if (!OptionsManager.Instance.ShadowBool) shadow.Value = false;
            if (!OptionsManager.Instance.GreenBool) green.Value = false;
            if (!OptionsManager.Instance.StoneBool) stone.Value = false;
            if (!OptionsManager.Instance.ChronosBool) chronos.Value = false;
            if (!OptionsManager.Instance.GeminiBool) gemini.Value = false;
            if (!OptionsManager.Instance.PiscesBool) pisces.Value = false;
            if (!OptionsManager.Instance.JollyBool) jolly.Value = false;
            if (!OptionsManager.Instance.BlinkBool) blink.Value = false;
            if (!OptionsManager.Instance.WarpBool) warp.Value = false;
            if (!OptionsManager.Instance.TetherBool) tether.Value = false;
            if (!OptionsManager.Instance.MudBool) mud.Value = false;
            if (!OptionsManager.Instance.GambitBool) gambit.Value = false;
            if (!OptionsManager.Instance.GorbinoBool) gorbino.Value = false;
        }
    }

    private void OnMaterialIndexChanged(int previousValue, int newValue)
    {
        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Material index changed from {previousValue} to {newValue} for player {OwnerClientId}");

        ApplyCustomisation();
    }

    private void OnCustomColorChanged(float previousValue, float newValue)
    {
        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Custom color changed for player {OwnerClientId}");

        // Only re-apply if in custom color mode
        if (IsCustomColorMode(materialIndex.Value))
        {
            ApplyCustomisation();
        }
    }

    private void ApplyCustomisation()
    {
        if (CustomisationManager.Instance == null)
        {
            Debug.LogWarning("[NetworkedCustomisation] CustomisationManager not found!");
            return;
        }

        int matIndex = materialIndex.Value;
        bool isCustomMode = IsCustomColorMode(matIndex);

        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Applying customisation - Index: {matIndex}, Custom: {isCustomMode}");

        if (isCustomMode)
        {
            ApplyCustomColorMaterial();
        }
        else
        {
            ApplyStandardMaterial(matIndex);
        }

        hasAppliedCustomisation = true;
    }

    private bool IsCustomColorMode(int index)
    {
        if (CustomisationManager.Instance == null)
            return false;

        return index >= CustomisationManager.Instance.GetMaterialCount();
    }

    private void ApplyStandardMaterial(int index)
    {
        Material mat = CustomisationManager.Instance.GetMaterialAtIndex(index);
        if (mat == null)
        {
            Debug.LogWarning($"[NetworkedCustomisation] Invalid material index: {index}");
            return;
        }

        ApplyMaterialToAllRenderers(mat, false);
    }

    private void ApplyCustomColorMaterial()
    {
        // Create runtime material instance
        EnsureRuntimeCustomMaterial();

        if (runtimeCustomMaterial == null)
            return;

        // Apply HSV color to material
        Color c = Color.HSVToRGB(customHue.Value, customSaturation.Value, customValue.Value);
        c.a = customAlpha.Value;

        if (runtimeCustomMaterial.HasProperty("_BaseColor"))
            runtimeCustomMaterial.SetColor("_BaseColor", c);
        if (runtimeCustomMaterial.HasProperty("_Color"))
            runtimeCustomMaterial.SetColor("_Color", c);
        if (runtimeCustomMaterial.HasProperty("_TintColor"))
            runtimeCustomMaterial.SetColor("_TintColor", c);

        ApplyMaterialToAllRenderers(runtimeCustomMaterial, true);

        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Applied custom color: {c}");
    }

    private void EnsureRuntimeCustomMaterial()
    {
        if (runtimeCustomMaterial != null)
            return;

        if (CustomisationManager.Instance == null)
            return;

        // Get base material from manager (this handles the custom color base material)
        Material baseMat = CustomisationManager.Instance.SelectedMaterial;
        if (baseMat == null)
        {
            Debug.LogWarning("[NetworkedCustomisation] No custom color base material found!");
            return;
        }

        // Create instance for this player
        runtimeCustomMaterial = new Material(baseMat);
        runtimeCustomMaterial.name = $"{baseMat.name}_Player{OwnerClientId}";
    }

    private void ApplyMaterialToAllRenderers(Material mat, bool isCustomColor)
    {
        int appliedCount = 0;

        // Apply to this object
        if (ShouldApplyToObject(gameObject))
        {
            appliedCount += ApplyMaterialToRenderers(gameObject, mat, isCustomColor);
        }

        // Apply to children
        if (applyToChildren)
        {
            Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in childRenderers)
            {
                if (renderer.gameObject != gameObject && ShouldApplyToObject(renderer.gameObject))
                {
                    appliedCount += ApplyMaterialToRenderer(renderer, mat, isCustomColor);
                }
            }
        }

        if (debugLog)
            Debug.Log($"[NetworkedCustomisation] Applied material to {appliedCount} renderer(s)");
    }

    private bool ShouldApplyToObject(GameObject obj)
    {
        if (customizationLayerMask != ~0 && ((1 << obj.layer) & customizationLayerMask) == 0)
            return false;

        if (!string.IsNullOrEmpty(childFilterTag) && !obj.CompareTag(childFilterTag))
            return false;

        return true;
    }

    private bool ShouldApplyCustomColor(GameObject obj)
    {
        if (!string.IsNullOrEmpty(customColourTargetTag) && !obj.CompareTag(customColourTargetTag))
            return false;

        if (((1 << obj.layer) & customColourLayerMask) == 0)
            return false;

        return true;
    }

    private int ApplyMaterialToRenderers(GameObject obj, Material mat, bool isCustomColor)
    {
        int count = 0;
        Renderer[] renderers = obj.GetComponents<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            count += ApplyMaterialToRenderer(renderer, mat, isCustomColor);
        }
        return count;
    }

    private int ApplyMaterialToRenderer(Renderer renderer, Material mat, bool isCustomColor)
    {
        if (renderer == null)
            return 0;

        Material[] mats = renderer.sharedMaterials;

        if (!isCustomColor)
        {
            // Standard material - apply to all slots
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
        }
        else
        {
            // Custom color - apply based on filtering
            bool shouldRecolor = ShouldApplyCustomColor(renderer.gameObject);

            for (int i = 0; i < mats.Length; i++)
            {
                // If slot filtering is enabled
                if (customColourMaterialSlot >= 0 && i != customColourMaterialSlot)
                    continue;

                if (shouldRecolor)
                {
                    mats[i] = mat;
                }
            }
        }

        renderer.sharedMaterials = mats;
        return mats.Length;
    }

    /// <summary>
    /// Public method to change material at runtime (owner only)
    /// </summary>
    public void ChangeMaterial(int newMaterialIndex)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("[NetworkedCustomisation] Only the owner can change their material!");
            return;
        }

        materialIndex.Value = newMaterialIndex;
    }

    /// <summary>
    /// Public method to change custom color at runtime (owner only)
    /// </summary>
    public void ChangeCustomColor(float h, float s, float v, float a = 1f)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("[NetworkedCustomisation] Only the owner can change their color!");
            return;
        }

        if (!IsCustomColorMode(materialIndex.Value))
        {
            Debug.LogWarning("[NetworkedCustomisation] Not in custom color mode!");
            return;
        }

        customHue.Value = Mathf.Clamp01(h);
        customSaturation.Value = Mathf.Clamp01(s);
        customValue.Value = Mathf.Clamp01(v);
        customAlpha.Value = Mathf.Clamp01(a);
    }

    /// <summary>
    /// Get the current material index for this player
    /// </summary>
    public int GetMaterialIndex() => materialIndex.Value;

    /// <summary>
    /// Get the current custom color for this player (RGB)
    /// </summary>
    public Color GetCustomColor()
    {
        Color c = Color.HSVToRGB(customHue.Value, customSaturation.Value, customValue.Value);
        c.a = customAlpha.Value;
        return c;
    }

    /// <summary>
    /// Force re-apply customisation (useful for debugging or manual refresh)
    /// </summary>
    [ContextMenu("Force Reapply Customisation")]
    public void ForceReapply()
    {
        ApplyCustomisation();
    }

    //private void Awake()
    //{
    //    GameManager gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
    //    if (OptionsManager.Instance != null)
    //    {
    //        if (OptionsManager.Instance.FireballBool) playerPrefabs.Add(gameManager.pickupPrefabs[0]);
    //        if (OptionsManager.Instance.IceBool) playerPrefabs.Add(gameManager.pickupPrefabs[1]);
    //        if (OptionsManager.Instance.LightningBool) playerPrefabs.Add(gameManager.pickupPrefabs[2]);
    //        if (OptionsManager.Instance.ShadowBool) playerPrefabs.Add(gameManager.pickupPrefabs[3]);
    //        if (OptionsManager.Instance.GreenBool) playerPrefabs.Add(gameManager.pickupPrefabs[4]);
    //        if (OptionsManager.Instance.StoneBool) playerPrefabs.Add(gameManager.pickupPrefabs[5]);
    //        if (OptionsManager.Instance.ChronosBool) playerPrefabs.Add(gameManager.pickupPrefabs[6]);
    //        if (OptionsManager.Instance.GeminiBool) playerPrefabs.Add(gameManager.pickupPrefabs[7]);
    //        if (OptionsManager.Instance.PiscesBool) playerPrefabs.Add(gameManager.pickupPrefabs[8]);
    //        if (OptionsManager.Instance.JollyBool) playerPrefabs.Add(gameManager.pickupPrefabs[9]);
    //        if (OptionsManager.Instance.BlinkBool) playerPrefabs.Add(gameManager.pickupPrefabs[10]);
    //        if (OptionsManager.Instance.WarpBool) playerPrefabs.Add(gameManager.pickupPrefabs[11]);
    //        if (OptionsManager.Instance.TetherBool) playerPrefabs.Add(gameManager.pickupPrefabs[12]);
    //        if (OptionsManager.Instance.MudBool) playerPrefabs.Add(gameManager.pickupPrefabs[13]);
    //        if (OptionsManager.Instance.GambitBool) playerPrefabs.Add(gameManager.pickupPrefabs[14]);
    //        if (OptionsManager.Instance.GorbinoBool) playerPrefabs.Add(gameManager.pickupPrefabs[15]);
    //    }
    //    else if (OptionsManager.Instance == null)
    //    {
    //        playerPrefabs = gameManager.pickupPrefabs;
    //    }
    //}
}