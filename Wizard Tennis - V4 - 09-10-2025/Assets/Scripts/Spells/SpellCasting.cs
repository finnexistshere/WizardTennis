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
    private GameObject currentBall;
    private float ballCheckInterval = 0.5f;
    private float nextBallCheckTime = 0f;

    private void Update()
    {
        // Regular spell logic
        if (Time.time >= nextBallCheckTime)
        {
            nextBallCheckTime = Time.time + ballCheckInterval;
            TryFindAndLinkBall();
        }

        if (isCasting)
            return;

        UpdateSpellBook();

        if (!string.IsNullOrEmpty(inputSpellAddress) && Time.time - lastInputTime >= inputTimeout)
        {
            inputSpellAddress = "";
            UpdateSpellBook();
        }

        if (CheckSpellInput(out string direction))
            RegisterInput(direction);

        if (!string.IsNullOrEmpty(inputSpellAddress) && spellBook.ContainsKey(inputSpellAddress))
            CheckSpell();
    }

    private void TryFindAndLinkBall()
    {
        // Skip if already linked and valid
        if (currentBall != null)
        {
            if (currentBall.activeInHierarchy)
                return;
            else
                currentBall = null;
        }

        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (ballObj == null)
        {
            Debug.Log("[Spellcasting] No Ball found with tag 'Ball'.");
            return;
        }

        currentBall = ballObj;

        // --- Assign visuals ---
        parentObject = ballObj;

        // Find the visual child named "sm_Ball"
        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
        {
            baseEffectObject = visualChild.gameObject;
            Debug.Log("[Spellcasting] Linked Ball visuals: " + baseEffectObject.name);
        }
        else
        {
            Debug.LogWarning("[Spellcasting] Could not find child named 'sm_Ball' under Ball prefab.");
        }

        Debug.Log("[Spellcasting] Connected to active Ball GameObject.");
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

            audioSource.PlayOneShot(spellRegisterSound); // Feedback

            SpellEffects.spellName = spellName;

            if (boolBook[spellName])
            {
                SpellEffects.plrHitSpell = true;
            }
            else
            {
                SpellEffects.plrHitSpell = false;
                SpellEffects.castSpell();
            }

            // Start visual/audio reset coroutine and pass the spell address for deferred removal
            StartCoroutine(ResetVisualAfterDelay(spellDuration, baseEffectObject, inputSpellAddress));
        }
        else
        {
            Debug.Log("Spell not found");
        }

        inputSpellAddress = "";
        UpdateSpellBook();
    }

    public void CastSpellNormal(string spellName)
    {
        bool found = false;
        string spellAddress = "";

        foreach (var spell in spellBook)
        {
            if (spell.Value == spellName)
            {
                spellAddress = spell.Key;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"Spell '{spellName}' not found in spell book.");
            return;
        }

        // Disable base effect
        if (baseEffectObject != null)
            baseEffectObject.SetActive(false);

        // Apply buff/debuff
        float value = debuffBook[spellAddress];

        Debug.Log($"{spellName} cast!");
        if (UIManager.Instance != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white;
            UIManager.Instance.UpdateSpellStatus(spellName, uiSpellColor);
        }

        Color spellColor = spellColors[spellAddress];
        spellFloorImage.ShowSpell(spellName, spellColor);
        spellParticleColor.SetSpellColor(spellColor);

        if (wizardAudio.TryGetValue(spellAddress, out AudioClip wizclip) && wizclip != null)
        {
            AudioClip spellClip = null;
            spellAudio.TryGetValue(spellAddress, out spellClip);
            StartCoroutine(PlaySpellSequence(wizclip, spellClip, 0.35f)); // waits 1 second
        }
        else if (spellAudio.TryGetValue(spellAddress, out AudioClip spellClipOnly) && spellClipOnly != null)
        {
            // If there’s no wizard clip, play the spell sound immediately
            audioSource?.PlayOneShot(spellClipOnly);
        }


        // Swap visuals
        if (spellVisuals.ContainsKey(spellAddress) && parentObject != null)
            SwapVisual(spellVisuals[spellAddress], parentObject.transform, baseEffectObject);
        SpellEffects.spellHit = true;
        // Start coroutine with deferred removal
        StartCoroutine(ResetVisualAfterDelay(spellDuration, baseEffectObject, spellAddress));
    }

    // --- Updated ResetVisualAfterDelay ---
    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect, string spellAddress)
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
                UIManager.Instance.UpdateSpellStatus("None", Color.white);
        }

        // Remove spell AFTER all effects
        RemoveSpell(spellAddress);

        isCasting = false;
        Debug.Log("Spellcasting unlocked.");
    }


    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        baseEffect.SetActive(false);

        currentVisualInstance = Instantiate(newPrefab, parentTransform);
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;
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

    private IEnumerator PlaySpellSequence(AudioClip wizardClip, AudioClip spellClip, float delay)
    {
        // Step 1: play the wizard voice line
        if (wizardClip != null)
            audioSource.PlayOneShot(wizardClip);

        // Step 2: wait for the delay (e.g. 1 second)
        yield return new WaitForSeconds(delay);

        // Step 3: play the spell sound
        if (spellClip != null)
            audioSource.PlayOneShot(spellClip);
    }

}
