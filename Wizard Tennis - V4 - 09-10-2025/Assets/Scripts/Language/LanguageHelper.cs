using System.Collections.Generic;
using UnityEngine;

// Language enum - must match OptionsManager
public enum Language
{
    English = 0,
    Spanish = 1,
    French = 2,
    German = 3,
    Italian = 4,
    Portuguese = 5,
    Russian = 6,
    Japanese = 7,
    Chinese = 8,
    Korean = 9
}

public static class LanguageHelper
{
    private static readonly Dictionary<Language, string> languageNames = new Dictionary<Language, string>
    {
        { Language.English, "English" },
        { Language.Spanish, "Español" },
        { Language.French, "Français" },
        { Language.German, "Deutsch" },
        { Language.Italian, "Italiano" },
        { Language.Portuguese, "Português" },
        { Language.Russian, "???????" },
        { Language.Japanese, "???" },
        { Language.Chinese, "??" },
        { Language.Korean, "???" }
    };

    private static readonly Dictionary<Language, string> languageCodes = new Dictionary<Language, string>
    {
        { Language.English, "en" },
        { Language.Spanish, "es" },
        { Language.French, "fr" },
        { Language.German, "de" },
        { Language.Italian, "it" },
        { Language.Portuguese, "pt" },
        { Language.Russian, "ru" },
        { Language.Japanese, "ja" },
        { Language.Chinese, "zh" },
        { Language.Korean, "ko" }
    };

    public static string GetLanguageName(Language language)
    {
        return languageNames.ContainsKey(language) ? languageNames[language] : "Unknown";
    }

    public static string GetLanguageCode(Language language)
    {
        return languageCodes.ContainsKey(language) ? languageCodes[language] : "en";
    }
}

/// <summary>
/// Stores localized spell names for different languages
/// </summary>
[System.Serializable]
public class LocalizedSpellName
{
    public Language language;
    public string localizedName;

    public LocalizedSpellName(Language lang, string name)
    {
        language = lang;
        localizedName = name;
    }
}

/// <summary>
/// Container for spell data with localization support
/// </summary>
[System.Serializable]
public class SpellData
{
    public string spellAddress;
    public string defaultName; // Fallback if translation missing
    public List<LocalizedSpellName> localizedNames = new List<LocalizedSpellName>();

    /// <summary>
    /// Get the spell name in the specified language, or default if not found
    /// </summary>
    public string GetLocalizedName(Language language)
    {
        foreach (var localized in localizedNames)
        {
            if (localized.language == language)
                return localized.localizedName;
        }

        // Fallback to English if available
        foreach (var localized in localizedNames)
        {
            if (localized.language == Language.English)
                return localized.localizedName;
        }

        // Final fallback to default name
        return defaultName;
    }

    /// <summary>
    /// Add or update a localized name
    /// </summary>
    public void SetLocalizedName(Language language, string name)
    {
        for (int i = 0; i < localizedNames.Count; i++)
        {
            if (localizedNames[i].language == language)
            {
                localizedNames[i].localizedName = name;
                return;
            }
        }

        localizedNames.Add(new LocalizedSpellName(language, name));
    }
}

/// <summary>
/// Manages spell localization at runtime
/// </summary>
public class SpellLocalizationManager : MonoBehaviour
{
    private static SpellLocalizationManager instance;
    public static SpellLocalizationManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SpellLocalizationManager");
                instance = go.AddComponent<SpellLocalizationManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private Dictionary<string, SpellData> spellDatabase = new Dictionary<string, SpellData>();
    private Language currentLanguage = Language.English;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to language changes from OptionsManager
        if (OptionsManager.Instance != null)
        {
            currentLanguage = OptionsManager.Instance.CurrentLanguage;
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }
    }

    private void Start()
    {
        // Delayed subscription in case OptionsManager spawns after this
        if (OptionsManager.Instance != null && currentLanguage != OptionsManager.Instance.CurrentLanguage)
        {
            currentLanguage = OptionsManager.Instance.CurrentLanguage;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            if (OptionsManager.Instance != null)
            {
                OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
            }
            instance = null;
        }
    }

    private void OnLanguageChanged(Language newLanguage)
    {
        currentLanguage = newLanguage;
        Debug.Log($"[SpellLocalizationManager] Language changed to: {LanguageHelper.GetLanguageName(newLanguage)}");

        // Notify all spellcasting components to update their UI
        BroadcastLanguageChange();
    }

    private void BroadcastLanguageChange()
    {
        // Update all Spellcasting components
        foreach (var sc in FindObjectsOfType<Spellcasting>())
        {
            sc.RefreshSpellBookUI();
        }

        // Update all NetworkedSpellcasting components
        foreach (var nsc in FindObjectsOfType<NetworkedSpellcasting>())
        {
            nsc.RefreshSpellBookUI();
        }
    }

    /// <summary>
    /// Register a spell with its localization data
    /// </summary>
    public void RegisterSpell(string address, SpellData data)
    {
        if (!spellDatabase.ContainsKey(address))
        {
            spellDatabase[address] = data;
        }
        else
        {
            // Merge localized names if spell already exists
            foreach (var localized in data.localizedNames)
            {
                spellDatabase[address].SetLocalizedName(localized.language, localized.localizedName);
            }
        }
    }

    /// <summary>
    /// Get localized spell name for current language
    /// </summary>
    public string GetLocalizedSpellName(string address, string fallbackName = "")
    {
        if (spellDatabase.ContainsKey(address))
        {
            return spellDatabase[address].GetLocalizedName(currentLanguage);
        }

        return !string.IsNullOrEmpty(fallbackName) ? fallbackName : address;
    }

    /// <summary>
    /// Get localized spell name for specific language
    /// </summary>
    public string GetLocalizedSpellName(string address, Language language, string fallbackName = "")
    {
        if (spellDatabase.ContainsKey(address))
        {
            return spellDatabase[address].GetLocalizedName(language);
        }

        return !string.IsNullOrEmpty(fallbackName) ? fallbackName : address;
    }

    public Language CurrentLanguage => currentLanguage;
}