using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SpellEffects : MonoBehaviour
{
    private GameObject caster;
    private GameObject victim;
    private TennisAI tennisAI;

    [Header("Runtime Spell State")]
    public string spellName;
    public bool resetOnOppHit;
    public bool resetOnPlrHit;
    public bool oppHitSpell;
    public bool plrHitSpell;
    public bool spellHit;
    public static bool isSpellSlowdownActive = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] ouchVoicelines;

    [Header("Point SFX")]
    public AudioSource pointSource;
    public AudioClip pointWon;
    public AudioClip pointlost;
    [Range(0f, 1f)] public float pointSFXVolume = 1f;

    [Header("Spell Explanation UI")]
    public GameObject spellExplanationUI;
    public TMP_Text spellExplanationText;
    public float slowTimeScale = 0.25f;
    public float explanationDuration = 2.5f;

    [Header("Spell Object Prefabs")]
    public GameObject iceBlockPrefab;
    private GameObject activeIceBlock;
    public GameObject stoneWallPrefab;
    private GameObject activeStoneWall;

    [Header("Singleplayer References")]
    public GameObject singleplayerOpponent;

    [Header("Behavior")]
    public bool allowTimescale = true;

    // Internal bookkeeping
    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();
    private Coroutine explanationRoutine;
    private float lastOriginalTimeScale = 1f;
    private Coroutine fireballRoutine;

    // Sentinel: prevents multiple visual/audio triggers per spell
    private bool spellVisualPlayed = false;

    private void Log(string msg) => Debug.Log($"[SpellEffects] {msg}");

    // -----------------------
    // Public API
    // -----------------------
    public void CastSpellWithContext(string spell, GameObject caster, GameObject victim, TennisAI ai = null)
    {
        if (string.IsNullOrEmpty(spell))
        {
            Debug.LogWarning("[SpellEffects] Attempt to cast null/empty spell.");
            return;
        }

        if (caster == null)
        {
            Debug.LogWarning($"[SpellEffects] CastSpellWithContext called with null caster for '{spell}'. Aborting.");
            return;
        }

        SetContext(caster, victim, ai);
        spellName = spell;
        Log($"Casting '{spellName}' from '{caster.name}' -> '{(victim ? victim.name : "null")}'");
        castSpell();
        ClearContext();
    }

    public void SetContext(GameObject caster, GameObject victim, TennisAI ai = null)
    {
        this.caster = caster;

        // Use the passed victim first; fallback to inspector for singleplayer
        if (victim != null)
            this.victim = victim;
        else if (singleplayerOpponent != null)
            this.victim = singleplayerOpponent;

        this.tennisAI = ai;
    }

    public void ClearContext()
    {
        caster = null;
        victim = null;
        tennisAI = null;
    }

    // -----------------------
    // Core casting logic
    // -----------------------
    public void castSpell()
    {
        if (string.IsNullOrEmpty(spellName)) return;

        // Explanation UI: show once per round per spell
        if (!spellsUsedThisRound.Contains(spellName))
        {
            spellsUsedThisRound.Add(spellName);
            if (explanationRoutine != null) StopCoroutine(explanationRoutine);
            explanationRoutine = StartCoroutine(ShowSpellExplanation(spellName));
        }

        switch (spellName)
        {
            case "Lightning": ApplyLightning(); break;
            case "Ice": ApplyIce(); break;
            case "Fireball": ApplyFireball(); break;
            case "Shadow": ApplyShadow(); break;
            case "Green": ApplyGreen(); break;
            case "Stone": ApplyStone(); break;
            case "Chronos": ApplyChronos(); break;
            default: Log($"Unknown spell requested: {spellName}"); break;
        }

        // Only run visuals/audio once
        if (!oppHitSpell && !spellVisualPlayed)
        {
            spellVisualPlayed = true;
            InvokeCasterSpellVisualCall(spellName);
        }
    }

    // -----------------------
    // Per-spell handlers
    // -----------------------
    private void ApplyLightning()
    {
        var mm = TryGetComponentFromCaster<MainCharacterMovement>();
        if (mm != null)
        {
            mm.speed = 17;
            StartCoroutine(ResetAfterDelay(5f));
            Log("Applied Lightning to caster (speed boost).");
        }
    }

    private void ApplyIce()
    {
        var oppHitting = TryGetComponentFromVictim<OppHitting>();
        var ball = TryGetComponentFromCaster<Ball>();

        if (oppHitting != null) oppHitting.speed = 0f;
        if (ball != null) ball.xPos = 0f;

        if (iceBlockPrefab != null && victim != null)
        {
            activeIceBlock = Instantiate(iceBlockPrefab, victim.transform.position, victim.transform.rotation);
            activeIceBlock.transform.SetParent(victim.transform);
            activeIceBlock.transform.localPosition = Vector3.zero;
        }

        StartCoroutine(ResetAfterDelay(0.5f));
    }

    private void ApplyFireball()
    {
        if (tennisAI != null) tennisAI.ApplyBuff(-0.2f, spellName);

        resetOnOppHit = true;
        if (fireballRoutine != null) StopCoroutine(fireballRoutine);
        fireballRoutine = StartCoroutine(FireballHitCheck());
    }

    private void ApplyShadow()
    {
        if (!oppHitSpell)
        {
            oppHitSpell = true;
            Log("Shadow: awaiting opponent hit.");
        }
        else
        {
            InvokeCasterSpellVisualCall(spellName);
            var opp = TryGetComponentFromVictim<OppHitting>();
            if (opp != null && caster != null)
            {
                opp.xPos = caster.transform.position.x;
                opp.zPos = caster.transform.position.z;
            }
            resetOnOppHit = true;
        }
    }

    private void ApplyGreen()
    {
        var ball = TryGetComponentFromCaster<Ball>();
        if (ball != null) ball.green = true;
        StartCoroutine(ResetAfterDelay(1f));
    }

    private void ApplyStone()
    {
        if (stoneWallPrefab != null && caster != null)
        {
            Vector3 spawnPos = caster.transform.position + caster.transform.forward * 2f;
            Quaternion spawnRot = Quaternion.identity;
            activeStoneWall = Instantiate(stoneWallPrefab, spawnPos, spawnRot);
            StartCoroutine(HandleStoneWall(activeStoneWall, 5f));
        }
    }

    private void ApplyChronos()
    {
        var mm = TryGetComponentFromCaster<MainCharacterMovement>();
        if (mm != null) { mm.speed = 70f; mm.gravity = 250f; }

        StartCoroutine(ApplyChronosAfterExplanation());
    }

    // -----------------------
    // Reset
    // -----------------------
    public void resetSpellEffect()
    {
        if (fireballRoutine != null) { StopCoroutine(fireballRoutine); fireballRoutine = null; }
        ForceResetSpellExplanation();
        isSpellSlowdownActive = false;
        spellVisualPlayed = false;

        switch (spellName)
        {
            case "Lightning":
                var mm1 = TryGetComponentFromCaster<MainCharacterMovement>();
                if (mm1 != null) mm1.speed = 7f;
                break;

            case "Ice":
                var opp = TryGetComponentFromVictim<OppHitting>();
                if (opp != null) opp.speed = 5f;
                if (activeIceBlock != null) { Destroy(activeIceBlock); activeIceBlock = null; }
                break;

            case "Fireball":
            case "Shadow":
                resetOnOppHit = false;
                break;

            case "Chronos":
                if (allowTimescale) Time.timeScale = 1f;
                var mm2 = TryGetComponentFromCaster<MainCharacterMovement>();
                if (mm2 != null) { mm2.speed = 7f; mm2.gravity = 25f; }
                break;
        }

        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
        spellHit = false;

        Log("resetSpellEffect executed.");
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        resetSpellEffect();
    }

    // -----------------------
    // Coroutines & utility
    // -----------------------
    private IEnumerator FireballHitCheck()
    {
        yield return new WaitUntil(() => resetOnOppHit == false);

        if (audioSource == null || ouchVoicelines == null || ouchVoicelines.Length == 0) yield break;

        yield return new WaitForSeconds(0.1f);

        int index = (ouchVoicelines.Length == 1) ? 0 : Random.Range(0, ouchVoicelines.Length);
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(ouchVoicelines[index]);
    }

    private IEnumerator ShowSpellExplanation(string spell)
    {
        if (spellExplanationUI == null) yield break;

        isSpellSlowdownActive = true;

        if (spellExplanationText != null)
            spellExplanationText.text = GetSpellDescription(spell);

        spellExplanationUI.SetActive(true);
        lastOriginalTimeScale = Time.timeScale;

        if (allowTimescale) Time.timeScale = slowTimeScale;

        yield return new WaitForSecondsRealtime(explanationDuration);

        ForceResetSpellExplanation();
        isSpellSlowdownActive = false;
    }

    public void ForceResetSpellExplanation()
    {
        Time.timeScale = lastOriginalTimeScale;
        if (spellExplanationUI != null) spellExplanationUI.SetActive(false);

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

    private IEnumerator ApplyChronosAfterExplanation()
    {
        yield return new WaitUntil(() => !isSpellSlowdownActive);

        if (allowTimescale) Time.timeScale = 0.1f;
        var mm = TryGetComponentFromCaster<MainCharacterMovement>();
        if (mm != null) { mm.speed = 70f; mm.gravity = 250f; }

        yield return new WaitForSecondsRealtime(2f);

        if (allowTimescale) Time.timeScale = 1f;
        if (mm != null) { mm.speed = 7f; mm.gravity = 25f; }

        resetSpellEffect();
    }

    private IEnumerator HandleStoneWall(GameObject wall, float duration)
    {
        if (wall == null) yield break;

        Vector3 endPos = wall.transform.position;
        Vector3 startPos = endPos + Vector3.down * 2f;
        wall.transform.position = startPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            wall.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        yield return new WaitForSeconds(duration - 1f);

        Renderer rend = wall.GetComponent<Renderer>();
        if (rend != null && rend.material.HasProperty("_Color"))
        {
            Color startColor = rend.material.color;
            float fade = 0f;
            while (fade < 1f)
            {
                fade += Time.deltaTime / 1f;
                Color c = startColor; c.a = Mathf.Lerp(1f, 0f, fade);
                rend.material.color = c;
                yield return null;
            }
        }

        Destroy(wall);
    }

    // -----------------------
    // Helper
    // -----------------------
    private T TryGetComponentFromCaster<T>() where T : Component => caster ? caster.GetComponent<T>() : null;
    private T TryGetComponentFromVictim<T>() where T : Component => victim ? victim.GetComponent<T>() : null;

    private void InvokeCasterSpellVisualCall(string spell)
    {
        if (caster == null) { Log("Caster null, cannot call Spellcasting."); return; }

        Spellcasting sc = caster.GetComponent<Spellcasting>();
        if (sc != null) { sc.CastSpellNormal(spell); Log($"Called Spellcasting.CastSpellNormal('{spell}')"); return; }

        NetworkedSpellcasting nsc = caster.GetComponent<NetworkedSpellcasting>();
        if (nsc != null) { nsc.CastSpellNormal(spell); Log($"Called NetworkedSpellcasting.CastSpellNormal('{spell}')"); return; }

        Log("No Spellcasting component found, skipping visual call.");
    }

    public static void ResetSpellsForNewRound() => spellsUsedThisRound.Clear();

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

}
