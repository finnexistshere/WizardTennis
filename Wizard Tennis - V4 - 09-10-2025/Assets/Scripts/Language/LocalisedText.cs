using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Attach this to any UI element (Text, TextMeshProUGUI, Button, etc.) to make it localizable.
/// Automatically updates when the language changes in OptionsManager.
/// </summary>
public class LocalizedText : MonoBehaviour
{
    [Header("Localized Text Settings")]
    [Tooltip("The default text to display (fallback if no translation exists)")]
    [SerializeField] private string defaultText = "";

    [Tooltip("List of translations for different languages")]
    [SerializeField] private List<LocalizedTextEntry> localizedTexts = new List<LocalizedTextEntry>();

    [Header("Component References (Auto-detected)")]
    [Tooltip("Text component to update (auto-detected if not assigned)")]
    [SerializeField] private Text legacyText;

    [Tooltip("TextMeshPro UGUI component to update (auto-detected if not assigned)")]
    [SerializeField] private TextMeshProUGUI tmpText;

    [Tooltip("TextMeshPro component to update (auto-detected if not assigned)")]
    [SerializeField] private TextMeshPro tmp3DText;

    [Header("Advanced Options")]
    [Tooltip("Update on Start even if language hasn't changed")]
    [SerializeField] private bool updateOnStart = true;

    [Tooltip("Format string arguments (e.g., 'Score: {0}')")]
    [SerializeField] private bool useFormatting = false;

    private object[] formatArgs = null;

    private void Awake()
    {
        // Auto-detect text components if not assigned
        if (legacyText == null)
            legacyText = GetComponent<Text>();

        if (tmpText == null)
            tmpText = GetComponent<TextMeshProUGUI>();

        if (tmp3DText == null)
            tmp3DText = GetComponent<TextMeshPro>();

        // If no text component found, warn
        if (legacyText == null && tmpText == null && tmp3DText == null)
        {
            Debug.LogWarning($"[LocalizedText] No Text or TextMeshPro component found on {gameObject.name}");
        }
    }

    private void Start()
    {
        // Register with the broadcast manager for automatic updates
        if (LocalizationBroadcastManager.Instance != null)
        {
            LocalizationBroadcastManager.Instance.Register(this);
        }

        // Subscribe to language changes (legacy support)
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        // Initial update
        if (updateOnStart)
        {
            UpdateText();
        }
    }

    private void OnEnable()
    {
        // Update text when re-enabled
        UpdateText();
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
        UpdateText();
    }

    /// <summary>
    /// Update the text component with the current language
    /// </summary>
    public void UpdateText()
    {
        Language currentLang = OptionsManager.Instance != null
            ? OptionsManager.Instance.CurrentLanguage
            : Language.English;

        string text = GetLocalizedText(currentLang);

        // Apply formatting if enabled
        if (useFormatting && formatArgs != null && formatArgs.Length > 0)
        {
            try
            {
                text = string.Format(text, formatArgs);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LocalizedText] Formatting error on {gameObject.name}: {e.Message}");
            }
        }

        SetText(text);
    }

    /// <summary>
    /// Set format arguments for string formatting (e.g., "Score: {0}")
    /// </summary>
    public void SetFormatArgs(params object[] args)
    {
        formatArgs = args;
        if (useFormatting)
        {
            UpdateText();
        }
    }

    /// <summary>
    /// Get the localized text for a specific language
    /// </summary>
    public string GetLocalizedText(Language language)
    {
        // Try to find exact language match
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

        // Final fallback to default text
        return defaultText;
    }

    /// <summary>
    /// Add or update a localized text entry
    /// </summary>
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

    /// <summary>
    /// Set the text on the appropriate component
    /// </summary>
    private void SetText(string text)
    {
        if (legacyText != null)
            legacyText.text = text;

        if (tmpText != null)
            tmpText.text = text;

        if (tmp3DText != null)
            tmp3DText.text = text;
    }

    /// <summary>
    /// Get the current text from the component
    /// </summary>
    public string GetCurrentText()
    {
        if (tmpText != null)
            return tmpText.text;

        if (legacyText != null)
            return legacyText.text;

        if (tmp3DText != null)
            return tmp3DText.text;

        return "";
    }

    /// <summary>
    /// Set the default text (used as fallback)
    /// </summary>
    public void SetDefaultText(string text)
    {
        defaultText = text;
        UpdateText();
    }

    public string DefaultText => defaultText;
    public List<LocalizedTextEntry> LocalizedTexts => localizedTexts;
}

/// <summary>
/// Stores a single translation for a specific language
/// </summary>
[System.Serializable]
public class LocalizedTextEntry
{
    public Language language;
    [TextArea(2, 5)]
    public string text;

    public LocalizedTextEntry(Language lang, string txt)
    {
        language = lang;
        text = txt;
    }
}