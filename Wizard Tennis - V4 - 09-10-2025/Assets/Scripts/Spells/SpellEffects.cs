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

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] ouchVoicelines;

    [Header("Point SFX")]
    public AudioSource pointSource;
    public AudioClip pointWon;
    public AudioClip pointlost;
    [Range(0f, 1f)] public float pointSFXVolume = 1f;


    public bool spellHit;


    private void Awake()
    {
        Player = GameObject.Find("Player");
        Opponent = GameObject.Find("Opponent");
        TennisAI = GameObject.Find("Game Manager").GetComponent<TennisAI>();
    }


    public void castSpell()
    {
        if (spellName == "Lightning")
        {
            Player.GetComponent<MainCharacterMovement>().speed = 17;
            Invoke("resetSpellEffect", 5f);
        }
        else if (spellName == "Ice")
        {
            Opponent.GetComponent<OppHitting>().speed = 0f;
            Player.GetComponent<Ball>().xPos = 0f;
            Invoke("resetSpellEffect", 0.5f);
        }
        else if (spellName == "Fireball")
        {
            TennisAI.ApplyBuff(-0.2f, spellName);
            resetOnOppHit = true;

            // Start a coroutine to monitor for when the Fireball hits the opponent
            StartCoroutine(FireballHitCheck());
        }
        else if (spellName == "Shadow")
        {
            if (!oppHitSpell)
            {
                oppHitSpell = true;
            } else
            {
                Player.GetComponent<Spellcasting>().CastSpellNormal(spellName);
                OppHitting OppHitting = Opponent.GetComponent<OppHitting>();
                OppHitting.xPos = Player.transform.position.x;
                OppHitting.zPos = Player.transform.position.z;
                resetOnOppHit = true;
            }
        }
        else if (spellName == "Green")
        {
            Player.GetComponent<Ball>().green = true;
            Invoke("resetSpellEffect", 1f);
        }

        if (!oppHitSpell)
        {
            Player.GetComponent<Spellcasting>().CastSpellNormal(spellName);
        }
    }


    public void resetSpellEffect()
    {
        if (spellName == "Lightning")
        {
            Player.GetComponent<MainCharacterMovement>().speed = 7;
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
            resetOnOppHit = false;
        }
        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
        spellHit = false;
    }

    public void OnPointWon()
    {
        if (pointSource != null && pointWon != null)
        {
            pointSource.PlayOneShot(pointWon, pointSFXVolume);
        }
    }

    public void OnPointLost()
    {
        if (pointSource != null && pointlost != null)
        {
            pointSource.PlayOneShot(pointlost, pointSFXVolume);
        }
    }
    
    private IEnumerator FireballHitCheck()
    {
        // Wait until the opponent is hit and the fireball effect resets
        yield return new WaitUntil(() => resetOnOppHit == false);

        if (audioSource == null) yield break;
        if (ouchVoicelines == null || ouchVoicelines.Length == 0) yield break;

        // Optional: slight delay for impact feel
        yield return new WaitForSeconds(0.1f);

        // Ensure this source isn’t inheriting a pitch change from elsewhere
        audioSource.pitch = 1f;

        int index = (ouchVoicelines.Length == 1) ? 0 : Random.Range(0, ouchVoicelines.Length);
        audioSource.PlayOneShot(ouchVoicelines[index]);
    }
}
