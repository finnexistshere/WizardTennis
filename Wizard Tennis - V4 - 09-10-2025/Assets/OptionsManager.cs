using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance;

    [Header("UI References")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Toggle leftHandedToggle;
    [SerializeField] private TextMeshProUGUI modeLabel;

    public bool leftHandedMode { get; private set; }
    private float currentVolume = 0.75f;

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

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Try hooking up UI immediately if active
        HookUIIfActive();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnhookUI();
    }

    // --- Scene Management ---
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookUIIfActive();
    }

    private void HookUIIfActive()
    {
        // Only try if Options GameObject is active
        if (!gameObject.activeInHierarchy) return;

        StartCoroutine(FindAndHookUI());
    }

    private IEnumerator FindAndHookUI()
    {
        yield return null; // wait one frame
        yield return new WaitForSeconds(0.05f); // small delay to ensure UI exists

        if (volumeSlider == null)
            volumeSlider = FindObjectOfType<Slider>(true);
        if (leftHandedToggle == null)
            leftHandedToggle = FindObjectOfType<Toggle>(true);
        if (modeLabel == null)
            modeLabel = FindObjectOfType<TextMeshProUGUI>(true);

        HookUI();
    }

    private void HookUI()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
            volumeSlider.value = currentVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (leftHandedToggle != null)
        {
            leftHandedToggle.onValueChanged.RemoveAllListeners();
            leftHandedToggle.isOn = leftHandedMode;
            leftHandedToggle.onValueChanged.AddListener(SetLeftHandedMode);
        }

        UpdateUILabel();
    }

    private void UnhookUI()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveAllListeners();
        if (leftHandedToggle != null)
            leftHandedToggle.onValueChanged.RemoveAllListeners();
    }

    // --- Volume ---
    public void SetVolume(float value)
    {
        currentVolume = Mathf.Clamp01(value);
        AudioListener.volume = currentVolume;
        PlayerPrefs.SetFloat("Volume", currentVolume);
        PlayerPrefs.Save();
    }

    // --- Left-Handed Mode ---
    public void SetLeftHandedMode(bool enabled)
    {
        leftHandedMode = enabled;
        PlayerPrefs.SetInt("LeftHandedMode", leftHandedMode ? 1 : 0);
        PlayerPrefs.Save();
        UpdateUILabel();
    }

    private void UpdateUILabel()
    {
        if (modeLabel != null)
            modeLabel.text = leftHandedMode ? "Left-Handed Mode: ON" : "Left-Handed Mode: OFF";
    }

    // --- Load saved settings ---
    private void LoadSettings()
    {
        currentVolume = PlayerPrefs.GetFloat("Volume", 0.75f);
        leftHandedMode = PlayerPrefs.GetInt("LeftHandedMode", 0) == 1;
        AudioListener.volume = currentVolume;
    }
}
