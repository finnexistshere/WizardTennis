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

    [Header("HSV Sliders Names")]
    [SerializeField] private string hueSliderName = "Hue";
    [SerializeField] private string valueSliderName = "Brightness";
    [SerializeField] private string saturationSliderName = "Saturation";
    [SerializeField] private string alphaSliderName = "Alpha";

    [Header("Optional RGB Sliders (alternative control)")]
    [SerializeField] private Slider redSlider;
    [SerializeField] private Slider greenSlider;
    [SerializeField] private Slider blueSlider;

    [Header("RGB Slider Names")]
    [SerializeField] private string redSliderName = "Red";
    [SerializeField] private string greenSliderName = "Green";
    [SerializeField] private string blueSliderName = "Blue";

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
    private bool isHooked = false;

    private void OnEnable()
    {
        StartCoroutine(DelayedInitialize());
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        // Wait for UI to be fully loaded
        yield return null;
        yield return new WaitForEndOfFrame();

        if (debugLog)
            Debug.Log("[ColorSliderUI] Starting delayed initialization...");

        TryFindSliders();
        HookSliders();

        if (CustomisationManager.Instance != null)
        {
            // Unhook first to avoid duplicate subscriptions
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnCustomModeChanged;
            CustomisationManager.Instance.OnCustomColourChanged -= OnCustomColourChanged;

            // Then hook
            CustomisationManager.Instance.OnCustomColourModeChanged += OnCustomModeChanged;
            CustomisationManager.Instance.OnCustomColourChanged += OnCustomColourChanged;

            if (debugLog)
                Debug.Log("[ColorSliderUI] Subscribed to CustomisationManager events");
        }

        SyncFromManager();
        UpdateEnabledState();
        UpdateGradients();

        if (debugLog)
            Debug.Log("[ColorSliderUI] Initialization complete");
    }

    private void OnDisable()
    {
        UnhookSliders();

        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnCustomModeChanged;
            CustomisationManager.Instance.OnCustomColourChanged -= OnCustomColourChanged;
        }

        isHooked = false;
    }

    public void TryFindSliders()
    {
        bool foundAny = false;

        // HSV Sliders
        if (hueSlider == null && !string.IsNullOrEmpty(hueSliderName))
        {
            GameObject hueObj = GameObject.Find(hueSliderName);
            if (hueObj != null)
            {
                hueSlider = hueObj.GetComponent<Slider>();
                if (hueSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found hue slider: {hueSliderName}");
                }
            }
        }

        if (saturationSlider == null && !string.IsNullOrEmpty(saturationSliderName))
        {
            GameObject satObj = GameObject.Find(saturationSliderName);
            if (satObj != null)
            {
                saturationSlider = satObj.GetComponent<Slider>();
                if (saturationSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found saturation slider: {saturationSliderName}");
                }
            }
        }

        if (valueSlider == null && !string.IsNullOrEmpty(valueSliderName))
        {
            GameObject valObj = GameObject.Find(valueSliderName);
            if (valObj != null)
            {
                valueSlider = valObj.GetComponent<Slider>();
                if (valueSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found value slider: {valueSliderName}");
                }
            }
        }

        if (alphaSlider == null && !string.IsNullOrEmpty(alphaSliderName))
        {
            GameObject alphaObj = GameObject.Find(alphaSliderName);
            if (alphaObj != null)
            {
                alphaSlider = alphaObj.GetComponent<Slider>();
                if (alphaSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found alpha slider: {alphaSliderName}");
                }
            }
        }

        // RGB Sliders
        if (redSlider == null && !string.IsNullOrEmpty(redSliderName))
        {
            GameObject redObj = GameObject.Find(redSliderName);
            if (redObj != null)
            {
                redSlider = redObj.GetComponent<Slider>();
                if (redSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found red slider: {redSliderName}");
                }
            }
        }

        if (greenSlider == null && !string.IsNullOrEmpty(greenSliderName))
        {
            GameObject greenObj = GameObject.Find(greenSliderName);
            if (greenObj != null)
            {
                greenSlider = greenObj.GetComponent<Slider>();
                if (greenSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found green slider: {greenSliderName}");
                }
            }
        }

        if (blueSlider == null && !string.IsNullOrEmpty(blueSliderName))
        {
            GameObject blueObj = GameObject.Find(blueSliderName);
            if (blueObj != null)
            {
                blueSlider = blueObj.GetComponent<Slider>();
                if (blueSlider != null)
                {
                    foundAny = true;
                    if (debugLog)
                        Debug.Log($"[ColorSliderUI] Found blue slider: {blueSliderName}");
                }
            }
        }

        if (foundAny && !isHooked)
        {
            HookSliders();
        }
    }

    private void HookSliders()
    {
        if (isHooked)
        {
            if (debugLog)
                Debug.Log("[ColorSliderUI] Already hooked, unhooking first...");
            UnhookSliders();
        }

        int hookedCount = 0;

        // HSV sliders
        if (hueSlider != null)
        {
            hueSlider.onValueChanged.RemoveAllListeners();
            hueSlider.onValueChanged.AddListener(OnHSVChanged);
            hookedCount++;
        }

        if (saturationSlider != null)
        {
            saturationSlider.onValueChanged.RemoveAllListeners();
            saturationSlider.onValueChanged.AddListener(OnHSVChanged);
            hookedCount++;
        }

        if (valueSlider != null)
        {
            valueSlider.onValueChanged.RemoveAllListeners();
            valueSlider.onValueChanged.AddListener(OnHSVChanged);
            hookedCount++;
        }

        if (alphaSlider != null)
        {
            alphaSlider.onValueChanged.RemoveAllListeners();
            alphaSlider.onValueChanged.AddListener(OnHSVChanged);
            hookedCount++;
        }

        // RGB sliders
        if (redSlider != null)
        {
            redSlider.onValueChanged.RemoveAllListeners();
            redSlider.onValueChanged.AddListener(OnRGBChanged);
            hookedCount++;
        }

        if (greenSlider != null)
        {
            greenSlider.onValueChanged.RemoveAllListeners();
            greenSlider.onValueChanged.AddListener(OnRGBChanged);
            hookedCount++;
        }

        if (blueSlider != null)
        {
            blueSlider.onValueChanged.RemoveAllListeners();
            blueSlider.onValueChanged.AddListener(OnRGBChanged);
            hookedCount++;
        }

        isHooked = hookedCount > 0;

        if (debugLog)
            Debug.Log($"[ColorSliderUI] Hooked {hookedCount} slider(s)");
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

        isHooked = false;

        if (debugLog)
            Debug.Log("[ColorSliderUI] Unhooked all sliders");
    }

    private void OnHSVChanged(float _)
    {
        if (suppressEvents || CustomisationManager.Instance == null)
            return;

        if (!CustomisationManager.Instance.IsCustomColourSelected)
        {
            if (debugLog)
                Debug.Log("[ColorSliderUI] HSV changed but custom colour not selected - ignoring");
            return;
        }

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
        {
            if (debugLog)
                Debug.Log("[ColorSliderUI] RGB changed but custom colour not selected - ignoring");
            return;
        }

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

    private void OnCustomColourChanged(Color color)
    {
        if (debugLog)
            Debug.Log($"[ColorSliderUI] Custom colour changed event received: {color}");

        // This is called when the color changes from CustomisationManager
        // We should sync our UI without triggering events
        SyncFromManager();
    }

    private void SyncFromManager()
    {
        if (CustomisationManager.Instance == null)
        {
            if (debugLog)
                Debug.LogWarning("[ColorSliderUI] Cannot sync - CustomisationManager.Instance is null");
            return;
        }

        suppressEvents = true;

        CustomisationManager.Instance.GetCustomColorHSV(out float h, out float s, out float v, out float a);

        if (hueSlider != null) hueSlider.SetValueWithoutNotify(h);
        if (saturationSlider != null) saturationSlider.SetValueWithoutNotify(s);
        if (valueSlider != null) valueSlider.SetValueWithoutNotify(v);
        if (alphaSlider != null) alphaSlider.SetValueWithoutNotify(a);

        Color rgb = CustomisationManager.Instance.GetCustomColorRGB();
        if (redSlider != null) redSlider.SetValueWithoutNotify(rgb.r);
        if (greenSlider != null) greenSlider.SetValueWithoutNotify(rgb.g);
        if (blueSlider != null) blueSlider.SetValueWithoutNotify(rgb.b);

        UpdateColorPreview(h, s, v, a);
        UpdateGradients();

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

        if (hueSlider != null) hueSlider.SetValueWithoutNotify(h);
        if (saturationSlider != null) saturationSlider.SetValueWithoutNotify(s);
        if (valueSlider != null) valueSlider.SetValueWithoutNotify(v);

        suppressEvents = false;
    }

    private void UpdateRGBSlidersFromHSV(float h, float s, float v)
    {
        suppressEvents = true;

        Color rgb = Color.HSVToRGB(h, s, v);

        if (redSlider != null) redSlider.SetValueWithoutNotify(rgb.r);
        if (greenSlider != null) greenSlider.SetValueWithoutNotify(rgb.g);
        if (blueSlider != null) blueSlider.SetValueWithoutNotify(rgb.b);

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
            if (debugLog)
                Debug.Log("[ColorSliderUI] Disabled sliders - no CustomisationManager");
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
        if (debugLog)
            Debug.Log("[ColorSliderUI] Manual refresh requested");

        TryFindSliders();
        SyncFromManager();
        UpdateEnabledState();
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