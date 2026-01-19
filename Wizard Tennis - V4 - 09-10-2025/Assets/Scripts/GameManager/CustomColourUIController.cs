using UnityEngine;
using UnityEngine.UI;

public class MaterialChannelSliderUI : MonoBehaviour
{
    [Header("Channel")]
    [SerializeField] private int channelIndex;

    [Header("RGB Sliders (0–1)")]
    [SerializeField] private Slider sliderR;
    [SerializeField] private Slider sliderG;
    [SerializeField] private Slider sliderB;

    [Header("Optional: Color Preview")]
    [SerializeField] private Image colorPreview;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private bool suppressEvents;

    private void OnEnable()
    {
        // Wait one frame to ensure CustomisationManager is ready
        StartCoroutine(DelayedInitialize());
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        yield return null; // Wait one frame

        HookSliders();

        // Subscribe to custom color mode changes
        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnCustomModeChanged;
            CustomisationManager.Instance.OnCustomColourModeChanged += OnCustomModeChanged;
        }
        else
        {
            Debug.LogWarning("[MaterialChannelSlider] CustomisationManager.Instance is null!");
        }

        // Initial sync and state update
        SyncFromChannel();
        UpdateEnabledState();

        if (debugLog)
            Debug.Log($"[MaterialChannelSlider] Initialized. Custom mode: {(CustomisationManager.Instance != null ? CustomisationManager.Instance.IsCustomColourSelected.ToString() : "N/A")}");
    }

    private void OnDisable()
    {
        UnhookSliders();

        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnCustomModeChanged;
        }
    }

    private void HookSliders()
    {
        if (sliderR != null)
        {
            sliderR.onValueChanged.RemoveAllListeners();
            sliderR.onValueChanged.AddListener(OnSliderChanged);
        }

        if (sliderG != null)
        {
            sliderG.onValueChanged.RemoveAllListeners();
            sliderG.onValueChanged.AddListener(OnSliderChanged);
        }

        if (sliderB != null)
        {
            sliderB.onValueChanged.RemoveAllListeners();
            sliderB.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void UnhookSliders()
    {
        if (sliderR != null)
            sliderR.onValueChanged.RemoveListener(OnSliderChanged);
        if (sliderG != null)
            sliderG.onValueChanged.RemoveListener(OnSliderChanged);
        if (sliderB != null)
            sliderB.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void OnSliderChanged(float _)
    {
        if (CustomisationManager.Instance == null)
            return;

        if (!CustomisationManager.Instance.IsCustomColourSelected)
            return;

        Color color = new Color(
            sliderR.value,
            sliderG.value,
            sliderB.value,
            1f
        );

        CustomisationManager.Instance.SetCustomColour(color);
        UpdateColorPreview(color);
    }

    private void SyncFromChannel()
    {
        if (CustomisationManager.Instance == null)
        {
            Debug.LogWarning("[MaterialChannelSlider] CustomisationManager not found!");
            return;
        }

        var channel = CustomisationManager.Instance.GetChannel(channelIndex);
        if (channel == null)
        {
            Debug.LogWarning($"[MaterialChannelSlider] Channel {channelIndex} not found!");
            return;
        }

        suppressEvents = true;

        if (sliderR != null)
            sliderR.value = channel.color.r;
        if (sliderG != null)
            sliderG.value = channel.color.g;
        if (sliderB != null)
            sliderB.value = channel.color.b;

        suppressEvents = false;

        // Update preview
        UpdateColorPreview(channel.color);

        if (debugLog)
            Debug.Log($"[MaterialChannelSlider] Synced channel {channelIndex} from manager: {channel.color}");
    }

    private void UpdateColorPreview(Color color)
    {
        if (colorPreview != null)
        {
            colorPreview.color = new Color(color.r, color.g, color.b, 1f);
        }
    }

    private void OnCustomModeChanged(bool isActive)
    {
        if (debugLog)
            Debug.Log($"[MaterialChannelSlider] Custom mode changed to: {isActive}");

        UpdateEnabledState();

        // Sync from channel when entering custom mode
        if (isActive)
        {
            SyncFromChannel();
        }
    }

    private void UpdateEnabledState()
    {
        if (CustomisationManager.Instance == null)
        {
            Debug.LogWarning("[MaterialChannelSlider] Cannot update state - CustomisationManager.Instance is null!");

            // Disable all sliders if manager not found
            if (sliderR != null) sliderR.interactable = false;
            if (sliderG != null) sliderG.interactable = false;
            if (sliderB != null) sliderB.interactable = false;
            return;
        }

        bool shouldBeEnabled = CustomisationManager.Instance.IsCustomColourSelected;

        if (debugLog)
            Debug.Log($"[MaterialChannelSlider] UpdateEnabledState - Should be enabled: {shouldBeEnabled}, Current index: {CustomisationManager.Instance.SelectedMaterialIndex}, Material count: {CustomisationManager.Instance.GetMaterialCount()}");

        if (sliderR != null)
            sliderR.interactable = shouldBeEnabled;
        if (sliderG != null)
            sliderG.interactable = shouldBeEnabled;
        if (sliderB != null)
            sliderB.interactable = shouldBeEnabled;

        // Optional: Change visual appearance when disabled
        if (sliderR != null)
        {
            var colors = sliderR.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            sliderR.colors = colors;
        }
    }

    /// <summary>
    /// Public method to manually refresh from manager
    /// </summary>
    public void RefreshFromManager()
    {
        SyncFromChannel();
    }
}