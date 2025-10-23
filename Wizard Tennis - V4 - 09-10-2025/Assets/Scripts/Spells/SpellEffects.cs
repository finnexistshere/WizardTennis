using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffects : MonoBehaviour
{
    // All this script does is contain the functions to cast the spell effects
    // It's been made a separate script for the sake of keeping things a bit more organised so the spellcasting system isn't overflowing with code
    // And so that if you want to make a change to a spell, you don't have to search through the spellcasting script to try and find what you're looking for


    // Also, to start off with, I'm putting each spells code within the main castSpell function. But if this becomes too messy, we can change it to call on separate spell functions


    public GameObject Player;
    public GameObject Opponent;
    public TennisAI TennisAI;


    public string spellName;
    public bool resetOnOppHit;
    public bool resetOnPlrHit;
    public bool oppHitSpell;
    public bool plrHitSpell;


    private void Awake()
    {
        Player = GameObject.Find("Player");
        Opponent = GameObject.Find("Opponent");
        TennisAI = GameObject.Find("Game Manager").GetComponent<TennisAI>();
    }


    public void castSpell()
    {
        Player.GetComponent<Spellcasting>().CastSpellNormal(spellName);

        if (spellName == "Lightning")
        {
            Player.GetComponent<MainCharacterMovement>().speed = 17;
            Invoke("resetSpellEffect", 5f);
        }
        else if (spellName == "Ice")
        {
            Opponent.GetComponent<OppHitting>().speed = 0f;
            Invoke("resetSpellEffect", 0.5f);
        }
        else if (spellName == "Fireball")
        {
            TennisAI.ApplyBuff(-0.2f, spellName);
            resetOnOppHit = true;
        }
        else if (spellName == "Shadow")
        {
            Opponent.GetComponent<OppHitting>().reverse = true;
            resetOnOppHit = true;
        }
    }


    public void resetSpellEffect()
    {
        if (spellName == "Lightning")
        {
            Player.GetComponent<MainCharacterMovement>().speed = 10;
        }
        else if (spellName == "Ice")
        {
            Opponent.GetComponent<OppHitting>().speed = 5;
        }
        else if (spellName ==  "Fireball")
        {
            resetOnOppHit = false;
        }
        else if (spellName == "Shadow")
        {
            Opponent.GetComponent<OppHitting>().reverse = false;
            resetOnOppHit = false;
        }
        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
    }
}
