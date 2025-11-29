using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SpellEffects : MonoBehaviour
{
    // Legacy references for single-player (kept for backwards compatibility)
    public GameObject Player;
    public GameObject Opponent;
    public TennisAI TennisAI;

    // Context references (used by networked mode)
    [Header("Context References")]
    private GameObject currentPlayer;
    private GameObject currentOpponent;
    private TennisAI currentAI;

    public string spellName;
    public bool resetOnOppHit;
    public bool resetOnPlrHit;
    public bool oppHitSpell;
    public bool plrHitSpell;
    public bool resetOnBounce;

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

    [Header("Spell Object Settings")]
    public GameObject iceBlockPrefab;
    private GameObject activeIceBlock;
    public GameObject stoneWallPrefab;
    private GameObject activeStoneWall;
    public GameObject geminiPrefab;
    private GameObject activeGemini;
    public GameObject mudPrefab;
    private GameObject activeMud;
    public GameObject orbiterPrefab;
    private GameObject activeOrbiter;
    public GameObject tetherPrefab;
    private GameObject activeTether;
    public GameObject jollyPrefab;
    private GameObject activeJolly;
    private GameObject Gorbino;
    public GameObject BallPrefab;
    private GameObject activeBall;
    public GameObject flamePrefab;
    private GameObject activeFlame;

    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();
    private Coroutine explanationRoutine;
    private float lastOriginalTimeScale = 1f;

    public bool spellHit;
    public static bool isSpellSlowdownActive = false;

    private string[] allSpells = { "Lightning", "Ice", "Fireball", "Shadow", "Green", "Stone", "Chronos", "Gemini", "Blink", "Jolly", "Mud", "Warp", "Pisces", "Tether" };

    private void Awake()
    {
        // Try to find default references (single-player)
        if (Player == null)
            Player = GameObject.Find("Player");
        if (Opponent == null)
            Opponent = GameObject.Find("Opponent");
        if (TennisAI == null)
        {
            var gm = GameObject.Find("Game Manager");
            if (gm != null)
                TennisAI = gm.GetComponent<TennisAI>();
        }
    }

    /// <summary>
    /// Sets the context for spell effects - which player is casting, who the opponent is, and AI reference
    /// </summary>
    public void SetContext(GameObject player, GameObject opponent, TennisAI ai)
    {
        currentPlayer = player;
        currentOpponent = opponent;
        currentAI = ai;

        Debug.Log($"[SpellEffects] Context set - Player: {player?.name}, Opponent: {opponent?.name}, AI: {ai != null}");
    }

    /// <summary>
    /// Clears the stored context (call when resetting or ending spells)
    /// </summary>
    public void ClearContext()
    {
        currentPlayer = null;
        currentOpponent = null;
        currentAI = null;
    }

    /// <summary>
    /// Gets the active player (context if set, otherwise default)
    /// </summary>
    private GameObject GetPlayer()
    {
        return currentPlayer != null ? currentPlayer : Player;
    }

    /// <summary>
    /// Gets the active opponent (context if set, otherwise default)
    /// </summary>
    private GameObject GetOpponent()
    {
        return currentOpponent != null ? currentOpponent : Opponent;
    }

    /// <summary>
    /// Gets the active AI (context if set, otherwise default)
    /// </summary>
    private TennisAI GetAI()
    {
        return currentAI != null ? currentAI : TennisAI;
    }

    public void castSpell()
    {
        GameObject player = GetPlayer();
        GameObject opponent = GetOpponent();
        TennisAI ai = GetAI();

        if (player == null)
        {
            Debug.LogError("[SpellEffects] No player context! Cannot cast spell.");
            return;
        }

        if (OptionsManager.Instance != null)
        {
            // Show explanation only once per round per spell
            if (!spellsUsedThisRound.Contains(spellName) && OptionsManager.Instance.spellTips)
            {
                spellsUsedThisRound.Add(spellName);
                explanationRoutine = StartCoroutine(ShowSpellExplanation(spellName));
            }
        }

        switch (spellName)
        {
            case "Lightning":
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null) mcm.speed = 17;
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Ice":
                if (opponent != null)
                {
                    var oppHit = opponent.GetComponent<OppHitting>();
                    if (oppHit != null) oppHit.speed = 0f;

                    var ballComp = player.GetComponent<Ball>();
                    if (ballComp != null) ballComp.xPos = 0f;

                    if (iceBlockPrefab != null)
                    {
                        activeIceBlock = Instantiate(iceBlockPrefab, opponent.transform.position, opponent.transform.rotation);
                        activeIceBlock.transform.SetParent(opponent.transform);
                        activeIceBlock.transform.localPosition = Vector3.zero;
                    }
                }
                Invoke(nameof(resetSpellEffect), 0.5f);
                break;

            case "Fireball":
                if (ai != null)
                {
                    ai.ApplyBuff(-0.2f, spellName);
                }
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
                    // Cast spell visually
                    var spellcasting = player.GetComponent<Spellcasting>();
                    var netSpellcasting = player.GetComponent<NetworkedSpellcasting>();

                    if (spellcasting != null)
                        spellcasting.CastSpellNormal(spellName);
                    else if (netSpellcasting != null)
                        netSpellcasting.CastSpellNormal(spellName);

                    if (opponent != null)
                    {
                        var opp = opponent.GetComponent<OppHitting>();
                        if (opp != null)
                        {
                            opp.xPos = player.transform.position.x;
                            opp.zPos = player.transform.position.z;
                        }
                    }
                    resetOnOppHit = true;
                }
                break;

            case "Green":
                var greenBall = player.GetComponent<Ball>();
                if (greenBall != null) greenBall.green = true;
                Invoke(nameof(resetSpellEffect), 1f);
                break;

            case "Stone":
                if (stoneWallPrefab != null)
                {
                    Vector3 spawnPos = player.transform.position + player.transform.forward * 2f;
                    Quaternion spawnRot = Quaternion.identity;

                    activeStoneWall = Instantiate(stoneWallPrefab, spawnPos, spawnRot);

                    var returner = activeStoneWall.GetComponent<SimpleBallReturner>();
                    if (returner != null)
                    {
                        var ballComp = player.GetComponent<Ball>();
                        if (ballComp != null && ballComp.aimTarget != null)
                            returner.aimTarget = ballComp.aimTarget.transform;
                        if (opponent != null)
                            returner.opponent = opponent.transform;
                    }

                    StartCoroutine(HandleStoneWall(activeStoneWall, 5f));
                    Invoke(nameof(resetSpellEffect), 5f);
                }
                break;

            case "Chronos":
                StartCoroutine(ApplyChronosAfterExplanation());
                break;

            case "Gemini":
                Vector3 gemPos = player.transform.position;
                gemPos.x = -player.transform.position.x;
                Quaternion gemRot = player.transform.rotation;

                activeGemini = Instantiate(geminiPrefab, gemPos, gemRot);

                var gemReturner = activeGemini.GetComponent<SimpleBallReturner>();
                if (gemReturner != null)
                {
                    var ballComp = player.GetComponent<Ball>();
                    if (ballComp != null && ballComp.aimTarget != null)
                        gemReturner.aimTarget = ballComp.aimTarget.transform;
                    if (opponent != null)
                        gemReturner.opponent = opponent.transform;
                }

                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Blink":
                Vector3 input = Vector3.zero;

                KeyCode forward = KeyCode.W;
                KeyCode backward = KeyCode.S;
                KeyCode left = KeyCode.A;
                KeyCode right = KeyCode.D;

                if (OptionsManager.Instance != null && OptionsManager.Instance.leftHandedMode)
                {
                    forward = KeyCode.UpArrow;
                    backward = KeyCode.DownArrow;
                    left = KeyCode.LeftArrow;
                    right = KeyCode.RightArrow;
                }

                if (Input.GetKey(forward)) input.z += 3;
                if (Input.GetKey(backward)) input.z -= 3;
                if (Input.GetKey(right)) input.x += 3;
                if (Input.GetKey(left)) input.x -= 3;

                Vector3 moveDirection = new Vector3(input.x, 0, input.z);
                Vector3 testPos = player.transform.position - moveDirection;

                if (player.transform.position.z > 0)
                {
                    if (testPos.z > 10.9) testPos.z = 10.9f;
                    if (testPos.z < 0.5) testPos.z = 0.5f;
                }
                else
                {
                    if (testPos.z > -1) testPos.z = -1;
                    if (testPos.z < -11.5) testPos.z = -11.5f;
                }

                if (testPos.x > 4.9) testPos.x = 4.9f;
                if (testPos.x < -4.9) testPos.x = -4.9f;

                var movement = player.GetComponent<MainCharacterMovement>();
                var controller = player.GetComponent<CharacterController>();

                if (movement != null) movement.enabled = false;
                if (controller != null) controller.enabled = false;

                player.transform.position = testPos;

                if (movement != null) movement.enabled = true;
                if (controller != null) controller.enabled = true;

                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Jolly":
                var capsule = player.GetComponent<CapsuleCollider>();
                if (capsule != null) capsule.radius = 2;

                Quaternion jollyRot = Quaternion.identity * Quaternion.Euler(0, -90, 90);
                Transform racketTransform = player.transform.GetChild(2)?.GetChild(1)?.GetChild(0)?.GetChild(0)?.GetChild(1)?.GetChild(0)?.GetChild(0);

                if (racketTransform != null)
                {
                    activeJolly = Instantiate(jollyPrefab, racketTransform.position, Quaternion.identity);
                    activeJolly.transform.SetParent(racketTransform);
                    activeJolly.transform.localPosition = new Vector3(0, 0.05f, 0);
                    activeJolly.transform.localRotation = jollyRot;
                    activeJolly.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                    // Hide racket mesh
                    Transform racketMesh = player.transform.GetChild(2)?.GetChild(0)?.GetChild(4)?.GetChild(0)?.GetChild(0);
                    if (racketMesh != null)
                        racketMesh.gameObject.SetActive(false);
                }

                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Mud":
                resetOnOppHit = true;
                resetOnBounce = true;
                break;

            case "Warp":
                Invoke(nameof(resetSpellEffect), 0.5f);
                break;

            case "Pisces":
                Quaternion orbitRot = player.transform.rotation;
                activeOrbiter = Instantiate(orbiterPrefab, player.transform.position, orbitRot);
                activeOrbiter.transform.SetParent(player.transform);
                activeOrbiter.transform.localPosition = Vector3.zero;

                SimpleBallReturner[] children = activeOrbiter.GetComponentsInChildren<SimpleBallReturner>();
                foreach (SimpleBallReturner child in children)
                {
                    var ballComp = player.GetComponent<Ball>();
                    if (ballComp != null && ballComp.aimTarget != null)
                        child.aimTarget = ballComp.aimTarget.transform;
                    if (opponent != null)
                        child.opponent = opponent.transform;
                }

                StartCoroutine(HandleOrbiter(activeOrbiter, 5f));
                break;

            case "Tether":
                if (tetherPrefab != null && opponent != null)
                {
                    Vector3 tetherPos = opponent.transform.position;
                    tetherPos.y = 1.45f;
                    Quaternion tetherRot = Quaternion.identity;

                    activeTether = Instantiate(tetherPrefab, tetherPos, tetherRot);
                    var tether = activeTether.GetComponent<Tether>();
                    if (tether != null)
                        tether.Player = opponent.transform;

                    var oppHit = opponent.GetComponent<OppHitting>();
                    if (oppHit != null)
                        oppHit.tether = activeTether;

                    StartCoroutine(HandleStoneWall(activeTether, 5f));
                    Invoke(nameof(resetSpellEffect), 5f);
                }
                break;

            case "Gorbino":
                Gorbino = GameObject.Find("Gorbino");
                if (Gorbino != null)
                {
                    activeBall = Instantiate(BallPrefab, Gorbino.transform.position, Quaternion.identity);
                    Gorbino.SetActive(false);
                    Invoke(nameof(resetSpellEffect), 5f);
                }
                break;

            case "Gambit":
                int spellInt = Random.Range(0, allSpells.Length);
                spellName = allSpells[spellInt];
                castSpell();
                return;
        }

        // Cast spell visually if not an on-hit spell
        if (!oppHitSpell)
        {
            var spellcasting = player.GetComponent<Spellcasting>();
            var netSpellcasting = player.GetComponent<NetworkedSpellcasting>();

            if (spellcasting != null)
                spellcasting.CastSpellNormal(spellName);
            else if (netSpellcasting != null)
                netSpellcasting.CastSpellNormal(spellName);
        }
    }

    public void resetSpellEffect()
    {
        GameObject player = GetPlayer();
        GameObject opponent = GetOpponent();

        if (player == null) return;

        switch (spellName)
        {
            case "Lightning":
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null) mcm.speed = 7;
                break;

            case "Ice":
                if (opponent != null)
                {
                    var oppHit = opponent.GetComponent<OppHitting>();
                    if (oppHit != null) oppHit.speed = 5;
                }
                if (activeIceBlock != null)
                {
                    Destroy(activeIceBlock);
                    activeIceBlock = null;
                }
                break;

            case "Fireball":
                if (flamePrefab != null && opponent != null)
                {
                    activeFlame = Instantiate(flamePrefab, opponent.transform.position, Quaternion.identity);
                    activeFlame.transform.SetParent(opponent.transform);
                    activeFlame.transform.localPosition = new Vector3(0, 1.65f, 0);
                    StartCoroutine(HandleMud(activeFlame, 3f));
                }
                break;

            case "Shadow":
                resetOnOppHit = false;
                break;

            case "Chronos":
                Time.timeScale = 1f;
                var chronosMcm = player.GetComponent<MainCharacterMovement>();
                if (chronosMcm != null)
                {
                    chronosMcm.speed = 7;
                    chronosMcm.gravity = 25f;
                }
                break;

            case "Gemini":
                if (activeGemini != null)
                    Destroy(activeGemini);
                break;

            case "Jolly":
                var capsule = player.GetComponent<CapsuleCollider>();
                if (capsule != null) capsule.radius = 1;

                Transform racketMesh = player.transform.GetChild(2)?.GetChild(0)?.GetChild(4)?.GetChild(0)?.GetChild(0);
                if (racketMesh != null)
                    racketMesh.gameObject.SetActive(true);

                if (activeJolly != null)
                    Destroy(activeJolly);
                break;

            case "Mud":
                resetOnOppHit = false;
                resetOnBounce = false;

                if (mudPrefab != null)
                {
                    GameObject Ball = GameObject.FindWithTag("Ball");
                    if (Ball != null)
                    {
                        Vector3 spawnPos = Ball.transform.position;
                        spawnPos.y = 0.94f;
                        Quaternion spawnRot = Quaternion.identity;

                        activeMud = Instantiate(mudPrefab, spawnPos, spawnRot);
                        StartCoroutine(HandleMud(activeMud, 5f));
                    }
                }
                break;

            case "Warp":
                float min, max;
                var ballComp = player.GetComponent<Ball>();

                if (ballComp != null && ballComp.aimTarget != null && ballComp.aimTarget.position.x > 0)
                {
                    min = -4.5f;
                    max = 0f;
                }
                else
                {
                    min = 0f;
                    max = 4.5f;
                }

                float ballX = Random.Range(min, max);
                GameObject Ball1 = GameObject.FindWithTag("Ball");

                if (Ball1 != null)
                {
                    Vector3 ballPos = Ball1.transform.position;
                    ballPos.x = ballX;
                    Ball1.transform.position = ballPos;
                }
                break;

            case "Pisces":
                if (activeOrbiter != null)
                    Destroy(activeOrbiter);
                break;

            case "Tether":
                if (opponent != null)
                {
                    var oppHit = opponent.GetComponent<OppHitting>();
                    if (oppHit != null)
                        oppHit.tether = null;
                }
                break;

            case "Gorbino":
                if (activeBall != null)
                    Destroy(activeBall);
                if (Gorbino != null)
                    Gorbino.SetActive(true);
                break;
        }

        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
        spellHit = false;
    }

    private IEnumerator ApplyChronosAfterExplanation()
    {
        GameObject player = GetPlayer();
        if (player == null) yield break;

        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);
        yield return new WaitForSecondsRealtime(0.05f);

        Time.timeScale = 0.1f;

        var mcm = player.GetComponent<MainCharacterMovement>();
        if (mcm != null)
        {
            mcm.speed = 70f;
            mcm.gravity = 250f;
        }

        yield return new WaitForSecondsRealtime(2.0f);

        Time.timeScale = 1f;
        if (mcm != null)
        {
            mcm.speed = 7f;
            mcm.gravity = 25f;
        }

        resetSpellEffect();
    }

    private IEnumerator HandleStoneWall(GameObject wall, float duration)
    {
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

    private IEnumerator HandleMud(GameObject mud, float duration)
    {
        Vector3 endScale = mud.transform.localScale;
        Vector3 startScale = new Vector3(0.1f, 0.01f, 0.1f);
        mud.transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            mud.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        yield return new WaitForSeconds(duration - 1f);
        Destroy(mud);
    }

    private IEnumerator HandleOrbiter(GameObject orbiter, float duration)
    {
        Vector3 endScale = orbiter.transform.localScale;
        Vector3 startScale = new Vector3(0.1f, 0.1f, 0.1f);
        orbiter.transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            orbiter.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        yield return new WaitForSeconds(duration - 1f);
        Destroy(orbiter);
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

        if (explanationRoutine != null)
            ForceResetSpellExplanation();

        isSpellSlowdownActive = true;

        if (spellExplanationText != null)
            spellExplanationText.text = GetSpellDescription(spell);

        spellExplanationUI.SetActive(true);

        lastOriginalTimeScale = Time.timeScale;
        Time.timeScale = slowTimeScale;

        yield return new WaitForSecondsRealtime(explanationDuration);

        ForceResetSpellExplanation();
        isSpellSlowdownActive = false;
    }

    public void ForceResetSpellExplanation()
    {
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
            case "Stone": return "Stone: Rock solid defense!";
            case "Chronos": return "Chronos: Time itself bends!";
            case "Gemini": return "Gemini: Summon a clone!";
            case "Blink": return "Blink: Blink and you'll miss it!";
            case "Jolly": return "Jolly: Joyfully large range!";
            case "Mud": return "Mud: Muddy attack!";
            case "Warp": return "Warp: Portal-hopping ball!";
            case "Pisces": return "Pisces: Fishy defense!";
            case "Tether": return "Tether: Tied-down attack!";
            case "Gorbino": return "Gorbino: Gorbino.";
            case "Gambit": return "Gambit: Take a gamble!";
            default: return "A mysterious spell...";
        }
    }

    public static void ResetSpellsForNewRound()
    {
        spellsUsedThisRound.Clear();
    }
}