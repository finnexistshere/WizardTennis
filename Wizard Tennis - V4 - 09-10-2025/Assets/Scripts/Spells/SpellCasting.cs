using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class Spellcasting : MonoBehaviour
{
    // --- Spell Dictionaries ---
    public Dictionary<string, string> spellBook = new Dictionary<string, string>();
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();
    public Dictionary<string, GameObject> spellVisuals = new Dictionary<string, GameObject>();
    public Dictionary<string, bool> boolBook = new Dictionary<string, bool>();
    public Dictionary<string, Color> spellColors = new Dictionary<string, Color>();
    public Dictionary<string, AudioClip> spellAudio = new Dictionary<string, AudioClip>();
    public Dictionary<string, AudioClip> wizardAudio = new Dictionary<string, AudioClip>();

    // --- UI References ---
    [Header("UI References")]
    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public SpellTextEntry spellTextPrefab;

    // --- Ball Visuals ---
    [Header("Ball Visual")]
    public GameObject parentObject;
    public GameObject baseEffectObject;
    private GameObject currentVisualInstance;

    [Header("Spell Settings")]
    [Tooltip("Time in seconds before returning to the base ball and clearing effect")]
    public float spellDuration = 5f;

    [Header("Particle Systems")]
    public ParticleSystem hitParticle;
    public SpellParticleColor spellParticleColor;

    [Tooltip("Time before spell input auto-clears if no further input is given")]
    public float inputTimeout = 2f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip SpellInputClick;
    public AudioClip spellRegisterSound;

    // --- Spellcasting State ---
    public string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false; // prevents new spells while one is active

    public TennisAI TennisAi;
    public SpellFloorImage spellFloorImage;
    public SpellEffects SpellEffects;

    private Color floorVisualColor;

    public bool leftHandedMode = false; // false = arrows, true = WASD


    private void Awake()
    {
        spellBookPanel.SetActive(true);
        lastInputTime = -inputTimeout;

        // Read left-handed mode from OptionsManager on spawn
        if (OptionsManager.Instance != null)
            SetLeftHandedMode(OptionsManager.Instance.leftHandedMode);
    }

    private void Update()
    {
        // Lock out spell input while a spell is active
        if (isCasting)
            return;

        UpdateSpellBook();

        // Auto-clear input if timeout reached
        if (!string.IsNullOrEmpty(inputSpellAddress) && Time.time - lastInputTime >= inputTimeout)
        {
            Debug.Log("Input timeout reached, clearing input...");
            inputSpellAddress = "";
            UpdateSpellBook();
        }

        // --- Spellcasting inputs ---
        if (CheckSpellInput(out string direction))
        {
            RegisterInput(direction);
        }

        // --- Check completed spell ---
        if (!string.IsNullOrEmpty(inputSpellAddress) && spellBook.ContainsKey(inputSpellAddress))
        {
            CheckSpell();
        }
    }
    // --- Update left-handed mode ---
    public void SetLeftHandedMode(bool enabled)
    {
        leftHandedMode = enabled;
    }

    private bool CheckSpellInput(out string direction)
    {
        direction = "";

        // Default: Arrow keys
        KeyCode leftKey = KeyCode.LeftArrow;
        KeyCode rightKey = KeyCode.RightArrow;
        KeyCode upKey = KeyCode.UpArrow;
        KeyCode downKey = KeyCode.DownArrow;

        // If OptionsManager exists and left-handed mode is ON, use WASD for spells
        if (OptionsManager.Instance != null && OptionsManager.Instance.leftHandedMode)
        {
            leftKey = KeyCode.A;
            rightKey = KeyCode.D;
            upKey = KeyCode.W;
            downKey = KeyCode.S;
        }
        if (Input.GetKeyDown(leftKey)) direction = "a";
        if (Input.GetKeyDown(rightKey)) direction = "A";
        if (Input.GetKeyDown(upKey)) direction = "B";
        if (Input.GetKeyDown(downKey)) direction = "b";

        return !string.IsNullOrEmpty(direction);
    }

    private void RegisterInput(string direction)
    {
        audioSource.PlayOneShot(SpellInputClick);
        inputSpellAddress += direction;
        lastInputTime = Time.time;
        UpdateSpellBook();
    }

    private void CheckSpell()
    {
        if (spellBook.ContainsKey(inputSpellAddress))
        {
            string spellName = spellBook[inputSpellAddress];
            if (isCasting)
            {
                Debug.Log("Spell blocked: another spell is still active.");
                return;
            }

            isCasting = true; // Lock new spellcasting
            currentActiveSpell = spellName;
            audioSource.PlayOneShot(spellRegisterSound); // Feedback to the player to confirm they cast a spell

            //TennisAi.ApplyBuff(value, spellName);

            SpellEffects.spellName = spellBook[inputSpellAddress];

            if (boolBook[spellName])
            {
                SpellEffects.plrHitSpell = true;
            }
            else
            {
                SpellEffects.plrHitSpell = false;
                SpellEffects.castSpell();
            }
            RemoveSpell(inputSpellAddress);
        }
        else
        {
            Debug.Log("Spell not found");
        }

        inputSpellAddress = "";
        UpdateSpellBook();
    }

    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        if (baseEffect != null)
            baseEffect.SetActive(false);

        currentVisualInstance = Instantiate(newPrefab, parentTransform);
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;
    }

    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect)
    {
        yield return new WaitForSeconds(delay);

        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
            currentVisualInstance = null;
        }

        if (baseEffect != null)
            baseEffect.SetActive(true);

        spellParticleColor.ResetColor();

        if (!string.IsNullOrEmpty(currentActiveSpell))
        {
            TennisAi.ClearEffects();
            currentActiveSpell = "";

            if (UIManager.Instance != null)
                UIManager.Instance.UpdateSpellStatus("None");
        }

        isCasting = false; // Unlock spellcasting
        Debug.Log("Spellcasting unlocked.");
    }

    private void UpdateSpellBook()
    {
        spellAddressText.text = string.IsNullOrEmpty(inputSpellAddress) ? "" : inputSpellAddress;

        foreach (GameObject currentSpell in GameObject.FindGameObjectsWithTag("SpellUI"))
            Destroy(currentSpell);

        foreach (KeyValuePair<string, string> item in spellBook)
        {
            if (item.Key.StartsWith(inputSpellAddress))
            {
                SpellTextEntry newEntry = Instantiate(spellTextPrefab, spellBookPanel.transform, false);
                newEntry.gameObject.tag = "SpellUI";
                newEntry.SetText(item.Value, item.Key);
            }
        }
    }

    public void CastSpellNormal(string spellName)
    {
        bool found = false;
        foreach (var spell in spellBook)
        {
            if (spell.Value == spellName)
            {
                inputSpellAddress = spell.Key;
                Debug.Log($"Found {spell.Key}");
                found = true;
            }
        }
        if (found == false)
        {
            Debug.Log("Spell address could not be found fuck you");
            return;
        }

        // Disable base effect
        if (baseEffectObject != null)
            baseEffectObject.SetActive(false);

        // Apply buff/debuff
        float value = debuffBook[inputSpellAddress];

        Debug.Log(spellName + " cast!");
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateSpellStatus(spellName);
        Color spellColor = spellColors[inputSpellAddress];
        spellFloorImage.ShowSpell(spellName, spellColor);
        spellParticleColor.SetSpellColor(spellColor);
        if (spellAudio.TryGetValue(inputSpellAddress, out AudioClip clip) && clip != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(clip);
            else
                Debug.LogWarning("[Spellcasting] Missing AudioSource reference!");
        }
        if (wizardAudio.TryGetValue(inputSpellAddress, out AudioClip wizclip) && wizclip != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(wizclip);
            else
                Debug.LogWarning("[Spellcasting] Missing AudioSource reference!");
        }

        // Swap visuals
        if (spellVisuals.ContainsKey(inputSpellAddress) && parentObject != null)
        {
            SwapVisual(spellVisuals[inputSpellAddress], parentObject.transform, baseEffectObject);
            StartCoroutine(ResetVisualAfterDelay(spellDuration, baseEffectObject));
            // Remove this spell from the book after casting
            RemoveSpell(inputSpellAddress);
        }
        else
        {
            // Still reset even if no visual prefab
            StartCoroutine(ResetVisualAfterDelay(spellDuration, baseEffectObject));
        }
    }

    private void RemoveSpell(string address)
    {
        if (spellBook.ContainsKey(address))
        {
            string spellName = spellBook[address];

            // Remove from all linked dictionaries safely
            spellBook.Remove(address);
            debuffBook.Remove(address);
            spellVisuals.Remove(address);
            spellColors.Remove(address);
            spellAudio.Remove(address);
            wizardAudio.Remove(address);

            // boolBook is keyed by spell name instead of address
            boolBook.Remove(spellName);

            Debug.Log($"Removed spell '{spellName}' ({address}) from spell book.");
        }

        // Update UI after removal
        UpdateSpellBook();
    }

    public void AddSpell(string address, string name, float value, GameObject visualPrefab, bool onHitBool, Color FloorVisualColor, AudioClip spellCastAudio, AudioClip wizardSpellSound)
    {
        if (!spellBook.ContainsKey(address))
        {
            spellBook[address] = name;
            debuffBook[address] = value;

            if (visualPrefab != null)
                spellVisuals[address] = visualPrefab;

            boolBook[name] = onHitBool;
            spellColors[address] = FloorVisualColor;
            spellAudio[address] = spellCastAudio;
            wizardAudio[address] = wizardSpellSound;
        }
    }
}
