using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized manager for broadcasting language changes to all localized components.
/// Maintains a registry of all localized components for efficient updates.
/// </summary>
public class LocalizationBroadcastManager : MonoBehaviour
{
    private static LocalizationBroadcastManager instance;
    public static LocalizationBroadcastManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("LocalizationBroadcastManager");
                instance = go.AddComponent<LocalizationBroadcastManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // Registries for all localized components
    private HashSet<LocalizedText> registeredTexts = new HashSet<LocalizedText>();
    private HashSet<LocalizedButton> registeredButtons = new HashSet<LocalizedButton>();
    private HashSet<LocalizedDropdown> registeredDropdowns = new HashSet<LocalizedDropdown>();
    private HashSet<LocalizedInputField> registeredInputFields = new HashSet<LocalizedInputField>();

    // Performance tracking
    private int totalRegistered = 0;
    private bool enableDebugLogs = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to OptionsManager language changes
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }
    }

    private void Start()
    {
        // Delayed subscription in case OptionsManager spawns after this
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;

            // Force an initial update to sync with current language
            Language currentLang = OptionsManager.Instance.CurrentLanguage;
            Debug.Log($"[LocalizationBroadcast] Initializing with current language: {LanguageHelper.GetLanguageName(currentLang)}");

            // Wait a frame for all components to register, then update
            StartCoroutine(InitialLanguageSync());
        }
        else
        {
            Debug.LogWarning("[LocalizationBroadcast] OptionsManager not found during Start! Will retry...");
            StartCoroutine(RetryOptionsManagerConnection());
        }
    }

    /// <summary>
    /// Wait for components to register, then sync with current language
    /// </summary>
    private System.Collections.IEnumerator InitialLanguageSync()
    {
        // Wait 2 frames to ensure all Start() methods have run
        yield return null;
        yield return null;

        if (OptionsManager.Instance != null)
        {
            Language currentLang = OptionsManager.Instance.CurrentLanguage;
            Debug.Log($"[LocalizationBroadcast] Syncing {totalRegistered} components to language: {LanguageHelper.GetLanguageName(currentLang)}");

            // Use scene search to catch any components that haven't registered yet
            BroadcastToAll(includeInactive: false);
        }
    }

    /// <summary>
    /// Retry connecting to OptionsManager if it wasn't ready at Start
    /// </summary>
    private System.Collections.IEnumerator RetryOptionsManagerConnection()
    {
        int attempts = 0;
        int maxAttempts = 10;

        while (attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.5f);
            attempts++;

            if (OptionsManager.Instance != null)
            {
                Debug.Log($"[LocalizationBroadcast] Connected to OptionsManager on attempt {attempts}");
                OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;

                // Sync with current language
                StartCoroutine(InitialLanguageSync());
                yield break;
            }
        }

        Debug.LogError($"[LocalizationBroadcast] Failed to connect to OptionsManager after {maxAttempts} attempts!");
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

    #region Registration

    public void Register(LocalizedText component)
    {
        if (component != null && registeredTexts.Add(component))
        {
            totalRegistered++;
            if (enableDebugLogs)
                Debug.Log($"[LocalizationBroadcast] Registered LocalizedText: {component.gameObject.name}");
        }
    }

    public void Register(LocalizedButton component)
    {
        if (component != null && registeredButtons.Add(component))
        {
            totalRegistered++;
            if (enableDebugLogs)
                Debug.Log($"[LocalizationBroadcast] Registered LocalizedButton: {component.gameObject.name}");
        }
    }

    public void Register(LocalizedDropdown component)
    {
        if (component != null && registeredDropdowns.Add(component))
        {
            totalRegistered++;
            if (enableDebugLogs)
                Debug.Log($"[LocalizationBroadcast] Registered LocalizedDropdown: {component.gameObject.name}");
        }
    }

    public void Register(LocalizedInputField component)
    {
        if (component != null && registeredInputFields.Add(component))
        {
            totalRegistered++;
            if (enableDebugLogs)
                Debug.Log($"[LocalizationBroadcast] Registered LocalizedInputField: {component.gameObject.name}");
        }
    }

    public void Unregister(LocalizedText component)
    {
        if (component != null && registeredTexts.Remove(component))
        {
            totalRegistered--;
        }
    }

    public void Unregister(LocalizedButton component)
    {
        if (component != null && registeredButtons.Remove(component))
        {
            totalRegistered--;
        }
    }

    public void Unregister(LocalizedDropdown component)
    {
        if (component != null && registeredDropdowns.Remove(component))
        {
            totalRegistered--;
        }
    }

    public void Unregister(LocalizedInputField component)
    {
        if (component != null && registeredInputFields.Remove(component))
        {
            totalRegistered--;
        }
    }

    #endregion

    #region Language Change Handling

    private void OnLanguageChanged(Language newLanguage)
    {
        Debug.Log($"[LocalizationBroadcast] Language changed to {LanguageHelper.GetLanguageName(newLanguage)}, updating {totalRegistered} components...");
        BroadcastToRegistered();
    }

    /// <summary>
    /// Broadcast language change to all registered components (fast)
    /// </summary>
    private void BroadcastToRegistered()
    {
        int updated = 0;

        // Clean up null references
        registeredTexts.RemoveWhere(item => item == null);
        registeredButtons.RemoveWhere(item => item == null);
        registeredDropdowns.RemoveWhere(item => item == null);
        registeredInputFields.RemoveWhere(item => item == null);

        // Update all registered components
        foreach (var text in registeredTexts)
        {
            if (text != null)
            {
                text.UpdateText();
                updated++;
            }
        }

        foreach (var button in registeredButtons)
        {
            if (button != null)
            {
                button.UpdateText();
                updated++;
            }
        }

        foreach (var dropdown in registeredDropdowns)
        {
            if (dropdown != null)
            {
                dropdown.UpdateOptions();
                updated++;
            }
        }

        foreach (var inputField in registeredInputFields)
        {
            if (inputField != null)
            {
                inputField.UpdatePlaceholder();
                updated++;
            }
        }

        Debug.Log($"[LocalizationBroadcast] Updated {updated} registered components");
    }

    /// <summary>
    /// Find and update all localized components in the scene (slower, but catches unregistered components)
    /// Use this when loading new scenes or as a fallback
    /// </summary>
    public void BroadcastToAll(bool includeInactive = false)
    {
        Debug.Log($"[LocalizationBroadcast] Broadcasting to all components (includeInactive: {includeInactive})...");

        int updated = 0;

        // Find and update all LocalizedText components
        foreach (var component in FindObjectsOfType<LocalizedText>(includeInactive))
        {
            component.UpdateText();
            updated++;
        }

        // Find and update all LocalizedButton components
        foreach (var component in FindObjectsOfType<LocalizedButton>(includeInactive))
        {
            component.UpdateText();
            updated++;
        }

        // Find and update all LocalizedDropdown components
        foreach (var component in FindObjectsOfType<LocalizedDropdown>(includeInactive))
        {
            component.UpdateOptions();
            updated++;
        }

        // Find and update all LocalizedInputField components
        foreach (var component in FindObjectsOfType<LocalizedInputField>(includeInactive))
        {
            component.UpdatePlaceholder();
            updated++;
        }

        Debug.Log($"[LocalizationBroadcast] Updated {updated} components via scene search");
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Force sync all UI to the current language setting
    /// Useful after scene loads or when ensuring everything is up-to-date
    /// </summary>
    public void SyncToCurrentLanguage(bool includeInactive = false)
    {
        if (OptionsManager.Instance == null)
        {
            Debug.LogWarning("[LocalizationBroadcast] Cannot sync - OptionsManager not found!");
            return;
        }

        Language currentLang = OptionsManager.Instance.CurrentLanguage;
        Debug.Log($"[LocalizationBroadcast] Syncing all UI to current language: {LanguageHelper.GetLanguageName(currentLang)}");

        BroadcastToAll(includeInactive);
    }

    /// <summary>
    /// Get count of registered components
    /// </summary>
    public int GetRegisteredCount()
    {
        return totalRegistered;
    }

    /// <summary>
    /// Get detailed registration info
    /// </summary>
    public void LogRegistrationInfo()
    {
        Debug.Log($"[LocalizationBroadcast] Registration Info:\n" +
                  $"  LocalizedText: {registeredTexts.Count}\n" +
                  $"  LocalizedButton: {registeredButtons.Count}\n" +
                  $"  LocalizedDropdown: {registeredDropdowns.Count}\n" +
                  $"  LocalizedInputField: {registeredInputFields.Count}\n" +
                  $"  Total: {totalRegistered}");
    }

    /// <summary>
    /// Clear all null references from registries
    /// </summary>
    public void CleanupRegistries()
    {
        int before = totalRegistered;

        registeredTexts.RemoveWhere(item => item == null);
        registeredButtons.RemoveWhere(item => item == null);
        registeredDropdowns.RemoveWhere(item => item == null);
        registeredInputFields.RemoveWhere(item => item == null);

        totalRegistered = registeredTexts.Count + registeredButtons.Count +
                         registeredDropdowns.Count + registeredInputFields.Count;

        int removed = before - totalRegistered;
        if (removed > 0)
        {
            Debug.Log($"[LocalizationBroadcast] Cleaned up {removed} null references");
        }
    }

    /// <summary>
    /// Enable/disable debug logging
    /// </summary>
    public void SetDebugLogging(bool enabled)
    {
        enableDebugLogs = enabled;
        Debug.Log($"[LocalizationBroadcast] Debug logging {(enabled ? "enabled" : "disabled")}");
    }

    #endregion
}