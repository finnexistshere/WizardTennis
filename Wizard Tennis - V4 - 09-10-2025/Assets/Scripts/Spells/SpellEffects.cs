using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpellEffects : MonoBehaviour
{
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

    [Header("Spell Explanation UI")]
    public GameObject spellExplanationUI; // UI panel or text to display
    public TMP_Text spellExplanationText; // Optional - text component to show info
    public float slowTimeScale = 0.25f;
    public float explanationDuration = 2.5f;

    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();

    public bool spellHit;

    private void Awake()
    {
        Player = GameObject.Find("Player");
        Opponent = GameObject.Find("Opponent");
        TennisAI = GameObject.Find("Game Manager").GetComponent<TennisAI>();
    }

    public void castSpell()
    {
        // Check if first time this round
        if (!spellsUsedThisRound.Contains(spellName))
        {
            spellsUsedThisRound.Add(spellName);
            StartCoroutine(ShowSpellExplanation(spellName));
        }

        if (spellName == "Lightning")
        {
            Player.GetComponent<MainCharacterMovement>().speed = 17;
            Invoke(nameof(resetSpellEffect), 5f);
        }
        else if (spellName == "Ice")
        {
            Opponent.GetComponent<OppHitting>().speed = 0f;
            Player.GetComponent<Ball>().xPos = 0f;
            Invoke(nameof(resetSpellEffect), 0.5f);
        }
        else if (spellName == "Fireball")
        {
            TennisAI.ApplyBuff(-0.2f, spellName);
            resetOnOppHit = true;
            StartCoroutine(FireballHitCheck());
        }
        else if (spellName == "Shadow")
        {
            if (!oppHitSpell)
            {
                oppHitSpell = true;
            }
            else
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
            Invoke(nameof(resetSpellEffect), 1f);
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
        else if (spellName == "Fireball" || spellName == "Shadow")
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
            pointSource.PlayOneShot(pointWon, pointSFXVolume);
    }

    public void OnPointLost()
    {
        if (pointSource != null && pointlost != null)
            pointSource.PlayOneShot(pointlost, pointSFXVolume);
    }

    private IEnumerator FireballHitCheck()
    {
        yield return new WaitUntil(() => resetOnOppHit == false);

        if (audioSource == null) yield break;
        if (ouchVoicelines == null || ouchVoicelines.Length == 0) yield break;

        yield return new WaitForSeconds(0.1f);

        audioSource.pitch = 1f;
        int index = (ouchVoicelines.Length == 1) ? 0 : Random.Range(0, ouchVoicelines.Length);
        audioSource.PlayOneShot(ouchVoicelines[index]);
    }

    private IEnumerator ShowSpellExplanation(string spell)
    {
        if (spellExplanationUI == null) yield break;

        // Set UI text
        if (spellExplanationText != null)
            spellExplanationText.text = GetSpellDescription(spell);

        // Activate UI
        spellExplanationUI.SetActive(true);

        // Slow down time
        float originalTimeScale = Time.timeScale;
        Time.timeScale = slowTimeScale;

        yield return new WaitForSecondsRealtime(explanationDuration);

        // Restore time and hide UI
        Time.timeScale = originalTimeScale;
        spellExplanationUI.SetActive(false);
    }

    private string GetSpellDescription(string spell)
    {
        switch (spell)
        {
            case "Lightning": return "Lightning: Speed Incarnate!";
            case "Ice": return "Ice: Freeze your foe!";
            case "Fireball": return "Fireball: Fiery attack!";
            case "Shadow": return "Shadow: Return your hit!";
            case "Green": return "Green: Mysteriously green!";
            default: return "A mysterious spell...";
        }
    }

    public static void ResetSpellsForNewRound()
    {
        spellsUsedThisRound.Clear();
    }
}
