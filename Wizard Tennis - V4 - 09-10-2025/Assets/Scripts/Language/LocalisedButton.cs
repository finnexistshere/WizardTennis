using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Localizes Button text (child Text or TextMeshProUGUI component)
/// </summary>
[RequireComponent(typeof(Button))]
public class LocalizedButton : MonoBehaviour
{
    [Header("Localized Button Text")]
    [SerializeField] private string defaultText = "Button";
    [SerializeField] private List<LocalizedTextEntry> localizedTexts = new List<LocalizedTextEntry>();

    private Text legacyText;
    private TextMeshProUGUI tmpText;

    private void Awake()
    {
        // Find text component in children
        legacyText = GetComponentInChildren<Text>();
        tmpText = GetComponentInChildren<TextMeshProUGUI>();

        if (legacyText == null && tmpText == null)
        {
            Debug.LogWarning($"[LocalizedButton] No Text or TextMeshProUGUI component found in children of {gameObject.name}");
        }
    }

    private void Start()
    {
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        UpdateText();
    }

    private void OnDestroy()
    {
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(Language newLanguage)
    {
        UpdateText();
    }

    public void UpdateText()
    {
        Language currentLang = OptionsManager.Instance != null
            ? OptionsManager.Instance.CurrentLanguage
            : Language.English;

        string text = GetLocalizedText(currentLang);

        if (legacyText != null)
            legacyText.text = text;

        if (tmpText != null)
            tmpText.text = text;
    }

    public string GetLocalizedText(Language language)
    {
        foreach (var entry in localizedTexts)
        {
            if (entry.language == language)
                return entry.text;
        }

        // Fallback to English
        foreach (var entry in localizedTexts)
        {
            if (entry.language == Language.English)
                return entry.text;
        }

        return defaultText;
    }

    public void SetLocalizedText(Language language, string text)
    {
        for (int i = 0; i < localizedTexts.Count; i++)
        {
            if (localizedTexts[i].language == language)
            {
                localizedTexts[i].text = text;
                return;
            }
        }

        localizedTexts.Add(new LocalizedTextEntry(language, text));
    }
}

/// <summary>
/// Localizes Dropdown/TMP_Dropdown options
/// </summary>
public class LocalizedDropdown : MonoBehaviour
{
    [System.Serializable]
    public class LocalizedOption
    {
        public string optionKey; // Unique identifier for this option
        public string defaultText;
        public List<LocalizedTextEntry> localizedTexts = new List<LocalizedTextEntry>();

        public string GetLocalizedText(Language language)
        {
            foreach (var entry in localizedTexts)
            {
                if (entry.language == language)
                    return entry.text;
            }

            // Fallback to English
            foreach (var entry in localizedTexts)
            {
                if (entry.language == Language.English)
                    return entry.text;
            }

            return defaultText;
        }
    }

    [Header("Localized Dropdown Options")]
    [SerializeField] private List<LocalizedOption> localizedOptions = new List<LocalizedOption>();

    private Dropdown legacyDropdown;
    private TMP_Dropdown tmpDropdown;
    private int currentValue;

    private void Awake()
    {
        legacyDropdown = GetComponent<Dropdown>();
        tmpDropdown = GetComponent<TMP_Dropdown>();

        if (legacyDropdown == null && tmpDropdown == null)
        {
            Debug.LogWarning($"[LocalizedDropdown] No Dropdown or TMP_Dropdown component found on {gameObject.name}");
        }
    }

    private void Start()
    {
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        // Store current value
        SaveCurrentValue();

        // Initial update
        UpdateOptions();
    }

