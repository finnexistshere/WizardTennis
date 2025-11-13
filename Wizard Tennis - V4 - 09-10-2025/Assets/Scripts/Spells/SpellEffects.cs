using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SpellEffects : MonoBehaviour
{
    // These reference the Player and the Opponent so it can call their attached components (Aiming, Spells, etc)
    // If this script is to account for two players, then it'll need to find the *casting* player and the *victim* player on a case-by-case basis

    // This can be done by making NetworkedSpellcasting assign it's own Player Object in it's spell cast routine as the caster, and the other player in the scene as the victim
    // It'll then pass these values to a network-specific version of THIS SCRIPT, which will use those values for the effects, after which it'll wipe the values ready to be used again in ResetSpellEffect()
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

    // In the network specific version we'll omit the slow down but keep the Explanation UI
    [Header("Spell Explanation UI")]
    public GameObject spellExplanationUI;
    public TMP_Text spellExplanationText;
    public float slowTimeScale = 0.25f;
    public float explanationDuration = 2.5f;

    [Header("Spell Object Settings")]
    public GameObject iceBlockPrefab;
    private GameObject activeIceBlock;
    public GameObject stoneWallPrefab;
    private GameObject activeStoneWall;

    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();
    private Coroutine explanationRoutine;
    private float lastOriginalTimeScale = 1f;

    public bool spellHit;

    public static bool isSpellSlowdownActive = false;

    private void Awake()
    {
        Player = GameObject.Find("Player");
        Opponent = GameObject.Find("Opponent");
        TennisAI = GameObject.Find("Game Manager").GetComponent<TennisAI>();
    }

    public void castSpell()
    {
        // Show explanation only once per round per spell
        if (!spellsUsedThisRound.Contains(spellName))
        {
            spellsUsedThisRound.Add(spellName);
            explanationRoutine = StartCoroutine(ShowSpellExplanation(spellName));
        }

        switch (spellName)
        {
            case "Lightning":
                Player.GetComponent<MainCharacterMovement>().speed = 17;
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Ice":
                Opponent.GetComponent<OppHitting>().speed = 0f;
                Player.GetComponent<Ball>().xPos = 0f;

                if (iceBlockPrefab != null)
                {
                    activeIceBlock = Instantiate(iceBlockPrefab, Opponent.transform.position, Opponent.transform.rotation);
                    activeIceBlock.transform.SetParent(Opponent.transform);
                    activeIceBlock.transform.localPosition = Vector3.zero;
                }

                Invoke(nameof(resetSpellEffect), 0.5f);
                break;

            case "Fireball":
                TennisAI.ApplyBuff(-0.2f, spellName);
                resetOnOppHit = true;
                StartCoroutine(FireballHitCheck());
                break;

            case "Shadow":
                if (!oppHitSpell)
                {
                    oppHitSpell = true;
                }
                else
                {
                    Player.GetComponent<Spellcasting>().CastSpellNormal(spellName);
                    OppHitting opp = Opponent.GetComponent<OppHitting>();
                    opp.xPos = Player.transform.position.x;
                    opp.zPos = Player.transform.position.z;
                    resetOnOppHit = true;
                }
                break;

            case "Green":
                Player.GetComponent<Ball>().green = true;
                Invoke(nameof(resetSpellEffect), 1f);
                break;

            case "Stone":
                if (stoneWallPrefab != null)
                {
                    Vector3 spawnPos = Player.transform.position + Player.transform.forward * 2f;
                    Quaternion spawnRot = Quaternion.identity;

                    activeStoneWall = Instantiate(stoneWallPrefab, spawnPos, spawnRot);
                    StartCoroutine(HandleStoneWall(activeStoneWall, 5f)); // 5 seconds duration
                    Invoke(nameof(resetSpellEffect), 5f);
                }
                else
                {
                    Debug.LogWarning("Stone Wall Prefab not assigned!");
                }
                break;
            case "Chronos":
                // Temporarily buff movement and gravity for slow-time effect
                Player.GetComponent<MainCharacterMovement>().speed = 70f;
                Player.GetComponent<MainCharacterMovement>().gravity = 250f;

                // Wait until explanation UI finishes before applying time slowdown
                StartCoroutine(ApplyChronosAfterExplanation());
                break;
        }
        if (!oppHitSpell)
            Player.GetComponent<Spellcasting>().CastSpellNormal(spellName);
    }

    public void resetSpellEffect()
    {
        switch (spellName)
        {
            case "Lightning":
                Player.GetComponent<MainCharacterMovement>().speed = 7;
                break;

            case "Ice":
                Opponent.GetComponent<OppHitting>().speed = 5;
                if (activeIceBlock != null)
                {
                    Destroy(activeIceBlock);
                    activeIceBlock = null;
                }
                break;

            case "Fireball":
            case "Shadow":
                resetOnOppHit = false;
                break;
            case "Chronos":
                Time.timeScale = 1f;
                Player.GetComponent<MainCharacterMovement>().speed = 7;
                Player.GetComponent<MainCharacterMovement>().gravity = 25f;
                break;
        }

        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
        spellHit = false;
    }

    // Coroutine
    private IEnumerator ApplyChronosAfterExplanation()
    {
        // Wait until the tutorial/explanation is done
        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);

        // Small extra buffer to ensure TimeScale resets first
        yield return new WaitForSecondsRealtime(0.05f);

        // Apply Chronos time slowdown cleanly
        Time.timeScale = 0.1f;

        Player.GetComponent<MainCharacterMovement>().speed = 70f;
        Player.GetComponent<MainCharacterMovement>().gravity = 250f;

        // Keep it active for a few seconds in real time
        yield return new WaitForSecondsRealtime(2.0f);

        // Restore normal time and player physics
        Time.timeScale = 1f;
        Player.GetComponent<MainCharacterMovement>().speed = 7f;
        Player.GetComponent<MainCharacterMovement>().gravity = 25f;

        // Fully reset spell state
        resetSpellEffect();
    }

    private IEnumerator HandleStoneWall(GameObject wall, float duration)
    {
        // Make it rise up from below ground
        Vector3 endPos = wall.transform.position;
        Vector3 startPos = endPos + Vector3.down * 2f;
        wall.transform.position = startPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f; // speed of rising
            wall.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        // Wait for the wall's duration
        yield return new WaitForSeconds(duration - 1f);

        // Fade out before destroy (if it has a renderer)
        Renderer rend = wall.GetComponent<Renderer>();
        if (rend != null && rend.material.HasProperty("_Color"))
        {
            Color startColor = rend.material.color;
            float fadeTime = 1f;
            float fade = 0f;

            while (fade < 1f)
            {
                fade += Time.deltaTime / fadeTime;
                Color c = startColor;
                c.a = Mathf.Lerp(1f, 0f, fade);
                rend.material.color = c;
                yield return null;
            }
        }

        Destroy(wall);
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

        if (audioSource == null || ouchVoicelines == null || ouchVoicelines.Length == 0)
            yield break;

        yield return new WaitForSeconds(0.1f);

        audioSource.pitch = 1f;
        int index = (ouchVoicelines.Length == 1) ? 0 : Random.Range(0, ouchVoicelines.Length);
        audioSource.PlayOneShot(ouchVoicelines[index]);
    }

    private IEnumerator ShowSpellExplanation(string spell)
    {
        if (spellExplanationUI == null) yield break;

        // Prevent overlapping UI or multiple slowdowns
        if (explanationRoutine != null)
            ForceResetSpellExplanation();

        isSpellSlowdownActive = true; //  Mark slowdown active

        if (spellExplanationText != null)
            spellExplanationText.text = GetSpellDescription(spell);

        spellExplanationUI.SetActive(true);

        lastOriginalTimeScale = Time.timeScale;
        Time.timeScale = slowTimeScale;

        yield return new WaitForSecondsRealtime(explanationDuration);

        ForceResetSpellExplanation();

        isSpellSlowdownActive = false; // Mark slowdown complete
    }


    public void ForceResetSpellExplanation()
    {
        // Called by CollisionTrackerBall to restore the timescale before doing it's pause to give a point, otherwise things get a little messy. - Ed
        Time.timeScale = lastOriginalTimeScale;
        if (spellExplanationUI != null)
            spellExplanationUI.SetActive(false);

        if (explanationRoutine != null)
        {
            StopCoroutine(explanationRoutine);
            explanationRoutine = null;
        }
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
            case "Stone": return "Stone: Rock solid Defense!";
            case "Chronos": return "Chronos: Time itself bends!";
            default: return "A mysterious spell...";
        }
    }

    public static void ResetSpellsForNewRound()
    {
        spellsUsedThisRound.Clear();
    }
}
