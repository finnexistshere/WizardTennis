using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.InputSystem;

public class Spellcasting : MonoBehaviour
{
    public Dictionary<string, string> spellBook = new Dictionary<string, string>();
    // spellBook is currently here just for testing out spellcasting. This will be moved to a different script later on.
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();

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

    public void OnCastSpell(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            spellCasting = true;
        }
        else if (context.canceled)
        {
            spellCasting = false;
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
            UIManager.Instance?.UpdateSpellStatus(currentSpell, debuffBook[currentSpell]); // Clear UI display
        }
        else
        {
            Debug.Log("Spell not found");
        }

        inputSpellAddress = "";
    }
}