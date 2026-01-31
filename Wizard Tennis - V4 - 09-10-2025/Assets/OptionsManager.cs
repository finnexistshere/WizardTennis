using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;
using System.Collections.Generic;
//using UnityEngine.UIElements;

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

    [Header("=== Spell Customisation (Assign in Inspector) ===")]
    [Tooltip("Spell toggles")]
    // I'm so sorry I can't do a list of Toggles so there has to be a separate one for literally every single spell toggle I apolagise profusely for how clogged this is gonna get
    [SerializeField] private Toggle fireballToggle;
    [SerializeField] private Toggle iceToggle;
    [SerializeField] private Toggle lightningToggle;
    [SerializeField] private Toggle shadowToggle;
    [SerializeField] private Toggle greenToggle;
    [SerializeField] private Toggle stoneToggle;
    [SerializeField] private Toggle chronosToggle;
    [SerializeField] private Toggle geminiToggle;
    [SerializeField] private Toggle piscesToggle;
    [SerializeField] private Toggle jollyToggle;
    [SerializeField] private Toggle blinkToggle;
    [SerializeField] private Toggle warpToggle;
    [SerializeField] private Toggle tetherToggle;
    [SerializeField] private Toggle mudToggle;
    [SerializeField] private Toggle gambitToggle;
    [SerializeField] private Toggle gorbinoToggle;

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
    // Spell Customisation bools I'm so sorry again
    [SerializeField] private bool fireballBool = true;
    [SerializeField] private bool iceBool = true;
    [SerializeField] private bool lightningBool = true;
    [SerializeField] private bool shadowBool = true;
    [SerializeField] private bool greenBool = true;
    [SerializeField] private bool stoneBool = true;
    [SerializeField] private bool chronosBool = true;
    [SerializeField] private bool geminiBool = true;
    [SerializeField] private bool piscesBool = true;
    [SerializeField] private bool jollyBool = true;
    [SerializeField] private bool blinkBool = true;
    [SerializeField] private bool warpBool = true;
    [SerializeField] private bool tetherBool = true;
    [SerializeField] private bool mudBool = true;
    [SerializeField] private bool gambitBool = true;
    [SerializeField] private bool gorbinoBool = true;


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
    // Spell Customisation you know how it goes now
    public bool FireballBool => fireballBool;
    public bool IceBool => iceBool;
    public bool LightningBool => lightningBool;
    public bool ShadowBool => shadowBool;
    public bool GreenBool => greenBool;
    public bool StoneBool => stoneBool;
    public bool ChronosBool => chronosBool;
    public bool GeminiBool => geminiBool;
    public bool PiscesBool => piscesBool;
    public bool JollyBool => jollyBool;
    public bool BlinkBool => blinkBool;
    public bool WarpBool => warpBool;
    public bool TetherBool => tetherBool;
    public bool MudBool => mudBool;
    public bool GambitBool => gambitBool;
    public bool GorbinoBool => gorbinoBool;


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
        if (HasSpellbookUIReferences())
        {
            HookSpellbookUI();
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
            UnhookSpellbookUI();
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
    private bool HasSpellbookUIReferences()
    {
        return fireballToggle != null || iceToggle != null ||
               lightningToggle != null || shadowToggle != null ||
               greenToggle != null || stoneToggle != null ||
               chronosToggle != null || geminiToggle != null ||
               piscesToggle != null || jollyToggle != null ||
               blinkToggle != null || warpToggle != null ||
               tetherToggle != null || mudToggle != null || 
               gambitToggle != null || gorbinoToggle != null;
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


    // OKAY SAME THING BUT FOR THE SPELL CUSTOMISATION ON THE SPELLBOOK SCREEN
    public void OnSpellbookOpened(
        Toggle toggle = null,
        Toggle toggle2 = null,
        Toggle toggle3 = null,
        Toggle toggle4 = null,
        Toggle toggle5 = null,
        Toggle toggle6 = null,
        Toggle toggle7 = null,
        Toggle toggle8 = null,
        Toggle toggle9 = null,
        Toggle toggle10 = null,
        Toggle toggle11 = null,
        Toggle toggle12 = null,
        Toggle toggle13 = null,
        Toggle toggle14 = null,
        Toggle toggle15 = null,
        Toggle toggle16 = null)
    {
        UnhookSpellbookUI();

        // Override inspector references if runtime references provided
        if (toggle != null) fireballToggle = toggle;
        if (toggle2 != null) iceToggle = toggle2;
        if (toggle3 != null) lightningToggle = toggle3;
        if (toggle4 != null) shadowToggle = toggle4;
        if (toggle5 != null) greenToggle = toggle5;
        if (toggle6 != null) stoneToggle = toggle6;
        if (toggle7 != null) chronosToggle = toggle7;
        if (toggle8 != null) geminiToggle = toggle8;
        if (toggle9 != null) piscesToggle = toggle9;
        if (toggle10 != null) jollyToggle = toggle10;
        if (toggle11 != null) blinkToggle = toggle11;
        if (toggle12 != null) warpToggle = toggle12;
        if (toggle13 != null) tetherToggle = toggle13;
        if (toggle14 != null) mudToggle = toggle14;
        if (toggle15 != null) gambitToggle = toggle15;
        if (toggle16 != null) gorbinoToggle = toggle16;

        HookSpellbookUI();
    }

    public void OnSpellbookClosed()
    {
        // Don't unhook if UI was assigned in inspector
        // This allows persistent UI elements to stay connected
    }

    private void HookSpellbookUI()
    {
        if (isUIHooked) return;
        
        if (fireballToggle != null)
        {
            fireballToggle.onValueChanged.RemoveAllListeners();
            fireballToggle.isOn = fireballBool;
            fireballToggle.onValueChanged.AddListener(SetFireball);
        }
        if (iceToggle != null)
        {
            iceToggle.onValueChanged.RemoveAllListeners();
            iceToggle.isOn = iceBool;
            iceToggle.onValueChanged.AddListener(SetIce);
        }
        if (lightningToggle != null)
        {
            lightningToggle.onValueChanged.RemoveAllListeners();
            lightningToggle.isOn = lightningBool;
            lightningToggle.onValueChanged.AddListener(SetLightning);
        }
        if (shadowToggle != null)
        {
            shadowToggle.onValueChanged.RemoveAllListeners();
            shadowToggle.isOn = shadowBool;
            shadowToggle.onValueChanged.AddListener(SetShadow);
        }
        if (greenToggle != null)
        {
            greenToggle.onValueChanged.RemoveAllListeners();
            greenToggle.isOn = greenBool;
            greenToggle.onValueChanged.AddListener(SetGreen);
        }
        if (stoneToggle != null)
        {
            stoneToggle.onValueChanged.RemoveAllListeners();
            stoneToggle.isOn = stoneBool;
            stoneToggle.onValueChanged.AddListener(SetStone);
        }
        if (chronosToggle != null)
        {
            chronosToggle.onValueChanged.RemoveAllListeners();
            chronosToggle.isOn = chronosBool;
            chronosToggle.onValueChanged.AddListener(SetChronos);
        }
        if (geminiToggle != null)
        {
            geminiToggle.onValueChanged.RemoveAllListeners();
            geminiToggle.isOn = geminiBool;
            geminiToggle.onValueChanged.AddListener(SetGemini);
        }
        if (piscesToggle != null)
        {
            piscesToggle.onValueChanged.RemoveAllListeners();
            piscesToggle.isOn = piscesBool;
            piscesToggle.onValueChanged.AddListener(SetPisces);
        }
        if (jollyToggle != null)
        {
            jollyToggle.onValueChanged.RemoveAllListeners();
            jollyToggle.isOn = jollyBool;
            jollyToggle.onValueChanged.AddListener(SetJolly);
        }
        if (blinkToggle != null)
        {
            blinkToggle.onValueChanged.RemoveAllListeners();
            blinkToggle.isOn = blinkBool;
            blinkToggle.onValueChanged.AddListener(SetBlink);
        }
        if (warpToggle != null)
        {
            warpToggle.onValueChanged.RemoveAllListeners();
            warpToggle.isOn = warpBool;
            warpToggle.onValueChanged.AddListener(SetWarp);
        }
        if (tetherToggle != null)
        {
            tetherToggle.onValueChanged.RemoveAllListeners();
            tetherToggle.isOn = tetherBool;
            tetherToggle.onValueChanged.AddListener(SetTether);
        }
        if (mudToggle != null)
        {
            mudToggle.onValueChanged.RemoveAllListeners();
            mudToggle.isOn = mudBool;
            mudToggle.onValueChanged.AddListener(SetMud);
        }
        if (gambitToggle != null)
        {
            gambitToggle.onValueChanged.RemoveAllListeners();
            gambitToggle.isOn = gambitBool;
            gambitToggle.onValueChanged.AddListener(SetGambit);
        }
        if (gorbinoToggle != null)
        {
            gorbinoToggle.onValueChanged.RemoveAllListeners();
            gorbinoToggle.isOn = gorbinoBool;
            gorbinoToggle.onValueChanged.AddListener(SetGorbino);
        }

        UpdateUILabel();
        isUIHooked = true;
    }

    private void UnhookSpellbookUI()
    {
        if (!isUIHooked) return;

        // Remove all listeners
        //fireballToggle?.onValueChanged.RemoveListener((isOn) => {
        //    SetSpellToggle(isOn, fireballBool, "FireballBool", fireballToggle);
        //});
        fireballToggle.onValueChanged.RemoveListener(SetFireball);
        iceToggle.onValueChanged.RemoveListener(SetIce);
        lightningToggle.onValueChanged.RemoveListener(SetLightning);
        shadowToggle.onValueChanged.RemoveListener(SetShadow);
        greenToggle.onValueChanged.RemoveListener(SetGreen);
        stoneToggle.onValueChanged.RemoveListener(SetStone);
        chronosToggle.onValueChanged.RemoveListener(SetChronos);
        geminiToggle.onValueChanged.RemoveListener(SetGemini);
        piscesToggle.onValueChanged.RemoveListener(SetPisces);
        jollyToggle.onValueChanged.RemoveListener(SetJolly);
        blinkToggle.onValueChanged.RemoveListener(SetBlink);
        warpToggle.onValueChanged.RemoveListener(SetWarp);
        tetherToggle.onValueChanged.RemoveListener(SetTether);
        mudToggle.onValueChanged.RemoveListener(SetMud);
        gambitToggle.onValueChanged.RemoveListener(SetGambit);
        gorbinoToggle.onValueChanged.RemoveListener(SetGorbino);

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

    // Spell Customisation
    public void SetFireball(bool enabled)
    {
        fireballBool = enabled;
        PlayerPrefs.SetInt("FireballBool", fireballBool ? 1 : 0);
        PlayerPrefs.Save();

        if (fireballToggle != null && fireballToggle.isOn != fireballBool)
            fireballToggle.isOn = fireballBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetIce(bool enabled)
    {
        iceBool = enabled;
        PlayerPrefs.SetInt("IceBool", iceBool ? 1 : 0);
        PlayerPrefs.Save();

        if (iceToggle != null && iceToggle.isOn != iceBool)
            iceToggle.isOn = iceBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetLightning(bool enabled)
    {
        lightningBool = enabled;
        PlayerPrefs.SetInt("LightningBool", lightningBool ? 1 : 0);
        PlayerPrefs.Save();

        if (lightningToggle != null && lightningToggle.isOn != lightningBool)
            lightningToggle.isOn = lightningBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetShadow(bool enabled)
    {
        shadowBool = enabled;
        PlayerPrefs.SetInt("ShadowBool", shadowBool ? 1 : 0);
        PlayerPrefs.Save();

        if (shadowToggle != null && shadowToggle.isOn != shadowBool)
            shadowToggle.isOn = shadowBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetGreen(bool enabled)
    {
        greenBool = enabled;
        PlayerPrefs.SetInt("GreenBool", greenBool ? 1 : 0);
        PlayerPrefs.Save();

        if (greenToggle != null && greenToggle.isOn != greenBool)
            greenToggle.isOn = greenBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetStone(bool enabled)
    {
        stoneBool = enabled;
        PlayerPrefs.SetInt("StoneBool", stoneBool ? 1 : 0);
        PlayerPrefs.Save();

        if (stoneToggle != null && stoneToggle.isOn != stoneBool)
            stoneToggle.isOn = stoneBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetChronos(bool enabled)
    {
        chronosBool = enabled;
        PlayerPrefs.SetInt("ChronosBool", chronosBool ? 1 : 0);
        PlayerPrefs.Save();

        if (chronosToggle != null && chronosToggle.isOn != chronosBool)
            chronosToggle.isOn = chronosBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetGemini(bool enabled)
    {
        geminiBool = enabled;
        PlayerPrefs.SetInt("GeminiBool", geminiBool ? 1 : 0);
        PlayerPrefs.Save();

        if (geminiToggle != null && geminiToggle.isOn != geminiBool)
            geminiToggle.isOn = geminiBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetPisces(bool enabled)
    {
        piscesBool = enabled;
        PlayerPrefs.SetInt("PiscesBool", piscesBool ? 1 : 0);
        PlayerPrefs.Save();

        if (piscesToggle != null && piscesToggle.isOn != piscesBool)
            piscesToggle.isOn = piscesBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetJolly(bool enabled)
    {
        jollyBool = enabled;
        PlayerPrefs.SetInt("JollyBool", jollyBool ? 1 : 0);
        PlayerPrefs.Save();

        if (jollyToggle != null && jollyToggle.isOn != jollyBool)
            jollyToggle.isOn = jollyBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetBlink(bool enabled)
    {
        blinkBool = enabled;
        PlayerPrefs.SetInt("BlinkBool", blinkBool ? 1 : 0);
        PlayerPrefs.Save();

        if (blinkToggle != null && blinkToggle.isOn != blinkBool)
            blinkToggle.isOn = blinkBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetWarp(bool enabled)
    {
        warpBool = enabled;
        PlayerPrefs.SetInt("WarpBool", warpBool ? 1 : 0);
        PlayerPrefs.Save();

        if (warpToggle != null && warpToggle.isOn != warpBool)
            warpToggle.isOn = warpBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetTether(bool enabled)
    {
        tetherBool = enabled;
        PlayerPrefs.SetInt("TetherBool", tetherBool ? 1 : 0);
        PlayerPrefs.Save();

        if (tetherToggle != null && tetherToggle.isOn != tetherBool)
            tetherToggle.isOn = tetherBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetMud(bool enabled)
    {
        mudBool = enabled;
        PlayerPrefs.SetInt("MudBool", mudBool ? 1 : 0);
        PlayerPrefs.Save();

        if (mudToggle != null && mudToggle.isOn != mudBool)
            mudToggle.isOn = mudBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetGambit(bool enabled)
    {
        gambitBool = enabled;
        PlayerPrefs.SetInt("GambitBool", gambitBool ? 1 : 0);
        PlayerPrefs.Save();

        if (gambitToggle != null && gambitToggle.isOn != gambitBool)
            gambitToggle.isOn = gambitBool;

        OnSettingsChanged?.Invoke();
    }
    public void SetGorbino(bool enabled)
    {
        gorbinoBool = enabled;
        PlayerPrefs.SetInt("GorbinoBool", gorbinoBool ? 1 : 0);
        PlayerPrefs.Save();

        if (gorbinoToggle != null && gorbinoToggle.isOn != gorbinoBool)
            gorbinoToggle.isOn = gorbinoBool;

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
        // Removed for compatibility with the Language switching, here for legacy
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
        // Spell Customisatoin
        fireballBool = PlayerPrefs.GetInt("FireballBool", 1) == 1;
        iceBool = PlayerPrefs.GetInt("IceBool", 1) == 1;
        lightningBool = PlayerPrefs.GetInt("LightningBool", 1) == 1;
        shadowBool = PlayerPrefs.GetInt("ShadowBool", 1) == 1;
        greenBool = PlayerPrefs.GetInt("GreenBool", 1) == 1;
        stoneBool = PlayerPrefs.GetInt("StoneBool", 1) == 1;
        chronosBool = PlayerPrefs.GetInt("ChronosBool", 1) == 1;
        geminiBool = PlayerPrefs.GetInt("GeminiBool", 1) == 1;
        piscesBool = PlayerPrefs.GetInt("PiscesBool", 1) == 1;
        jollyBool = PlayerPrefs.GetInt("JollyBool", 1) == 1;
        blinkBool = PlayerPrefs.GetInt("BlinkBool", 1) == 1;
        warpBool = PlayerPrefs.GetInt("WarpBool", 1) == 1;
        tetherBool = PlayerPrefs.GetInt("TetherBool", 1) == 1;
        mudBool = PlayerPrefs.GetInt("mudBool", 1) == 1;
        gambitBool = PlayerPrefs.GetInt("GambitBool", 1) == 1;
        gorbinoBool = PlayerPrefs.GetInt("GorbinoBool", 1) == 1;

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