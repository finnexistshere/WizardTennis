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