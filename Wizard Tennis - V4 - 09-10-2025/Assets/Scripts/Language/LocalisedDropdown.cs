using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
        // Register with broadcast manager
        if (LocalizationBroadcastManager.Instance != null)
        {
            LocalizationBroadcastManager.Instance.Register(this);
        }

        // Subscribe to language changes (legacy support)
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
        // Unregister from broadcast manager
        if (LocalizationBroadcastManager.Instance != null)
        {
            LocalizationBroadcastManager.Instance.Unregister(this);
        }

        // Unsubscribe from language changes (legacy support)
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