using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.InputSystem;

public class Spellcasting : MonoBehaviour
{
    public Dictionary<string, string> spellBook = new Dictionary<string, string>();
    // spellBook is currently here just for testing out spellcasting. This will be moved to a different script later on.
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();

    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public TextMeshProUGUI spellTextPrefab;

    private bool spellCasting = false;
    public string inputSpellAddress = "";

    public TennisAI TennisAi;

    void Update()
    {
        /*if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            spellCasting = true;
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            spellCasting = false;
            checkSpell();
        }*/

        if (spellCasting)
        {
            //see if you can use switch instead of ifs here later
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                inputSpellAddress += "L";
                updateSpellBook();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                inputSpellAddress += "R";
                updateSpellBook();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                inputSpellAddress += "U";
                updateSpellBook();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                inputSpellAddress += "D";
                updateSpellBook();
            }
        }
    }

    public void OnCastSpell(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            spellCasting = true;
            spellBookPanel.SetActive(true);
            updateSpellBook();
        }
        else if (context.canceled)
        {
            spellCasting = false;
            spellBookPanel.SetActive(false);
            checkSpell();
        }
    }

    private void checkSpell()
    {
        string currentSpell = "";

        if (spellBook.ContainsKey(inputSpellAddress))
        {
            currentSpell = spellBook[inputSpellAddress];
            TennisAi.ApplyBuff(debuffBook[currentSpell], currentSpell);
            Debug.Log(currentSpell + " cast!");
        }
        else
        {
            Debug.Log("Spell not found");
        }

        inputSpellAddress = "";
    }

    private void updateSpellBook()
    {
        if (inputSpellAddress == "")
        {
            spellAddressText.text = "Spell Address";
        } else
        {
            spellAddressText.text = inputSpellAddress;
        }

            GameObject[] currentSpells = GameObject.FindGameObjectsWithTag("SpellUI");
        foreach (GameObject currentSpell in currentSpells)
        {
            Destroy(currentSpell);
        }

        foreach (KeyValuePair<string, string> item in spellBook)
        {
            if (item.Key.StartsWith(inputSpellAddress)) {
                TextMeshProUGUI newSpell = Instantiate(spellTextPrefab);
                newSpell.text = item.Value + "\n" + item.Key;
                newSpell.transform.SetParent(spellBookPanel.transform, false);
            }
        }
    }
}