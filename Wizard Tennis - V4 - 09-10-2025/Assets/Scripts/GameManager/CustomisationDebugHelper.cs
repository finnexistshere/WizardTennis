using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug helper to verify CustomisationManager events are firing correctly
/// Attach this to any GameObject in your scene
/// </summary>
public class CustomisationDebugHelper : MonoBehaviour
{
    [Header("Test Panel (Optional)")]
    [SerializeField] private GameObject colorSlidersPanel;

    [Header("Debug Options")]
    [SerializeField] private bool logEvents = true;
    [SerializeField] private bool autoRefreshSliders = true;

    private void OnEnable()
    {
        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged += OnModeChanged;
            CustomisationManager.Instance.OnMaterialChanged += OnMaterialChanged;

            Debug.Log($"<color=green>[CustomisationDebug] Subscribed to events. Current mode: Custom={CustomisationManager.Instance.IsCustomColourSelected}</color>");
        }
        else
        {
            Debug.LogError("[CustomisationDebug] CustomisationManager.Instance is NULL!");
        }
    }

    private void OnDisable()
    {
        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnModeChanged;
            CustomisationManager.Instance.OnMaterialChanged -= OnMaterialChanged;
        }
    }

    private void OnModeChanged(bool isCustomMode)
    {
        if (logEvents)
        {
            Debug.Log($"<color=cyan>[CustomisationDebug] ? CUSTOM MODE CHANGED: {isCustomMode}</color>");
        }

        // Show/hide sliders panel if assigned
        if (colorSlidersPanel != null)
        {
            colorSlidersPanel.SetActive(isCustomMode);
            Debug.Log($"<color=yellow>[CustomisationDebug] Sliders panel active: {isCustomMode}</color>");
        }

        // Force refresh all slider UIs
        if (autoRefreshSliders)
        {
            RefreshAllSliderUIs();
        }
    }

    private void OnMaterialChanged(Material material, int index)
    {
        if (logEvents)
        {
            Debug.Log($"<color=magenta>[CustomisationDebug] Material changed to index {index}: {(material != null ? material.name : "NULL")}</color>");
        }
    }

    /// <summary>
    /// Force all MaterialChannelSliderUI components to refresh
    /// </summary>
    public void RefreshAllSliderUIs()
    {
        var sliders = FindObjectsOfType<MaterialChannelSliderUI>(true);
        Debug.Log($"<color=yellow>[CustomisationDebug] Found {sliders.Length} slider UIs, refreshing...</color>");

        foreach (var slider in sliders)
        {
            slider.RefreshFromManager();
        }
    }

    /// <summary>
    /// Manual test button - force enable custom mode
    /// </summary>
    [ContextMenu("Force Enable Custom Mode")]
    public void ForceEnableCustomMode()
    {
        if (CustomisationManager.Instance != null)
        {
            int customIndex = CustomisationManager.Instance.GetMaterialCount();
            Debug.Log($"<color=green>[CustomisationDebug] Forcing custom mode (index {customIndex})</color>");
            CustomisationManager.Instance.SetMaterial(customIndex);
        }
    }

    /// <summary>
    /// Manual test button - check current state
    /// </summary>
    [ContextMenu("Log Current State")]
    public void LogCurrentState()
    {
        if (CustomisationManager.Instance == null)
        {
            Debug.LogError("[CustomisationDebug] CustomisationManager.Instance is NULL!");
            return;
        }

        Debug.Log($"<color=cyan>====== CUSTOMISATION STATE ======</color>");
        Debug.Log($"Selected Index: {CustomisationManager.Instance.SelectedMaterialIndex}");
        Debug.Log($"Material Count: {CustomisationManager.Instance.GetMaterialCount()}");
        Debug.Log($"Is Custom Mode: {CustomisationManager.Instance.IsCustomColourSelected}");
        Debug.Log($"Selected Material: {(CustomisationManager.Instance.SelectedMaterial != null ? CustomisationManager.Instance.SelectedMaterial.name : "NULL")}");
        Debug.Log($"<color=cyan>=================================</color>");
    }
}