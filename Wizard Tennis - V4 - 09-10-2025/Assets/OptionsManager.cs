using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance;

    [Header("=== UI References (Assign in Inspector) ===")]
    [Tooltip("Volume slider reference")]
    [SerializeField] private Slider volumeSlider;

    [Tooltip("Master volume slider (affects all audio)")]
    [SerializeField] private Slider masterVolumeSlider;

    [Tooltip("Music volume slider")]
    [SerializeField] private Slider musicVolumeSlider;

    [Tooltip("SFX volume slider")]
    [SerializeField] private Slider sfxVolumeSlider;

    [Tooltip("Left-handed mode toggle")]
    [SerializeField] private Toggle leftHandedToggle;

    [Tooltip("Display label for left-handed mode status")]
    [SerializeField] private TextMeshProUGUI modeLabel;

    [Tooltip("Music selection dropdown")]
    [SerializeField] private TMP_Dropdown musicDropdown;

    [Tooltip("Language selection dropdown")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Tooltip("Spell tips toggle")]
    [SerializeField] private Toggle spellTipsToggle;

    [Tooltip("VSync toggle")]
    [SerializeField] private Toggle vsyncToggle;

    [Tooltip("Fullscreen toggle")]
    [SerializeField] private Toggle fullscreenToggle;

    [Tooltip("Quality settings dropdown")]
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Tooltip("Resolution dropdown")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Tooltip("Mouse sensitivity slider")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Tooltip("Invert Y-axis toggle")]
    [SerializeField] private Toggle invertYToggle;

    [Tooltip("Show FPS counter toggle")]
    [SerializeField] private Toggle showFpsToggle;
    [SerializeField] private GameObject fPSCanvas;

    [Tooltip("Field of view slider")]
    [SerializeField] private Slider fovSlider;

    [Header("=== Audio Settings ===")]
    [SerializeField] private float volume = 0.75f;
    [SerializeField] private float masterVolume = 1.0f;
    [SerializeField] private float musicVolume = 0.8f;
    [SerializeField] private float sfxVolume = 1.0f;
    public int bgmValue;
    public AudioClip[] bgmOptions;
    public AudioClip bgm;

    [Header("=== Gameplay Settings ===")]
    [SerializeField] public bool leftHandedMode = false;
    [SerializeField] public bool spellTips = true;
    [SerializeField] private float mouseSensitivity = 1.0f;
    [SerializeField] private bool invertY = false;

    [Header("=== Graphics Settings ===")]
    [SerializeField] private bool vsyncEnabled = true;
    [SerializeField] private bool fullscreen = true;
    [SerializeField] private int qualityLevel = -1; // -1 = use current
    [SerializeField] private int resolutionIndex = 0;
    [SerializeField] private float fieldOfView = 60f;

    [Header("=== UI Settings ===")]
    [SerializeField] private bool showFps = false;
    [SerializeField] private Language currentLanguage = Language.English;

    [Header("=== FPS Counter (Optional) ===")]
    [SerializeField] private TextMeshProUGUI fpsText;
    private float deltaTime = 0.0f;

    // Public Properties
    public float Volume => volume;
    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
    public bool LeftHandedMode => leftHandedMode;
    public bool SpellTips => spellTips;
    public float MouseSensitivity => mouseSensitivity;
    public bool InvertY => invertY;
    public bool VsyncEnabled => vsyncEnabled;
    public bool Fullscreen => fullscreen;
    public int QualityLevel => qualityLevel;
    public bool ShowFps => showFps;
    public float FieldOfView => fieldOfView;
    public Language CurrentLanguage => currentLanguage;
    public string CurrentLanguageCode => LanguageHelper.GetLanguageCode(currentLanguage);

    // Events
    public delegate void LanguageChangedHandler(Language newLanguage);
    public event LanguageChangedHandler OnLanguageChanged;

    public delegate void SettingsChangedHandler();
    public event SettingsChangedHandler OnSettingsChanged;

    // Track if UI is currently hooked
    private bool isUIHooked = false;
    private Resolution[] resolutions;

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

        if (fpsText != null)
        {
            DontDestroyOnLoad(fPSCanvas);
        }

        LoadSettings();
        InitializeResolutions();
    }

    private void Start()
    {
        // Auto-hook UI if references are assigned in inspector
        if (HasUIReferences())
        {
            HookUI();
        }
    }

    private void Update()
    {
        // Update FPS counter if enabled
        if (showFps && fpsText != null)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            float fps = 1.0f / deltaTime;
            fpsText.text = $"FPS: {Mathf.Ceil(fps)}";
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnhookUI();
            Instance = null;
        }
    }

    #region UI Management

    /// <summary>
    /// Check if any UI references are assigned in inspector
    /// </summary>
    private bool HasUIReferences()
    {
        return volumeSlider != null || leftHandedToggle != null ||
               musicDropdown != null || languageDropdown != null ||
               masterVolumeSlider != null || musicVolumeSlider != null ||
               sfxVolumeSlider != null || vsyncToggle != null ||
               fullscreenToggle != null || qualityDropdown != null ||
               resolutionDropdown != null || mouseSensitivitySlider != null ||
               invertYToggle != null || showFpsToggle != null || fovSlider != null;
    }

    /// <summary>
    /// Legacy method for runtime UI assignment (still supported)
    /// </summary>
    public void OnOptionsMenuOpened(
        Slider slider = null,
        Toggle toggle = null,
        TextMeshProUGUI label = null,
        TMP_Dropdown drop = null,
        Toggle toggle2 = null,
        TMP_Dropdown langDrop = null)
    {
        UnhookUI();

        // Override inspector references if runtime references provided
        if (slider != null) volumeSlider = slider;
        if (toggle != null) leftHandedToggle = toggle;
        if (label != null) modeLabel = label;
        if (drop != null) musicDropdown = drop;
        if (toggle2 != null) spellTipsToggle = toggle2;
        if (langDrop != null) languageDropdown = langDrop;

        HookUI();
    }

    public void OnOptionsMenuClosed()
    {
        // Don't unhook if UI was assigned in inspector
        // This allows persistent UI elements to stay connected
    }

    private void HookUI()
    {
        if (isUIHooked) return;

        // Volume Controls
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.value = volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            masterVolumeSlider.value = masterVolume;
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.value = musicVolume;
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.value = sfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        // Gameplay Controls
        if (leftHandedToggle != null)
        {
            leftHandedToggle.onValueChanged.RemoveAllListeners();
            leftHandedToggle.isOn = leftHandedMode;
            leftHandedToggle.onValueChanged.AddListener(SetLeftHandedMode);
        }

        if (spellTipsToggle != null)
        {
            spellTipsToggle.onValueChanged.RemoveAllListeners();
            spellTipsToggle.isOn = spellTips;
            spellTipsToggle.onValueChanged.AddListener(SetSpellTips);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            mouseSensitivitySlider.value = mouseSensitivity;
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        }

        if (invertYToggle != null)
        {
            invertYToggle.onValueChanged.RemoveAllListeners();
            invertYToggle.isOn = invertY;
            invertYToggle.onValueChanged.AddListener(SetInvertY);
        }

        // Dropdowns
        if (musicDropdown != null)
        {
            musicDropdown.onValueChanged.RemoveAllListeners();
            musicDropdown.value = bgmValue;
            musicDropdown.onValueChanged.AddListener(MusicDropDownChange);
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveAllListeners();

            if (languageDropdown.options.Count == 0)
            {
                languageDropdown.ClearOptions();
                var languageNames = new System.Collections.Generic.List<string>();
                foreach (Language lang in System.Enum.GetValues(typeof(Language)))
                {
                    languageNames.Add(LanguageHelper.GetLanguageName(lang));
                }
                languageDropdown.AddOptions(languageNames);
            }

            languageDropdown.value = (int)currentLanguage;
            languageDropdown.onValueChanged.AddListener((int value) => SetLanguage((Language)value));
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged.RemoveAllListeners();

            if (qualityDropdown.options.Count == 0)
            {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            }

            qualityDropdown.value = qualityLevel >= 0 ? qualityLevel : QualitySettings.GetQualityLevel();
            qualityDropdown.onValueChanged.AddListener(SetQualityLevel);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.RemoveAllListeners();
            PopulateResolutionDropdown();
            resolutionDropdown.value = resolutionIndex;
            resolutionDropdown.onValueChanged.AddListener(SetResolution);
        }

        // Graphics Toggles
        if (vsyncToggle != null)
        {
            vsyncToggle.onValueChanged.RemoveAllListeners();
            vsyncToggle.isOn = vsyncEnabled;
            vsyncToggle.onValueChanged.AddListener(SetVsync);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.isOn = fullscreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        // UI Toggles
        if (showFpsToggle != null)
        {
            showFpsToggle.onValueChanged.RemoveAllListeners();
            showFpsToggle.isOn = showFps;
            showFpsToggle.onValueChanged.AddListener(SetShowFps);
        }

        // FOV Slider
        if (fovSlider != null)
        {
            fovSlider.onValueChanged.RemoveAllListeners();
            fovSlider.value = fieldOfView;
            fovSlider.onValueChanged.AddListener(SetFieldOfView);
        }

        UpdateUILabel();
        UpdateFpsDisplay();
        isUIHooked = true;
    }

    private void UnhookUI()
    {
        if (!isUIHooked) return;

        // Remove all listeners
        volumeSlider?.onValueChanged.RemoveListener(SetVolume);
        masterVolumeSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        musicVolumeSlider?.onValueChanged.RemoveListener(SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.RemoveListener(SetSfxVolume);
        leftHandedToggle?.onValueChanged.RemoveListener(SetLeftHandedMode);
        spellTipsToggle?.onValueChanged.RemoveListener(SetSpellTips);
        mouseSensitivitySlider?.onValueChanged.RemoveListener(SetMouseSensitivity);
        invertYToggle?.onValueChanged.RemoveListener(SetInvertY);
        musicDropdown?.onValueChanged.RemoveListener(MusicDropDownChange);
        languageDropdown?.onValueChanged.RemoveListener((int value) => SetLanguage((Language)value));
        qualityDropdown?.onValueChanged.RemoveListener(SetQualityLevel);
        resolutionDropdown?.onValueChanged.RemoveListener(SetResolution);
        vsyncToggle?.onValueChanged.RemoveListener(SetVsync);
        fullscreenToggle?.onValueChanged.RemoveListener(SetFullscreen);
        showFpsToggle?.onValueChanged.RemoveListener(SetShowFps);
        fovSlider?.onValueChanged.RemoveListener(SetFieldOfView);

        isUIHooked = false;
    }

    #endregion

    #region Audio Settings

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("Volume", volume);
        PlayerPrefs.Save();

        if (volumeSlider != null && !Mathf.Approximately(volumeSlider.value, volume))
            volumeSlider.value = volume;

        OnSettingsChanged?.Invoke();
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
        PlayerPrefs.Save();

        if (masterVolumeSlider != null && !Mathf.Approximately(masterVolumeSlider.value, masterVolume))
            masterVolumeSlider.value = masterVolume;

        OnSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();

        if (musicVolumeSlider != null && !Mathf.Approximately(musicVolumeSlider.value, musicVolume))
            musicVolumeSlider.value = musicVolume;

        OnSettingsChanged?.Invoke();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("SfxVolume", sfxVolume);
        PlayerPrefs.Save();

        if (sfxVolumeSlider != null && !Mathf.Approximately(sfxVolumeSlider.value, sfxVolume))
            sfxVolumeSlider.value = sfxVolume;

        OnSettingsChanged?.Invoke();
    }

    #endregion

    #region Gameplay Settings

    public void SetLeftHandedMode(bool enabled)
    {
        leftHandedMode = enabled;
        PlayerPrefs.SetInt("LeftHandedMode", leftHandedMode ? 1 : 0);
        PlayerPrefs.Save();

        UpdateUILabel();

        if (leftHandedToggle != null && leftHandedToggle.isOn != leftHandedMode)
            leftHandedToggle.isOn = leftHandedMode;

        OnSettingsChanged?.Invoke();
    }

    public void SetSpellTips(bool enabled)
    {
        spellTips = enabled;
        PlayerPrefs.SetInt("spellTips", spellTips ? 1 : 0);
        PlayerPrefs.Save();

        if (spellTipsToggle != null && spellTipsToggle.isOn != spellTips)
            spellTipsToggle.isOn = spellTips;

        OnSettingsChanged?.Invoke();
    }

    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Clamp(value, 0.1f, 5.0f);
        PlayerPrefs.SetFloat("MouseSensitivity", mouseSensitivity);
        PlayerPrefs.Save();

        if (mouseSensitivitySlider != null && !Mathf.Approximately(mouseSensitivitySlider.value, mouseSensitivity))
            mouseSensitivitySlider.value = mouseSensitivity;

        OnSettingsChanged?.Invoke();
    }

    public void SetInvertY(bool enabled)
    {
        invertY = enabled;
        PlayerPrefs.SetInt("InvertY", invertY ? 1 : 0);
        PlayerPrefs.Save();

        if (invertYToggle != null && invertYToggle.isOn != invertY)
            invertYToggle.isOn = invertY;

        OnSettingsChanged?.Invoke();
    }

    #endregion

    #region Graphics Settings

    public void SetVsync(bool enabled)
    {
        vsyncEnabled = enabled;
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        PlayerPrefs.SetInt("Vsync", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (vsyncToggle != null && vsyncToggle.isOn != enabled)
            vsyncToggle.isOn = enabled;

        OnSettingsChanged?.Invoke();
    }

    public void SetFullscreen(bool enabled)
    {
        fullscreen = enabled;
        Screen.fullScreen = enabled;
        PlayerPrefs.SetInt("Fullscreen", enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (fullscreenToggle != null && fullscreenToggle.isOn != enabled)
            fullscreenToggle.isOn = enabled;

        OnSettingsChanged?.Invoke();
    }

    public void SetQualityLevel(int level)
    {
        qualityLevel = level;
        QualitySettings.SetQualityLevel(level);
        PlayerPrefs.SetInt("QualityLevel", level);
        PlayerPrefs.Save();

        if (qualityDropdown != null && qualityDropdown.value != level)
            qualityDropdown.value = level;

        OnSettingsChanged?.Invoke();
    }

    public void SetResolution(int index)
    {
        resolutionIndex = index;

        if (resolutions != null && index >= 0 && index < resolutions.Length)
        {
            Resolution resolution = resolutions[index];
            Screen.SetResolution(resolution.width, resolution.height, fullscreen);
            PlayerPrefs.SetInt("ResolutionIndex", index);
            PlayerPrefs.Save();
        }

        if (resolutionDropdown != null && resolutionDropdown.value != index)
            resolutionDropdown.value = index;

        OnSettingsChanged?.Invoke();
    }

    public void SetFieldOfView(float fov)
    {
        fieldOfView = Mathf.Clamp(fov, 30f, 120f);
        PlayerPrefs.SetFloat("FOV", fieldOfView);
        PlayerPrefs.Save();

        if (fovSlider != null && !Mathf.Approximately(fovSlider.value, fieldOfView))
            fovSlider.value = fieldOfView;

        // Apply to active cameras
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            cam.fieldOfView = fieldOfView;
        }

        OnSettingsChanged?.Invoke();
    }

    private void InitializeResolutions()
    {
        resolutions = Screen.resolutions;
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null || resolutions == null) return;

        resolutionDropdown.ClearOptions();
        var options = new System.Collections.Generic.List<string>();

        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = $"{resolutions[i].width} x {resolutions[i].height} @ {resolutions[i].refreshRate}Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionIndex = currentResolutionIndex;
    }

    #endregion

    #region UI Settings

    public void SetShowFps(bool enabled)
    {
        showFps = enabled;
        PlayerPrefs.SetInt("ShowFps", enabled ? 1 : 0);
        PlayerPrefs.Save();

        UpdateFpsDisplay();

        if (showFpsToggle != null && showFpsToggle.isOn != enabled)
            showFpsToggle.isOn = enabled;

        OnSettingsChanged?.Invoke();
    }

    public void SetLanguage(Language language)
    {
        currentLanguage = language;
        PlayerPrefs.SetInt("Language", (int)language);
        PlayerPrefs.Save();

        Debug.Log($"[OptionsManager] Language changed to: {LanguageHelper.GetLanguageName(language)} ({LanguageHelper.GetLanguageCode(language)})");

        if (languageDropdown != null && languageDropdown.value != (int)language)
            languageDropdown.value = (int)language;

        // Fire the language changed event (for spellcasting and other subscribers)
        OnLanguageChanged?.Invoke(language);

        // Force update all localized UI components
        BroadcastLanguageChangeToAllUI();

        OnSettingsChanged?.Invoke();
    }

    private void UpdateFpsDisplay()
    {
        if (fpsText != null)
        {
            fpsText.gameObject.SetActive(showFps);
        }
    }

    #endregion

    #region Loading and UI Updates

    private void UpdateUILabel()
    {
        if (modeLabel != null)
        {
            modeLabel.text = leftHandedMode ? "Left-Handed Mode: ON" : "Left-Handed Mode: OFF";
        }
    }

    private void LoadSettings()
    {
        // Audio
        volume = PlayerPrefs.GetFloat("Volume", 0.75f);
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1.0f);
        bgmValue = PlayerPrefs.GetInt("bgm", 0);

        // Gameplay
        leftHandedMode = PlayerPrefs.GetInt("LeftHandedMode", 0) == 1;
        spellTips = PlayerPrefs.GetInt("spellTips", 1) == 1;
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
        invertY = PlayerPrefs.GetInt("InvertY", 0) == 1;

        // Graphics
        vsyncEnabled = PlayerPrefs.GetInt("Vsync", 1) == 1;
        fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        qualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
        resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
        fieldOfView = PlayerPrefs.GetFloat("FOV", 60f);

        // UI
        showFps = PlayerPrefs.GetInt("ShowFps", 0) == 1;
        currentLanguage = (Language)PlayerPrefs.GetInt("Language", 0);

        // Apply settings
        AudioListener.volume = volume;
        QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;
        Screen.fullScreen = fullscreen;
        QualitySettings.SetQualityLevel(qualityLevel);

        // BGM
        if (bgmOptions != null && bgmOptions.Length > 0)
        {
            bgmValue = Mathf.Clamp(bgmValue, 0, bgmOptions.Length - 1);
            bgm = bgmOptions[bgmValue];
        }

        Debug.Log($"[OptionsManager] Loaded all settings. Language: {LanguageHelper.GetLanguageName(currentLanguage)}");
    }

    private void MusicDropDownChange(int value)
    {
        bgmValue = value;
        PlayerPrefs.SetInt("bgm", bgmValue);
        PlayerPrefs.Save();

        if (bgmOptions != null && bgmValue >= 0 && bgmValue < bgmOptions.Length)
        {
            bgm = bgmOptions[bgmValue];
        }

        OnSettingsChanged?.Invoke();
    }

    public void ApplyCurrentSettings()
    {
        AudioListener.volume = volume;
        QualitySettings.vSyncCount = vsyncEnabled ? 1 : 0;
        QualitySettings.SetQualityLevel(qualityLevel);
        Screen.fullScreen = fullscreen;

        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            cam.fieldOfView = fieldOfView;
        }
    }

    /// <summary>
    /// Reset all settings to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        SetVolume(0.75f);
        SetMasterVolume(1.0f);
        SetMusicVolume(0.8f);
        SetSfxVolume(1.0f);
        SetLeftHandedMode(false);
        SetSpellTips(true);
        SetMouseSensitivity(1.0f);
        SetInvertY(false);
        SetVsync(true);
        SetFullscreen(true);
        SetQualityLevel(QualitySettings.GetQualityLevel());
        SetShowFps(false);
        SetFieldOfView(60f);
        SetLanguage(Language.English);

        Debug.Log("[OptionsManager] Reset all settings to defaults");
    }

    /// <summary>
    /// Broadcasts language change to all localized UI components in the scene
    /// </summary>
    private void BroadcastLanguageChangeToAllUI()
    {
        Debug.Log($"[OptionsManager] Broadcasting language change to all UI components...");

        int updatedCount = 0;

        // Update all LocalizedText components
        foreach (var localizedText in FindObjectsOfType<LocalizedText>(true))
        {
            localizedText.UpdateText();
            updatedCount++;
        }

        // Update all LocalizedButton components
        foreach (var localizedButton in FindObjectsOfType<LocalizedButton>(true))
        {
            localizedButton.UpdateText();
            updatedCount++;
        }

        // Update all LocalizedDropdown components
        foreach (var localizedDropdown in FindObjectsOfType<LocalizedDropdown>(true))
        {
            localizedDropdown.UpdateOptions();
            updatedCount++;
        }

        // Update all LocalizedInputField components
        foreach (var localizedInputField in FindObjectsOfType<LocalizedInputField>(true))
        {
            localizedInputField.UpdatePlaceholder();
            updatedCount++;
        }

        Debug.Log($"[OptionsManager] Updated {updatedCount} localized UI components");
    }

    /// <summary>
    /// Force refresh all localized UI in the scene (useful for debugging)
    /// </summary>
    public void ForceRefreshAllLocalizedUI()
    {
        Debug.Log("[OptionsManager] Forcing refresh of all localized UI...");
        BroadcastLanguageChangeToAllUI();
    }

    #endregion
}