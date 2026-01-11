using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

        UpdatePlaceholder();
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