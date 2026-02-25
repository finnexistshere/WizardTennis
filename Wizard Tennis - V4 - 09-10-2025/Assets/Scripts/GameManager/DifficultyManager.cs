using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Stores the tunable values for a single difficulty tier.
/// All fields are set via the Inspector — no magic numbers in code.
/// </summary>
[System.Serializable]
public class DifficultyPreset
{
    public string displayName = "Normal";

    [Header("Opponent Movement")]
    public float oppMinSpeed = 3f;
    public float oppMaxSpeed = 14f;
    public float oppReactionTime = 0.2f;

    [Header("Spell Spawning")]
    public float spellSpawnInterval = 7f;
    public int maxActivePickups = 4;

    [Header("Player Hit Radius")]
    public float playerHitRadius = 1f;
}

/// <summary>
/// Manages difficulty selection, persists the choice across scenes,
/// and applies values to OppHitting / GameManager / player collider
/// when the gameplay scene loads.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Difficulty Presets (set all values here)")]
    public DifficultyPreset[] presets = new DifficultyPreset[5];

    [Header("Menu UI (auto-resolved on scene load)")]
    public GameObject difficultyMenuPanel;
    public Button[] difficultyButtons;          // one per preset, in order
    public TextMeshProUGUI selectedLabel;        // shows current selection
    public Button confirmButton;
    public Button cancelButton;

    [Header("Scene Names")]
    public string gameplaySceneName = "Gameplay"; // name of your main gameplay scene

    // ── Runtime ───────────────────────────────────────────────────────────
    private int _selectedIndex = 2;             // default: middle preset (index 2 = "Normal")
    private int _appliedIndex = 2;

    // Keys
    private const string PREF_KEY = "SelectedDifficulty";

    // ──────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load last saved choice (default to middle preset)
        _selectedIndex = PlayerPrefs.GetInt(PREF_KEY, 2);
        _appliedIndex = _selectedIndex;

        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveUIReferences();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Scene events
    // ──────────────────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveUIReferences();

        if (scene.name == gameplaySceneName)
            ApplyDifficultyToScene();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  UI wiring — called automatically on scene load AND can be called
    //  manually from any button (e.g. a "Settings" button on the main menu)
    // ──────────────────────────────────────────────────────────────────────

    public void ResolveUIReferences()
    {
        // Find the panel regardless of active state
        if (difficultyMenuPanel == null)
        {
            // Search all root objects including inactive ones
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = root.transform.Find("DifficultyMenuPanel");
                if (found != null) { difficultyMenuPanel = found.gameObject; break; }
                if (root.name == "DifficultyMenuPanel") { difficultyMenuPanel = root; break; }
            }
        }

        if (difficultyMenuPanel == null) return;

        // Temporarily activate so GetComponentsInChildren works reliably
        bool wasActive = difficultyMenuPanel.activeSelf;
        difficultyMenuPanel.SetActive(true);

        WireButtons();

        // Restore previous state rather than always hiding
        difficultyMenuPanel.SetActive(wasActive);
    }

    private void WireButtons()
    {
        Button[] buttons = difficultyMenuPanel.GetComponentsInChildren<Button>(true);
        List<Button> presetButtons = new List<Button>();

        foreach (Button btn in buttons)
        {
            if (btn.name.StartsWith("Difficulty_"))
            {
                if (int.TryParse(btn.name.Substring("Difficulty_".Length), out int idx))
                {
                    while (presetButtons.Count <= idx) presetButtons.Add(null);
                    presetButtons[idx] = btn;

                    int captured = idx;

                    TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null && presets != null && idx < presets.Length)
                        label.text = presets[idx].displayName;
                }
            }
            else if (btn.name == "ConfirmDifficulty")
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ConfirmSelection);
                confirmButton = btn;
            }
            else if (btn.name == "CancelDifficulty")
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(CancelSelection);
                cancelButton = btn;
            }
        }

        difficultyButtons = presetButtons.ToArray();

        Transform labelTransform = difficultyMenuPanel.transform.Find("SelectedLabel");
        if (labelTransform != null)
            selectedLabel = labelTransform.GetComponent<TextMeshProUGUI>();

        RefreshButtonVisuals();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Public API — call these from other UI buttons / scripts
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Call this from a "Change Difficulty" button anywhere in your UI.</summary>
    public void ShowMenu()
    {
        if (difficultyMenuPanel == null) ResolveUIReferences();
        if (difficultyMenuPanel != null)
        {
            _selectedIndex = _appliedIndex; // reset pending selection
            RefreshButtonVisuals();
            difficultyMenuPanel.SetActive(true);
        }
    }

    public void HideMenu()
    {
        if (difficultyMenuPanel != null)
            difficultyMenuPanel.SetActive(false);
    }

    /// <summary>Returns the currently applied preset.</summary>
    public DifficultyPreset CurrentPreset =>
        (presets != null && presets.Length > _appliedIndex) ? presets[_appliedIndex] : null;

    // ──────────────────────────────────────────────────────────────────────
    //  Internal button handlers
    // ──────────────────────────────────────────────────────────────────────

    public void OnDifficultyButtonPressed(int index)
    {
        if (presets == null || index >= presets.Length) return;
        _selectedIndex = index;
        RefreshButtonVisuals();
    }

    public void ConfirmSelection()
    {
        _appliedIndex = _selectedIndex;
        PlayerPrefs.SetInt(PREF_KEY, _appliedIndex);
        PlayerPrefs.Save();

        // Apply immediately if we're already in the gameplay scene
        if (SceneManager.GetActiveScene().name == gameplaySceneName)
            ApplyDifficultyToScene();

        HideMenu();

        Debug.Log($"[DifficultyManager] Difficulty set to: {CurrentPreset?.displayName}");
    }

    public void CancelSelection()
    {
        _selectedIndex = _appliedIndex; // discard pending change
        RefreshButtonVisuals();
        HideMenu();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Apply to scene
    // ──────────────────────────────────────────────────────────────────────

    private void ApplyDifficultyToScene()
    {
        DifficultyPreset preset = CurrentPreset;
        if (preset == null)
        {
            Debug.LogWarning("[DifficultyManager] No preset found, skipping apply.");
            return;
        }

        ApplyToOpponent(preset);
        ApplyToGameManager(preset);
        ApplyToPlayer(preset);

        Debug.Log($"[DifficultyManager] Applied difficulty '{preset.displayName}' to scene '{SceneManager.GetActiveScene().name}'");
    }

    private void ApplyToOpponent(DifficultyPreset preset)
    {
        OppHitting opp = FindObjectOfType<OppHitting>();
        if (opp == null)
        {
            Debug.LogWarning("[DifficultyManager] OppHitting not found in scene.");
            return;
        }

        opp.minSpeed = preset.oppMinSpeed;
        opp.maxSpeed = preset.oppMaxSpeed;
        opp.reactionTime = preset.oppReactionTime;

        Debug.Log($"[DifficultyManager] Opponent → minSpeed:{preset.oppMinSpeed} maxSpeed:{preset.oppMaxSpeed} reactionTime:{preset.oppReactionTime}");
    }

    private void ApplyToGameManager(DifficultyPreset preset)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) gm = FindObjectOfType<GameManager>();

        if (gm == null)
        {
            Debug.LogWarning("[DifficultyManager] GameManager not found in scene.");
            return;
        }

        gm.spawnInterval = preset.spellSpawnInterval;
        gm.maxActivePickups = preset.maxActivePickups;

        Debug.Log($"[DifficultyManager] GameManager → spawnInterval:{preset.spellSpawnInterval} maxPickups:{preset.maxActivePickups}");
    }

    private void ApplyToPlayer(DifficultyPreset preset)
    {
        // Match how SpellEffects finds and modifies the player collider (CapsuleCollider.radius)
        GameObject player = GameObject.Find("Player_Singleplayer");
        if (player == null)
        {
            Debug.LogWarning("[DifficultyManager] Player_Singleplayer not found in scene.");
            return;
        }

        CapsuleCollider col = player.GetComponent<CapsuleCollider>();
        if (col == null)
        {
            Debug.LogWarning("[DifficultyManager] CapsuleCollider not found on player.");
            return;
        }

        col.radius = preset.playerHitRadius;

        Debug.Log($"[DifficultyManager] Player collider radius → {preset.playerHitRadius}");
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Visuals
    // ──────────────────────────────────────────────────────────────────────

    private void RefreshButtonVisuals()
    {
        if (difficultyButtons == null) return;

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;

            // Highlight the currently selected button
            ColorBlock cb = difficultyButtons[i].colors;
            cb.normalColor = (i == _selectedIndex)
                ? new Color(1f, 0.85f, 0.2f)   // gold = selected
                : Color.white;
            difficultyButtons[i].colors = cb;
        }

        if (selectedLabel != null && presets != null && _selectedIndex < presets.Length)
            selectedLabel.text = presets[_selectedIndex].displayName;
    }
}