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
    private float checkInterval = 0.5f;
    private float lastCheckTime = 0f;

    private void OnEnable()
    {
        suppressEvents = false;
        StartCoroutine(DelayedInitialize());
    }

    private void Update()
    {
        // Continuously check for sliders and hook them if found
        if (Time.unscaledTime - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.unscaledTime;

            TryFindSliders();
            EnsureSlidersHooked();
        }
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        // Wait for UI to be fully loaded
        yield return new WaitForEndOfFrame();
        yield return null;

        if (debugLog)
            Debug.Log("[ColorSliderUI] Starting delayed initialization...");

        TryFindSliders();
        EnsureSlidersHooked();

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
        UnhookAllSliders();

        if (CustomisationManager.Instance != null)
        {
            CustomisationManager.Instance.OnCustomColourModeChanged -= OnCustomModeChanged;
            CustomisationManager.Instance.OnCustomColourChanged -= OnCustomColourChanged;
        }
    }

    public void TryFindSliders()
    {
        // HSV Sliders
        if (hueSlider == null && !string.IsNullOrEmpty(hueSliderName))
        {
            GameObject hueObj = GameObject.Find(hueSliderName);
            if (hueObj != null)
            {
                hueSlider = hueObj.GetComponent<Slider>();
                if (hueSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found hue slider: {hueSliderName}");
            }
        }

        if (saturationSlider == null && !string.IsNullOrEmpty(saturationSliderName))
        {
            GameObject satObj = GameObject.Find(saturationSliderName);
            if (satObj != null)
            {
                saturationSlider = satObj.GetComponent<Slider>();
                if (saturationSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found saturation slider: {saturationSliderName}");
            }
        }

        if (valueSlider == null && !string.IsNullOrEmpty(valueSliderName))
        {
            GameObject valObj = GameObject.Find(valueSliderName);
            if (valObj != null)
            {
                valueSlider = valObj.GetComponent<Slider>();
                if (valueSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found value slider: {valueSliderName}");
            }
        }

        if (alphaSlider == null && !string.IsNullOrEmpty(alphaSliderName))
        {
            GameObject alphaObj = GameObject.Find(alphaSliderName);
            if (alphaObj != null)
            {
                alphaSlider = alphaObj.GetComponent<Slider>();
                if (alphaSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found alpha slider: {alphaSliderName}");
            }
        }

        // RGB Sliders
        if (redSlider == null && !string.IsNullOrEmpty(redSliderName))
        {
            GameObject redObj = GameObject.Find(redSliderName);
            if (redObj != null)
            {
                redSlider = redObj.GetComponent<Slider>();
                if (redSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found red slider: {redSliderName}");
            }
        }

        if (greenSlider == null && !string.IsNullOrEmpty(greenSliderName))
        {
            GameObject greenObj = GameObject.Find(greenSliderName);
            if (greenObj != null)
            {
                greenSlider = greenObj.GetComponent<Slider>();
                if (greenSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found green slider: {greenSliderName}");
            }
        }

        if (blueSlider == null && !string.IsNullOrEmpty(blueSliderName))
        {
            GameObject blueObj = GameObject.Find(blueSliderName);
            if (blueObj != null)
            {
                blueSlider = blueObj.GetComponent<Slider>();
                if (blueSlider != null && debugLog)
                    Debug.Log($"[ColorSliderUI] Found blue slider: {blueSliderName}");
            }
        }
    }

    private void EnsureSlidersHooked()
    {
        // Check each slider and hook if it has no listeners
        if (hueSlider != null && hueSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            hueSlider.onValueChanged.RemoveAllListeners();
            hueSlider.onValueChanged.AddListener(OnHSVChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked hue slider");
        }

        if (saturationSlider != null && saturationSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            saturationSlider.onValueChanged.RemoveAllListeners();
            saturationSlider.onValueChanged.AddListener(OnHSVChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked saturation slider");
        }

        if (valueSlider != null && valueSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            valueSlider.onValueChanged.RemoveAllListeners();
            valueSlider.onValueChanged.AddListener(OnHSVChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked value slider");
        }

        if (alphaSlider != null && alphaSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            alphaSlider.onValueChanged.RemoveAllListeners();
            alphaSlider.onValueChanged.AddListener(OnHSVChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked alpha slider");
        }

        if (redSlider != null && redSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            redSlider.onValueChanged.RemoveAllListeners();
            redSlider.onValueChanged.AddListener(OnRGBChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked red slider");
        }

        if (greenSlider != null && greenSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            greenSlider.onValueChanged.RemoveAllListeners();
            greenSlider.onValueChanged.AddListener(OnRGBChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked green slider");
        }

        if (blueSlider != null && blueSlider.onValueChanged.GetPersistentEventCount() == 0)
        {
            blueSlider.onValueChanged.RemoveAllListeners();
            blueSlider.onValueChanged.AddListener(OnRGBChanged);
            if (debugLog)
                Debug.Log("[ColorSliderUI] Hooked blue slider");
        }
    }

    private void UnhookAllSliders()
    {
        if (hueSlider != null) hueSlider.onValueChanged.RemoveAllListeners();
        if (saturationSlider != null) saturationSlider.onValueChanged.RemoveAllListeners();
        if (valueSlider != null) valueSlider.onValueChanged.RemoveAllListeners();
        if (alphaSlider != null) alphaSlider.onValueChanged.RemoveAllListeners();
        if (redSlider != null) redSlider.onValueChanged.RemoveAllListeners();
        if (greenSlider != null) greenSlider.onValueChanged.RemoveAllListeners();
        if (blueSlider != null) blueSlider.onValueChanged.RemoveAllListeners();

        if (debugLog)
            Debug.Log("[ColorSliderUI] Unhooked all sliders");
    }

    private void OnHSVChanged(float _)
    {
        if (suppressEvents)
        {
            if (debugLog)
                Debug.Log("[ColorSliderUI] HSV changed but events suppressed");
            return;
        }

        if (CustomisationManager.Instance == null)
        {
            if (debugLog)
                Debug.LogWarning("[ColorSliderUI] HSV changed but CustomisationManager is null!");
            return;
        }

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
            Debug.Log($"[ColorSliderUI] *** HSV CHANGED AND APPLYING *** H={h:F2}, S={s:F2}, V={v:F2}, A={a:F2}");

        // This should trigger the material update
        CustomisationManager.Instance.SetCustomColorHSV(h, s, v, a);

        // Update preview without triggering events
        suppressEvents = true;
        UpdateColorPreview(h, s, v, a);
        UpdateRGBSlidersFromHSV(h, s, v);
        UpdateGradients();
        suppressEvents = false;
    }

    private void OnRGBChanged(float _)
    {
        if (suppressEvents)
        {
            if (debugLog)
                Debug.Log("[ColorSliderUI] RGB changed but events suppressed");
            return;
        }

        if (CustomisationManager.Instance == null)
        {
            if (debugLog)
                Debug.LogWarning("[ColorSliderUI] RGB changed but CustomisationManager is null!");
            return;
        }

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
            Debug.Log($"[ColorSliderUI] *** RGB CHANGED AND APPLYING *** R={r:F2}, G={g:F2}, B={b:F2}");

        CustomisationManager.Instance.SetCustomColorRGB(rgb);

        suppressEvents = true;
        UpdateHSVSlidersFromRGB(rgb);
        UpdateColorPreview(rgb);
        UpdateGradients();
        suppressEvents = false;
    }

    private void OnCustomColourChanged(Color color)
    {
        if (debugLog)
            Debug.Log($"[ColorSliderUI] Custom colour changed event received: {color}");

        // External change - sync our UI
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

        if (hueSlider != null) hueSlider.value = h;
        if (saturationSlider != null) saturationSlider.value = s;
        if (valueSlider != null) valueSlider.value = v;
        if (alphaSlider != null) alphaSlider.value = a;

        Color rgb = CustomisationManager.Instance.GetCustomColorRGB();
        if (redSlider != null) redSlider.value = rgb.r;
        if (greenSlider != null) greenSlider.value = rgb.g;
        if (blueSlider != null) blueSlider.value = rgb.b;

        UpdateColorPreview(h, s, v, a);
        UpdateGradients();

        suppressEvents = false;

        if (debugLog)
            Debug.Log($"[ColorSliderUI] *** SYNCED FROM MANAGER *** H={h:F2}, S={s:F2}, V={v:F2}, A={a:F2}");
    }

    private void UpdateColorPreview(float h, float s, float v, float a)
    {
        if (colorPreview != null)
        {
            Color c = Color.HSVToRGB(h, s, v);
            c.a = 1f;
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
        Color.RGBToHSV(rgb, out float h, out float s, out float v);

        if (hueSlider != null) hueSlider.value = h;
        if (saturationSlider != null) saturationSlider.value = s;
        if (valueSlider != null) valueSlider.value = v;
    }

    private void UpdateRGBSlidersFromHSV(float h, float s, float v)
    {
        Color rgb = Color.HSVToRGB(h, s, v);

        if (redSlider != null) redSlider.value = rgb.r;
        if (greenSlider != null) greenSlider.value = rgb.g;
        if (blueSlider != null) blueSlider.value = rgb.b;
    }

    private void UpdateGradients()
    {
        float h = hueSlider != null ? hueSlider.value : 0f;
        float s = saturationSlider != null ? saturationSlider.value : 1f;
        float v = valueSlider != null ? valueSlider.value : 1f;

        if (saturationGradient != null)
        {
            Color fullSat = Color.HSVToRGB(h, 1f, v);
            Color noSat = Color.HSVToRGB(h, 0f, v);
            UpdateGradientImage(saturationGradient, noSat, fullSat);
        }

        if (valueGradient != null)
        {
            Color fullValue = Color.HSVToRGB(h, s, 1f);
            Color noValue = Color.HSVToRGB(h, s, 0f);
            UpdateGradientImage(valueGradient, noValue, fullValue);
        }
    }

    private void UpdateGradientImage(Image img, Color startColor, Color endColor)
    {
        img.color = endColor;
    }

    private void OnCustomModeChanged(bool isActive)
    {
        if (debugLog)
            Debug.Log($"[ColorSliderUI] *** CUSTOM MODE CHANGED TO: {isActive} ***");

        UpdateEnabledState();

        if (isActive)
        {
            // Give it a frame to settle
            StartCoroutine(DelayedSync());
        }
    }

    private System.Collections.IEnumerator DelayedSync()
    {
        yield return null;
        SyncFromManager();
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
            Debug.Log($"[ColorSliderUI] *** SLIDERS INTERACTABLE: {shouldBeEnabled} ***");
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

    public void RefreshFromManager()
    {
        if (debugLog)
            Debug.Log("[ColorSliderUI] *** MANUAL REFRESH REQUESTED ***");

        TryFindSliders();
        EnsureSlidersHooked();
        SyncFromManager();
        UpdateEnabledState();
    }

    public void SetPresetColor(Color color)
    {
        if (CustomisationManager.Instance != null && CustomisationManager.Instance.IsCustomColourSelected)
        {
            CustomisationManager.Instance.SetCustomColorRGB(color);
            SyncFromManager();
        }
    }
}