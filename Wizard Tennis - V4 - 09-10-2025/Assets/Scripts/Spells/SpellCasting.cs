using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class Spellcasting : MonoBehaviour
{
    // --- Spell Dictionaries ---
    public Dictionary<string, string> spellBook = new Dictionary<string, string>();        // address spell name
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();         // address value
    public Dictionary<string, GameObject> spellVisuals = new Dictionary<string, GameObject>(); // address prefab
    public Dictionary<string, bool> boolBook = new Dictionary<string, bool>(); // dictionary that reads a spell bool value (bool value will tell whether the spell is cast immediately or when the player hits the ball)

    // --- UI References ---
    [Header("UI References")]
    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public SpellTextEntry spellTextPrefab;

    // --- Ball Visuals ---
    [Header("Ball Visual")]
    public GameObject parentObject;     // object containing scripts and systems (do not replace)
    public GameObject baseEffectObject; // child visual to be swapped
    private GameObject currentVisualInstance;

    [Header("Spell Settings")]
    [Tooltip("Time in seconds before returning to the base ball and clearing effect")]
    public float spellDuration = 5f;

    [Tooltip("Time before spell input auto-clears if no further input is given")]
    public float inputTimeout = 2f;

    // --- Spellcasting State ---
    public string inputSpellAddress = "";
    private string currentActiveSpell = ""; // track which spell is currently active

    private float lastInputTime; // tracks when the last input happened

    public TennisAI TennisAi; // assign TennisAI in Inspector

    public SpellFloorImage spellFloorImage;

    public SpellEffects SpellEffects; // assign SpellEffects in Inspector

    private Color floorVisualColor;

    private void Awake()
    {

        spellBookPanel.SetActive(true);
        lastInputTime = -inputTimeout; // so it doesn't auto-clear at start
    }

    private void Update()
    {
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
        lastInputTime = Time.time; // reset timeout timer
        UpdateSpellBook();
    }

    private void CheckSpell()
    {
        if (spellBook.ContainsKey(inputSpellAddress))
        {
            string spellName = spellBook[inputSpellAddress];

            // Disable base effect while spell is active
            if (baseEffectObject != null)
                baseEffectObject.SetActive(false);

            // Apply new buff/debuff
            float value = debuffBook[inputSpellAddress];
            //TennisAi.ApplyBuff(value, spellName);

            SpellEffects.spellName = spellBook[inputSpellAddress];

            if (boolBook[spellName])
            {
                SpellEffects.plrHitSpell = true;
            } else
            {
                SpellEffects.plrHitSpell = false;
                SpellEffects.castSpell();
            }

                currentActiveSpell = spellName;
            Debug.Log(spellName + " cast!");
            spellFloorImage.ShowSpell(spellName, floorVisualColor);

            // Swap visual if prefab exists
            if (spellVisuals.ContainsKey(inputSpellAddress) && parentObject != null)
            {
                SwapVisual(spellVisuals[inputSpellAddress], parentObject.transform, baseEffectObject);
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

        if (!string.IsNullOrEmpty(currentActiveSpell))
        {
            TennisAi.ClearEffects();
            currentActiveSpell = "";
        }
    }

    private void UpdateSpellBook()
    {
        spellAddressText.text = string.IsNullOrEmpty(inputSpellAddress) ? "" : inputSpellAddress;

        // Clear old spell UI
        foreach (GameObject currentSpell in GameObject.FindGameObjectsWithTag("SpellUI"))
            Destroy(currentSpell);

        // Create entries for matching spells
        foreach (KeyValuePair<string, string> item in spellBook)
        {
            if (item.Key.StartsWith(inputSpellAddress))
            {
                SpellTextEntry newEntry = Instantiate(spellTextPrefab, spellBookPanel.transform, false);

                // Tag so it can be cleaned up easily
                newEntry.gameObject.tag = "SpellUI";

                // Set both texts (different fonts handled in prefab)
                newEntry.SetText(item.Value, item.Key);
            }
        }
    }

    public void AddSpell(string address, string name, float value, GameObject visualPrefab, bool onHitBool, Color FloorVisualColor)
    {
        if (!spellBook.ContainsKey(address))
        {
            spellBook[address] = name;
            debuffBook[address] = value;
            if (visualPrefab != null)
                spellVisuals[address] = visualPrefab;
            boolBook[name] = onHitBool;
            floorVisualColor = FloorVisualColor;
        }
    }
}
