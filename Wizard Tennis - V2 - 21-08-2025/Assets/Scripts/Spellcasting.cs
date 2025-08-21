using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Accessibility;

public class Spellcasting : MonoBehaviour
{

    public List<string> spellBook; // spellBook is currently here just for testing out spellcasting. This will be moved to a different script later on.

    private bool spellCasting = false;
    public string inputSpellAddress = "";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            spellCasting = true;
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            spellCasting = false;
            checkSpell();
        }

        if (spellCasting)
        {
            //see if you can use switch instead of ifs here later
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                inputSpellAddress += "L";
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                inputSpellAddress += "R";
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                inputSpellAddress += "U";
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow)) { inputSpellAddress += "D"; }
        }
    }

    private void checkSpell()
    {
        string currentSpell = "";
        bool spellFound = false;
        for (int i = 0; i < spellBook.Count; i++)
        {
            if (spellBook[i] == inputSpellAddress)
            {
                currentSpell = spellBook[i];
                spellFound = true;
                break;
            }
        }

        if (spellFound)
        {
            Debug.Log(currentSpell);
        } else { Debug.Log("Spell not found"); }

        inputSpellAddress = "";
    }
}