    private void OnDestroy()
    {
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(Language newLanguage)
    {
        SaveCurrentValue();
        UpdateOptions();
        RestoreCurrentValue();
    }

    private void SaveCurrentValue()
    {
        if (legacyDropdown != null)
            currentValue = legacyDropdown.value;
        else if (tmpDropdown != null)
            currentValue = tmpDropdown.value;
    }

    private void RestoreCurrentValue()
    {
        if (legacyDropdown != null)
            legacyDropdown.value = currentValue;
        else if (tmpDropdown != null)
            tmpDropdown.value = currentValue;
    }

    public void UpdateOptions()
    {
        Language currentLang = OptionsManager.Instance != null
            ? OptionsManager.Instance.CurrentLanguage
            : Language.English;

        if (legacyDropdown != null)
        {
            legacyDropdown.ClearOptions();
            List<string> options = new List<string>();

            foreach (var option in localizedOptions)
            {
                options.Add(option.GetLocalizedText(currentLang));
            }

            legacyDropdown.AddOptions(options);
        }

        if (tmpDropdown != null)
        {
            tmpDropdown.ClearOptions();
            List<string> options = new List<string>();

            foreach (var option in localizedOptions)
            {
                options.Add(option.GetLocalizedText(currentLang));
            }

            tmpDropdown.AddOptions(options);
        }
    }

    /// <summary>
    /// Add a new option to the dropdown
    /// </summary>
    public void AddOption(string key, string defaultText)
    {
        LocalizedOption option = new LocalizedOption
        {
            optionKey = key,
            defaultText = defaultText,
            localizedTexts = new List<LocalizedTextEntry>()
        };

        localizedOptions.Add(option);
        UpdateOptions();
    }

    /// <summary>
    /// Set localized text for a specific option
    /// </summary>
    public void SetOptionText(string key, Language language, string text)
    {
        foreach (var option in localizedOptions)
        {
            if (option.optionKey == key)
            {
                bool found = false;
                foreach (var entry in option.localizedTexts)
                {
                    if (entry.language == language)
                    {
                        entry.text = text;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    option.localizedTexts.Add(new LocalizedTextEntry(language, text));
                }

                UpdateOptions();
                return;
            }
        }
    }
}

/// <summary>
/// Localizes InputField placeholder text
/// </summary>
public class LocalizedInputField : MonoBehaviour
{
    [Header("Placeholder Localization")]
    [SerializeField] private string defaultPlaceholder = "Enter text...";
    [SerializeField] private List<LocalizedTextEntry> localizedPlaceholders = new List<LocalizedTextEntry>();

    private InputField legacyInputField;
    private TMP_InputField tmpInputField;

    private void Awake()
    {
        legacyInputField = GetComponent<InputField>();
        tmpInputField = GetComponent<TMP_InputField>();

        if (legacyInputField == null && tmpInputField == null)
        {
            Debug.LogWarning($"[LocalizedInputField] No InputField or TMP_InputField found on {gameObject.name}");
        }
    }

    private void Start()
    {
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        UpdatePlaceholder();
    }

    private void OnDestroy()
    {
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(Language newLanguage)
    {
        UpdatePlaceholder();
    }

    public void UpdatePlaceholder()
    {
        Language currentLang = OptionsManager.Instance != null
            ? OptionsManager.Instance.CurrentLanguage
            : Language.English;

        string placeholder = GetLocalizedPlaceholder(currentLang);

        if (legacyInputField != null && legacyInputField.placeholder != null)
        {
            Text placeholderText = legacyInputField.placeholder.GetComponent<Text>();
            if (placeholderText != null)
                placeholderText.text = placeholder;
        }

        if (tmpInputField != null && tmpInputField.placeholder != null)
        {
            TextMeshProUGUI placeholderText = tmpInputField.placeholder.GetComponent<TextMeshProUGUI>();
            if (placeholderText != null)
                placeholderText.text = placeholder;
        }
    }

    public string GetLocalizedPlaceholder(Language language)
    {
        foreach (var entry in localizedPlaceholders)
        {
            if (entry.language == language)
                return entry.text;
        }

        // Fallback to English
        foreach (var entry in localizedPlaceholders)
        {
            if (entry.language == Language.English)
                return entry.text;
        }

        return defaultPlaceholder;
    }

    public void SetLocalizedPlaceholder(Language language, string text)
    {
        for (int i = 0; i < localizedPlaceholders.Count; i++)
        {
            if (localizedPlaceholders[i].language == language)
            {
                localizedPlaceholders[i].text = text;
                return;
            }
        }

        localizedPlaceholders.Add(new LocalizedTextEntry(language, text));
    }
}