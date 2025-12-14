using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance;

    [Header("Current Settings")]
    [SerializeField] private float volume = 0.75f;
    [SerializeField] public bool leftHandedMode = false;
    public bool spellTips = true;

    public float Volume => volume;
    public bool LeftHandedMode => leftHandedMode;
    public bool SpellTips => spellTips;

    // These are temporary references, assigned when menu opens
    private Slider volumeSlider;
    private Toggle leftHandedToggle;
    private TextMeshProUGUI modeLabel;
    private TMP_Dropdown dropdown;
    private Toggle spellTipsToggle;

    public int bgmValue;
    public AudioClip[] bgmOptions;
    public AudioClip bgm;

    // Track if UI is currently hooked to prevent errors
    private bool isUIHooked = false;

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
    }

    private void OnDestroy()
    {
        // Clean up when destroyed
        if (Instance == this)
        {
            UnhookUI();
            Instance = null;
        }
    }

    /// <summary>
    /// Call this whenever the options menu is opened in the scene.
    /// Pass the UI references from the scene.
    /// </summary>
    public void OnOptionsMenuOpened(
        Slider slider,
        Toggle toggle,
        TextMeshProUGUI label,
        TMP_Dropdown drop = null,
        Toggle toggle2 = null)
    {
        // Unhook previous UI first (in case menu was opened twice)
        UnhookUI();

        volumeSlider = slider;
        leftHandedToggle = toggle;
        modeLabel = label;
        dropdown = drop;
        spellTipsToggle = toggle2;

        HookUI();
    }

    /// <summary>
    /// Call this when the options menu is closed to clean up references
    /// </summary>
    public void OnOptionsMenuClosed()
    {
        UnhookUI();
    }

    private void HookUI()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.value = volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (leftHandedToggle != null)
        {
            leftHandedToggle.onValueChanged.RemoveAllListeners();
            leftHandedToggle.isOn = leftHandedMode;
            leftHandedToggle.onValueChanged.AddListener(SetLeftHandedMode);
        }

        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveAllListeners();
            dropdown.value = bgmValue;
            dropdown.onValueChanged.AddListener((int value) => MusicDropDownChange(value));
        }

        if (spellTipsToggle != null)
        {
            spellTipsToggle.onValueChanged.RemoveAllListeners();
            spellTipsToggle.isOn = spellTips;
            spellTipsToggle.onValueChanged.AddListener(SetSpellTips);
        }

        UpdateUILabel();
        isUIHooked = true;
    }

    private void UnhookUI()
    {
        if (!isUIHooked) return;

        // Remove listeners before clearing references to prevent memory leaks
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(SetVolume);
        }

        if (leftHandedToggle != null)
        {
            leftHandedToggle.onValueChanged.RemoveListener(SetLeftHandedMode);
        }

        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener((int value) => MusicDropDownChange(value));
        }

        if (spellTipsToggle != null)
        {
            spellTipsToggle.onValueChanged.RemoveListener(SetSpellTips);
        }

        // Clear all UI references
        volumeSlider = null;
        leftHandedToggle = null;
        modeLabel = null;
        dropdown = null;
        spellTipsToggle = null;

        isUIHooked = false;
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("Volume", volume);
        PlayerPrefs.Save();

        // Only update UI if it's currently hooked and valid
        if (isUIHooked && volumeSlider != null && !Mathf.Approximately(volumeSlider.value, volume))
        {
            volumeSlider.value = volume;
        }
    }

    public void SetLeftHandedMode(bool enabled)
    {
        leftHandedMode = enabled;
        PlayerPrefs.SetInt("LeftHandedMode", leftHandedMode ? 1 : 0);
        PlayerPrefs.Save();

        UpdateUILabel();

        // Only update UI if it's currently hooked and valid
        if (isUIHooked && leftHandedToggle != null && leftHandedToggle.isOn != leftHandedMode)
        {
            leftHandedToggle.isOn = leftHandedMode;
        }
    }

    public void SetSpellTips(bool enabled)
    {
        spellTips = enabled;
        PlayerPrefs.SetInt("spellTips", spellTips ? 1 : 0);
        PlayerPrefs.Save();

        // Only update UI if it's currently hooked and valid
        if (isUIHooked && spellTipsToggle != null && spellTipsToggle.isOn != spellTips)
        {
            spellTipsToggle.isOn = spellTips;
        }
    }

    private void UpdateUILabel()
    {
        if (isUIHooked && modeLabel != null)
        {
            modeLabel.text = leftHandedMode ? "Left-Handed Mode: ON" : "Left-Handed Mode: OFF";
        }
    }

    private void LoadSettings()
    {
        volume = PlayerPrefs.GetFloat("Volume", 0.75f);
        leftHandedMode = PlayerPrefs.GetInt("LeftHandedMode", 0) == 1;
        bgmValue = PlayerPrefs.GetInt("bgm", 0);
        spellTips = PlayerPrefs.GetInt("spellTips", 1) == 1; // Fixed: was inverted (1 should mean enabled)

        // Safeguard: Make sure bgmValue is within array bounds
        if (bgmOptions != null && bgmOptions.Length > 0)
        {
            bgmValue = Mathf.Clamp(bgmValue, 0, bgmOptions.Length - 1);
            bgm = bgmOptions[bgmValue];
        }
        else
        {
            Debug.LogWarning("[OptionsManager] No BGM options available!");
            bgm = null;
        }

        AudioListener.volume = volume;
    }

    private void MusicDropDownChange(int value)
    {
        bgmValue = value;
        PlayerPrefs.SetInt("bgm", bgmValue);
        PlayerPrefs.Save();

        // Safeguard: Check array bounds
        if (bgmOptions != null && bgmValue >= 0 && bgmValue < bgmOptions.Length)
        {
            bgm = bgmOptions[bgmValue];
        }
        else
        {
            Debug.LogWarning($"[OptionsManager] BGM value {bgmValue} is out of range!");
        }
    }

    /// <summary>
    /// Public method to get current settings without UI
    /// Useful for systems that need to query settings from other scenes
    /// </summary>
    public void ApplyCurrentSettings()
    {
        AudioListener.volume = volume;
        // Add other settings application here if needed
    }
}