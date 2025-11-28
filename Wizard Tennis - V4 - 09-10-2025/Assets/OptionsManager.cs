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

    // These are temporary references, assigned when menu opens
    private Slider volumeSlider;
    private Toggle leftHandedToggle;
    private TextMeshProUGUI modeLabel;
    private TMP_Dropdown dropdown;
    private Toggle spellTipsToggle;

    public int bgmValue;
    public AudioClip[] bgmOptions;
    public AudioClip bgm;

    public bool SpellTips => spellTips;

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

    /// <summary>
    /// Call this whenever the options menu is opened in the scene.
    /// Pass the UI references from the scene.
    /// </summary>
    public void OnOptionsMenuOpened(
        Slider slider,
        Toggle toggle,
        TextMeshProUGUI label,
        TMP_Dropdown drop = null,  // Make optional
        Toggle toggle2 = null)      // Make optional
    {
        volumeSlider = slider;
        leftHandedToggle = toggle;
        modeLabel = label;
        dropdown = drop;
        spellTipsToggle = toggle2;

        HookUI();
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
            dropdown.onValueChanged.AddListener(delegate { musicDropDownChange(); });
        }

        if (spellTipsToggle != null)
        {
            spellTipsToggle.onValueChanged.RemoveAllListeners();
            spellTipsToggle.isOn = spellTips;
            spellTipsToggle.onValueChanged.AddListener(SetSpellTips);
        }

        UpdateUILabel();
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("Volume", volume);
        PlayerPrefs.Save();

        if (volumeSlider != null && volumeSlider.value != volume)
            volumeSlider.value = volume;
    }

    public void SetLeftHandedMode(bool enabled)
    {
        leftHandedMode = enabled;
        PlayerPrefs.SetInt("LeftHandedMode", leftHandedMode ? 1 : 0);
        PlayerPrefs.Save();

        UpdateUILabel();

        if (leftHandedToggle != null && leftHandedToggle.isOn != leftHandedMode)
            leftHandedToggle.isOn = leftHandedMode;
    }

    private void UpdateUILabel()
    {
        if (modeLabel != null)
            modeLabel.text = leftHandedMode ? "Left-Handed Mode: ON" : "Left-Handed Mode: OFF";
    }

    private void LoadSettings()
    {
        volume = PlayerPrefs.GetFloat("Volume", 0.75f);
        leftHandedMode = PlayerPrefs.GetInt("LeftHandedMode", 0) == 1;
        bgmValue = PlayerPrefs.GetInt("bgm", 0);
        bgm = bgmOptions[bgmValue];
        AudioListener.volume = volume;
        spellTips = PlayerPrefs.GetInt("spellTips", 1) == 0;
    }

    public void musicDropDownChange() 
    {
        bgmValue = dropdown.value;
        PlayerPrefs.SetInt("bgm", bgmValue);
        bgm = bgmOptions[dropdown.value];
    }

    public void SetSpellTips(bool enabled)
    {
        spellTips = enabled;
        PlayerPrefs.SetInt("spellTips", spellTips ? 1 : 0);
        PlayerPrefs.Save();

        //UpdateUILabel();

        if (spellTipsToggle != null && spellTipsToggle.isOn != spellTips)
            spellTipsToggle.isOn = spellTips;
    }
}
