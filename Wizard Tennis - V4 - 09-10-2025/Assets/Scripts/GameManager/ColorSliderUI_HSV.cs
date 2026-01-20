using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HSV-based color slider for custom material coloring
/// Provides Hue, Saturation, Value (Brightness), and Alpha controls
/// </summary>
public class ColorSliderUI_HSV : MonoBehaviour
{
    [Header("HSV Sliders")]
    [SerializeField] private Slider hueSlider;
    [SerializeField] private Slider saturationSlider;
    [SerializeField] private Slider valueSlider;
    [SerializeField] private Slider alphaSlider;

    [Header("Optional RGB Sliders (alternative control)")]
    [SerializeField] private Slider redSlider;
    [SerializeField] private Slider greenSlider;
    [SerializeField] private Slider blueSlider;

    [Header("Color Preview")]
    [SerializeField] private Image colorPreview;
    [SerializeField] private Image hueGradient;
    [SerializeField] private Image saturationGradient;
    [SerializeField] private Image valueGradient;

    [Header("Mode")]
    [SerializeField] private bool useRGBMode = false;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private bool suppressEvents;

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialize());
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        yield return null;

        HookSliders();

        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnCustomModeChanged;
            CustomisationManager.Instance.OnCustomColourModeChanged += OnCustomModeChanged;
        }

        SyncFromManager();
        UpdateEnabledState();
        UpdateGradients();
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
        // HSV sliders
        if (hueSlider != null)
        {
            hueSlider.onValueChanged.RemoveAllListeners();
            hueSlider.onValueChanged.AddListener(OnHSVChanged);
        }

        if (saturationSlider != null)
        {
            saturationSlider.onValueChanged.RemoveAllListeners();
            saturationSlider.onValueChanged.AddListener(OnHSVChanged);
        }

        if (valueSlider != null)
        {
            valueSlider.onValueChanged.RemoveAllListeners();
            valueSlider.onValueChanged.AddListener(OnHSVChanged);
        }

        if (alphaSlider != null)
        {
            alphaSlider.onValueChanged.RemoveAllListeners();
            alphaSlider.onValueChanged.AddListener(OnHSVChanged);
        }

        // RGB sliders
        if (redSlider != null)
        {
            redSlider.onValueChanged.RemoveAllListeners();
            redSlider.onValueChanged.AddListener(OnRGBChanged);
        }

        if (greenSlider != null)
        {
            greenSlider.onValueChanged.RemoveAllListeners();
            greenSlider.onValueChanged.AddListener(OnRGBChanged);
        }

        if (blueSlider != null)
        {
            blueSlider.onValueChanged.RemoveAllListeners();
            blueSlider.onValueChanged.AddListener(OnRGBChanged);
        }
    }

    private void UnhookSliders()
    {
        if (hueSlider != null) hueSlider.onValueChanged.RemoveAllListeners();
        if (saturationSlider != null) saturationSlider.onValueChanged.RemoveAllListeners();
        if (valueSlider != null) valueSlider.onValueChanged.RemoveAllListeners();
        if (alphaSlider != null) alphaSlider.onValueChanged.RemoveAllListeners();
        if (redSlider != null) redSlider.onValueChanged.RemoveAllListeners();
        if (greenSlider != null) greenSlider.onValueChanged.RemoveAllListeners();
        if (blueSlider != null) blueSlider.onValueChanged.RemoveAllListeners();
    }

    private void OnHSVChanged(float _)
    {
        if (suppressEvents || CustomisationManager.Instance == null)
            return;

        if (!CustomisationManager.Instance.IsCustomColourSelected)
            return;

        float h = hueSlider != null ? hueSlider.value : 0f;
        float s = saturationSlider != null ? saturationSlider.value : 1f;
        float v = valueSlider != null ? valueSlider.value : 1f;
        float a = alphaSlider != null ? alphaSlider.value : 1f;

        if (debugLog)
            Debug.Log($"[ColorSliderUI] HSV changed: H={h:F2}, S={s:F2}, V={v:F2}, A={a:F2}");

        CustomisationManager.Instance.SetCustomColorHSV(h, s, v, a);

        UpdateColorPreview(h, s, v, a);
        UpdateRGBSlidersFromHSV(h, s, v);
        UpdateGradients();
    }

    private void OnRGBChanged(float _)
    {
        if (suppressEvents || CustomisationManager.Instance == null)
            return;

        if (!CustomisationManager.Instance.IsCustomColourSelected)
            return;

        float r = redSlider != null ? redSlider.value : 1f;
        float g = greenSlider != null ? greenSlider.value : 1f;
        float b = blueSlider != null ? blueSlider.value : 1f;

        Color rgb = new Color(r, g, b, 1f);

        if (debugLog)
            Debug.Log($"[ColorSliderUI] RGB changed: R={r:F2}, G={g:F2}, B={b:F2}");

        CustomisationManager.Instance.SetCustomColorRGB(rgb);

        UpdateHSVSlidersFromRGB(rgb);
        UpdateColorPreview(rgb);
        UpdateGradients();
    }

    private void SyncFromManager()
    {
        if (CustomisationManager.Instance == null)
            return;

        suppressEvents = true;

        CustomisationManager.Instance.GetCustomColorHSV(out float h, out float s, out float v, out float a);

        if (hueSlider != null) hueSlider.value = h;
        if (saturationSlider != null) saturationSlider.value = s;
        if (valueSlider != null) valueSlider.value = v;
        if (alphaSlider != null) alphaSlider.value = a;

        Color rgb = CustomisationManager.Instance.GetCustomColorRGB();
        if (redSlider != null) redSlider.value = rgb.r;
        if (greenSlider != null) greenSlider.value = rgb.g;
        if (blueSlider != null) blueSlider.value = rgb.b;

        UpdateColorPreview(h, s, v, a);

        suppressEvents = false;

        if (debugLog)
            Debug.Log($"[ColorSliderUI] Synced from manager: H={h:F2}, S={s:F2}, V={v:F2}");
    }

    private void UpdateColorPreview(float h, float s, float v, float a)
    {
        if (colorPreview != null)
        {
            Color c = Color.HSVToRGB(h, s, v);
            c.a = 1f; // Always show preview at full opacity
            colorPreview.color = c;
        }
    }

    private void UpdateColorPreview(Color rgb)
    {
        if (colorPreview != null)
        {
            colorPreview.color = new Color(rgb.r, rgb.g, rgb.b, 1f);
        }
    }

    private void UpdateHSVSlidersFromRGB(Color rgb)
    {
        suppressEvents = true;

        Color.RGBToHSV(rgb, out float h, out float s, out float v);

        if (hueSlider != null) hueSlider.value = h;
        if (saturationSlider != null) saturationSlider.value = s;
        if (valueSlider != null) valueSlider.value = v;

        suppressEvents = false;
    }

    private void UpdateRGBSlidersFromHSV(float h, float s, float v)
    {
        suppressEvents = true;

        Color rgb = Color.HSVToRGB(h, s, v);

        if (redSlider != null) redSlider.value = rgb.r;
        if (greenSlider != null) greenSlider.value = rgb.g;
        if (blueSlider != null) blueSlider.value = rgb.b;

        suppressEvents = false;
    }

    private void UpdateGradients()
    {
        float h = hueSlider != null ? hueSlider.value : 0f;
        float s = saturationSlider != null ? saturationSlider.value : 1f;
        float v = valueSlider != null ? valueSlider.value : 1f;

        // Update saturation gradient (gray to full color at current hue/value)
        if (saturationGradient != null)
        {
            Color fullSat = Color.HSVToRGB(h, 1f, v);
            Color noSat = Color.HSVToRGB(h, 0f, v);
            UpdateGradientImage(saturationGradient, noSat, fullSat);
        }

        // Update value gradient (black to full brightness at current hue/sat)
        if (valueGradient != null)
        {
            Color fullValue = Color.HSVToRGB(h, s, 1f);
            Color noValue = Color.HSVToRGB(h, s, 0f);
            UpdateGradientImage(valueGradient, noValue, fullValue);
        }
    }

    private void UpdateGradientImage(Image img, Color startColor, Color endColor)
    {
        // This requires a gradient texture or you can use a shader
        // For simplicity, just set to end color
        img.color = endColor;
    }

    private void OnCustomModeChanged(bool isActive)
    {
        if (debugLog)
            Debug.Log($"[ColorSliderUI] Custom mode changed to: {isActive}");

        UpdateEnabledState();

        if (isActive)
        {
            SyncFromManager();
        }
    }

    private void UpdateEnabledState()
    {
        if (CustomisationManager.Instance == null)
        {
            SetAllSlidersInteractable(false);
            return;
        }

        bool shouldBeEnabled = CustomisationManager.Instance.IsCustomColourSelected;

        SetAllSlidersInteractable(shouldBeEnabled);

        if (debugLog)
            Debug.Log($"[ColorSliderUI] Sliders enabled: {shouldBeEnabled}");
    }

    private void SetAllSlidersInteractable(bool interactable)
    {
        if (hueSlider != null) hueSlider.interactable = interactable;
        if (saturationSlider != null) saturationSlider.interactable = interactable;
        if (valueSlider != null) valueSlider.interactable = interactable;
        if (alphaSlider != null) alphaSlider.interactable = interactable;
        if (redSlider != null) redSlider.interactable = interactable;
        if (greenSlider != null) greenSlider.interactable = interactable;
        if (blueSlider != null) blueSlider.interactable = interactable;
    }

    /// <summary>
    /// Public method to manually refresh from manager
    /// </summary>
    public void RefreshFromManager()
    {
        SyncFromManager();
    }

    /// <summary>
    /// Set to a preset color
    /// </summary>
    public void SetPresetColor(Color color)
    {
        if (CustomisationManager.Instance != null && CustomisationManager.Instance.IsCustomColourSelected)
        {
            CustomisationManager.Instance.SetCustomColorRGB(color);
            SyncFromManager();
        }
    }
}