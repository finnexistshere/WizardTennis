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

    [Header("Audio Source")]
    public AudioSource audioSource;

    // --- Spellcasting State ---
    public string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false; // prevents new spells while one is active

    public TennisAI TennisAi;
    public SpellFloorImage spellFloorImage;
    public SpellEffects SpellEffects;

    private Color floorVisualColor;

    private void Awake()
    {
        spellBookPanel.SetActive(true);
        lastInputTime = -inputTimeout;
    }

    private void Update()
    {
        // Lock out all spell input while a spell is active
        if (isCasting)
            return;

        UpdateSpellBook();

        // Auto-clear if no input for `inputTimeout` seconds
        if (!string.IsNullOrEmpty(inputSpellAddress) && Time.time - lastInputTime >= inputTimeout)
        {
            Debug.Log("Input timeout reached, clearing input...");
            inputSpellAddress = "";
            UpdateSpellBook();
        }

        // --- Spellcasting inputs ---
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { RegisterInput("a"); }
        else if (Input.GetKeyDown(KeyCode.RightArrow)) { RegisterInput("A"); }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { RegisterInput("B"); }
        else if (Input.GetKeyDown(KeyCode.DownArrow)) { RegisterInput("b"); }

        // Check only if a valid spell has been fully entered
        if (!string.IsNullOrEmpty(inputSpellAddress) && spellBook.ContainsKey(inputSpellAddress))
        {
            CheckSpell();
        }
    }

    private void RegisterInput(string direction)
    {
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

            // Disable base effect
            if (baseEffectObject != null)
                baseEffectObject.SetActive(false);

            // Apply buff/debuff
            float value = debuffBook[inputSpellAddress];
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

            // Swap visuals
            if (spellVisuals.ContainsKey(inputSpellAddress) && parentObject != null)
            {
                SwapVisual(spellVisuals[inputSpellAddress], parentObject.transform, baseEffectObject);
                StartCoroutine(ResetVisualAfterDelay(spellDuration, baseEffectObject));
            }
            else
            {
                // Still reset even if no visual prefab
                StartCoroutine(ResetVisualAfterDelay(spellDuration, baseEffectObject));
            }
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

    public void AddSpell(string address, string name, float value, GameObject visualPrefab, bool onHitBool, Color FloorVisualColor, AudioClip spellCastAudio)
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
        }
    }
}
