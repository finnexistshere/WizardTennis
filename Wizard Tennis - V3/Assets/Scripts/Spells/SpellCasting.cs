using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Spellcasting : MonoBehaviour
{
    // --- Spell Dictionaries ---
    public Dictionary<string, string> spellBook = new Dictionary<string, string>();        // address spell name
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();         // address value
    public Dictionary<string, GameObject> spellVisuals = new Dictionary<string, GameObject>(); // address prefab

    // --- UI References ---
    [Header("UI References")]
    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public TextMeshProUGUI spellTextPrefab;

    // --- Ball Visuals ---
    [Header("Ball Visual")]
    public GameObject parentObject;     // object containing scripts and systems (do not replace)
    public GameObject baseEffectObject; // child visual to be swapped
    private GameObject currentVisualInstance;

    [Header("Spell Settings")]
    [Tooltip("Time in seconds before returning to the base ball and clearing effect")]
    public float spellDuration = 5f;

    // --- Spellcasting State ---
    private bool spellCasting = false;
    public string inputSpellAddress = "";
    private string currentActiveSpell = ""; // track which spell is currently active

    public TennisAI TennisAi; // assign TennisAI in Inspector

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift)) spellCasting = true;
        if (Input.GetKeyUp(KeyCode.LeftShift)) spellCasting = false;

        if (spellCasting)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { inputSpellAddress += "L"; UpdateSpellBook(); }
            else if (Input.GetKeyDown(KeyCode.RightArrow)) { inputSpellAddress += "R"; UpdateSpellBook(); }
            else if (Input.GetKeyDown(KeyCode.UpArrow)) { inputSpellAddress += "U"; UpdateSpellBook(); }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { inputSpellAddress += "D"; UpdateSpellBook(); }
        }
    }

    public void OnCastSpell(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            spellCasting = true;
            spellBookPanel.SetActive(true);
            UpdateSpellBook();
        }
        else if (context.canceled)
        {
            spellCasting = false;
            spellBookPanel.SetActive(false);
            CheckSpell();
        }
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
            TennisAi.ApplyBuff(value, spellName);
            currentActiveSpell = spellName;
            Debug.Log(spellName + " cast!");

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
    }

    /// <summary>
    /// Swap the current visual to a new spell visual
    /// </summary>
    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        // Destroy existing spell visual
        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        // Disable base effect
        if (baseEffect != null)
            baseEffect.SetActive(false);

        // Instantiate new visual as a child of the parent object
        currentVisualInstance = Instantiate(newPrefab, parentTransform);

        // Reset local transform so it aligns with parent
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Waits for delay, then resets visual to base effect and clears active spell
    /// </summary>
    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect)
    {
        yield return new WaitForSeconds(delay);

        // Destroy spell visual
        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
            currentVisualInstance = null;
        }

        // Re-enable base effect
        if (baseEffect != null)
            baseEffect.SetActive(true);

        // Clear active spell effect
        if (!string.IsNullOrEmpty(currentActiveSpell))
        {
            TennisAi.ClearEffects();
            currentActiveSpell = "";
        }
    }

    private void UpdateSpellBook()
    {
        spellAddressText.text = string.IsNullOrEmpty(inputSpellAddress) ? "Spell Address" : inputSpellAddress;

        foreach (GameObject currentSpell in GameObject.FindGameObjectsWithTag("SpellUI"))
            Destroy(currentSpell);

        foreach (KeyValuePair<string, string> item in spellBook)
        {
            if (item.Key.StartsWith(inputSpellAddress))
            {
                TextMeshProUGUI newSpell = Instantiate(spellTextPrefab);
                newSpell.text = item.Value + "\n" + item.Key;
                newSpell.transform.SetParent(spellBookPanel.transform, false);
            }
        }
    }

    // --- Add a spell (called by pickups) ---
    public void AddSpell(string address, string name, float value, GameObject visualPrefab)
    {
        if (!spellBook.ContainsKey(address))
        {
            spellBook[address] = name;
            debuffBook[address] = value;
            if (visualPrefab != null)
                spellVisuals[address] = visualPrefab;
        }
    }
}
