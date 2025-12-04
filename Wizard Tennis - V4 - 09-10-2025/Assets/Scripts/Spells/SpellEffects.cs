using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

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
    [HideInInspector] public GameObject activeIceBlock;
    public GameObject stoneWallPrefab;
    [HideInInspector] public GameObject activeStoneWall;
    public GameObject geminiPrefab;
    [HideInInspector] public GameObject activeGemini;
    public GameObject mudPrefab;
    [HideInInspector] public GameObject activeMud;
    public GameObject orbiterPrefab;
    [HideInInspector] public GameObject activeOrbiter;
    public GameObject tetherPrefab;
    [HideInInspector] public GameObject activeTether;
    public GameObject jollyPrefab;
    [HideInInspector] public GameObject activeJolly;
    private GameObject Gorbino;
    public GameObject BallPrefab;
    [HideInInspector] public GameObject activeBall;
    public GameObject flamePrefab;
    [HideInInspector] public GameObject activeFlame;

    [Header("Shadow Spell Settings")]
    public Material invisibleMaterial; // Assign a transparent material for invisibility
    private Material originalOpponentMaterial;
    private Renderer opponentRenderer;

    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();
    private Coroutine explanationRoutine;
    private float lastOriginalTimeScale = 1f;

    public bool spellHit;
    public static bool isSpellSlowdownActive = false;

    private string[] allSpells = { "Lightning", "Ice", "Fireball", "Shadow", "Green", "Stone", "Chronos", "Gemini", "Blink", "Jolly", "Mud", "Warp", "Pisces", "Tether" };

    private bool isNetworked => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    // Prevent duplicate activation
    private bool networkSpellActive = false;

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
        // Prefer explicit values passed in. If null, try to resolve via NetworkManager / NetworkedSpellcasting.
        if (player == null || opponent == null)
        {
            var netPlayers = FindObjectsOfType<NetworkedSpellcasting>();
            if (netPlayers.Length >= 2)
            {
                if (player == null)
                    player = netPlayers[0].gameObject;
                if (opponent == null)
                    opponent = netPlayers.Length > 1 ? netPlayers[1].gameObject : null;

                foreach (var sc in netPlayers)
                {
                    if (sc.IsOwner)
                    {
                        if (player == null) player = sc.gameObject;
                    }
                }
            }
        }

        currentPlayer = player;
        currentOpponent = opponent;
        currentAI = ai;

        Debug.Log($"[SpellEffects] Context set - Player: {player?.name} (Inst:{player?.GetInstanceID()}), Opponent: {opponent?.name} (Inst:{opponent?.GetInstanceID()}), AI: {ai != null}, IsNetworked: {isNetworked}");
    }

    /// <summary>
    /// Auto-set context based on who owns the spell - useful for on-hit spells
    /// </summary>
    public void AutoSetContext(GameObject ballOwner)
    {
        if (ballOwner == null)
        {
            Debug.LogWarning("[SpellEffects] AutoSetContext called with null ballOwner");
            return;
        }

        GameObject player = ballOwner;
        GameObject opponent = null;
        TennisAI ai = null;

        if (isNetworked)
        {
            // Determine opponent deterministically by choosing the NetworkedSpellcasting 
            // whose gameObject != ballOwner
            var all = FindObjectsOfType<NetworkedSpellcasting>();
            foreach (var sc in all)
            {
                if (sc.gameObject != ballOwner)
                {
                    opponent = sc.gameObject;
                    break;
                }
            }

            if (opponent == null && NetworkManager.Singleton != null)
            {
                foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
                {
                    var no = kv.Value;
                    if (no != null && no.gameObject != ballOwner && no.GetComponent<NetworkedSpellcasting>() != null)
                    {
                        opponent = no.gameObject;
                        break;
                    }
                }
            }
        }
        else
        {
            // Singleplayer fallback
            if (ballOwner == Player)
            {
                opponent = Opponent;
                ai = TennisAI;
            }
            else
            {
                opponent = Player;
            }
        }

        SetContext(player, opponent, ai);
        Debug.Log($"[SpellEffects] AutoSetContext completed for {ballOwner.name} -> opponent: {opponent?.name}");
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

    /// <summary>
    /// Register a server-spawned networked effect on the client so SpellEffects can reference it.
    /// Called from NetworkedSpellcasting.ClientRpc after server spawns an effect.
    /// </summary>
    public void RegisterNetworkedEffect(string spell, ulong netId)
    {
        if (netId == 0) return;
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var netObj) && netObj != null)
        {
            var go = netObj.gameObject;

            switch (spell)
            {
                case "Ice":
                    activeIceBlock = go;
                    if (GetOpponent() != null)
                    {
                        StartCoroutine(FollowTransform(activeIceBlock, GetOpponent().transform, 5f));
                        // Start freeze coroutine
                        StartCoroutine(FreezeOpponentMovement(GetOpponent(), 5f));
                    }
                    break;

                case "Stone":
                    activeStoneWall = go;
                    StartCoroutine(HandleStoneWall(activeStoneWall, 5f));
                    break;

                case "Gemini":
                    activeGemini = go;
                    Invoke(nameof(resetSpellEffect), 5f);
                    break;

                case "Pisces":
                    activeOrbiter = go;
                    StartCoroutine(HandleOrbiter(activeOrbiter, 5f));
                    break;

                case "Tether":
                    activeTether = go;
                    var tetherComp = activeTether.GetComponent<Tether>();
                    if (tetherComp != null && GetOpponent() != null)
                        tetherComp.Player = GetOpponent().transform;
                    StartCoroutine(HandleStoneWall(activeTether, 5f));
                    Invoke(nameof(resetSpellEffect), 5f);
                    break;

                case "Gorbino":
                    activeBall = go;
                    Invoke(nameof(resetSpellEffect), 5f);
                    break;

                default:
                    // Unknown effect — ignore for now
                    break;
            }
        }
        else
        {
            Debug.LogWarning($"[SpellEffects] RegisterNetworkedEffect: netId {netId} not found in SpawnManager.");
        }
    }

    public void castSpell()
    {
        // Prevent duplicate activation from different clients triggering the same queued action
        if (networkSpellActive)
        {
            Debug.Log($"[SpellEffects] castSpell ignored because another spell is active ({spellName})");
            return;
        }
        networkSpellActive = true;

        GameObject player = GetPlayer();
        GameObject opponent = GetOpponent();
        TennisAI ai = GetAI();

        if (player == null)
        {
            // Try to auto-find players if context is missing
            Debug.LogWarning("[SpellEffects] No player context! Attempting to auto-find players...");

            if (isNetworked)
            {
                // Find both networked players
                NetworkedSpellcasting[] allPlayers = FindObjectsOfType<NetworkedSpellcasting>();
                if (allPlayers.Length >= 2)
                {
                    // Assume first is player, second is opponent
                    player = allPlayers[0].gameObject;
                    opponent = allPlayers[1].gameObject;
                    Debug.Log($"[SpellEffects] Auto-found networked players: {player.name}, {opponent.name}");
                }
                else if (allPlayers.Length == 1)
                {
                    player = allPlayers[0].gameObject;
                    Debug.Log($"[SpellEffects] Auto-found single networked player: {player.name}");
                }
            }
            else
            {
                // Singleplayer fallback
                if (Player != null) player = Player;
                if (Opponent != null) opponent = Opponent;
                if (TennisAI != null) ai = TennisAI;
            }

            if (player == null)
            {
                Debug.LogError("[SpellEffects] Cannot cast spell - no player found even after auto-search!");
                networkSpellActive = false;
                return;
            }

            // Set the context so we don't have to search again
            SetContext(player, opponent, ai);
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
                    // Freeze opponent movement for 5 seconds
                    StartCoroutine(FreezeOpponentMovement(opponent, 5f));

                    if (iceBlockPrefab != null)
                    {
                        if (isNetworked && NetworkManager.Singleton.IsServer)
                        {
                            // Server spawns networked ice block
                            activeIceBlock = Instantiate(iceBlockPrefab, opponent.transform.position, opponent.transform.rotation);
                            NetworkObject iceNetObj = activeIceBlock.GetComponent<NetworkObject>();
                            if (iceNetObj != null)
                            {
                                iceNetObj.Spawn();
                                Debug.Log("[SpellEffects] Server spawned networked ice block");
                            }
                        }
                        else if (!isNetworked)
                        {
                            // Singleplayer - spawn local ice block
                            activeIceBlock = Instantiate(iceBlockPrefab, opponent.transform.position, opponent.transform.rotation);
                        }

                        // Make ice block follow opponent
                        if (activeIceBlock != null)
                        {
                            StartCoroutine(FollowTransform(activeIceBlock, opponent.transform, 5f));
                        }
                    }
                }
                Invoke(nameof(resetSpellEffect), 5f);
                break;


            case "Fireball":
                if (isNetworked && opponent != null)
                {
                    // Networked: knockback happens on hit via ApplyFireballKnockback
                    Debug.Log($"[SpellEffects] Fireball: Armed and ready. Opponent: {opponent.name}");
                }
                else if (ai != null)
                {
                    // Singleplayer: Apply AI debuff
                    ai.ApplyBuff(-0.2f, spellName);
                }

                resetOnOppHit = true;
                break;

            case "Shadow":
                if (!oppHitSpell)
                {
                    oppHitSpell = true;
                }
                else
                {
                    if (isNetworked && player != null)
                    {
                        // Networked: Make CASTER (player) invisible on **caster's client** only
                        var netSpellcasting = player.GetComponent<NetworkedSpellcasting>();
                        if (netSpellcasting != null && netSpellcasting.IsOwner)
                        {
                            opponentRenderer = player.GetComponentInChildren<SkinnedMeshRenderer>();
                            if (opponentRenderer == null)
                                opponentRenderer = player.GetComponentInChildren<MeshRenderer>();

                            if (opponentRenderer != null)
                            {
                                originalOpponentMaterial = opponentRenderer.material;
                                if (invisibleMaterial != null)
                                {
                                    opponentRenderer.material = invisibleMaterial;
                                    Debug.Log("[SpellEffects] Shadow: Applied invisible material to caster");
                                }
                                else
                                {
                                    Material tempMat = new Material(opponentRenderer.material);
                                    Color c = tempMat.color;
                                    c.a = 0.2f;
                                    tempMat.color = c;
                                    opponentRenderer.material = tempMat;
                                    Debug.Log("[SpellEffects] Shadow: Made caster 20% transparent");
                                }
                            }
                        }
                    }
                    else if (!isNetworked && opponent != null)
                    {
                        // Singleplayer: Teleport AI to player position
                        var opp = opponent.GetComponent<OppHitting>();
                        if (opp != null)
                        {
                            opp.xPos = player.transform.position.x;
                            opp.zPos = player.transform.position.z;
                        }
                    }

                    // Cast spell visually
                    var spellcasting = player.GetComponent<Spellcasting>();
                    var netSpellcastingComp = player.GetComponent<NetworkedSpellcasting>();

                    if (spellcasting != null)
                        spellcasting.CastSpellNormal(spellName);
                    else if (netSpellcastingComp != null)
                        netSpellcastingComp.CastSpellNormal(spellName);

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
                    if (isNetworked)
                    {
                        // Server spawned and RegisterNetworkedEffect will handle the wall on clients (HandleStoneWall called in RegisterNetworkedEffect).
                        if (activeStoneWall == null)
                        {
                            StartCoroutine(WaitForEffectAndHandleStoneWall("Stone", 0.05f));
                        }
                    }
                    else
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
                }
                break;

            case "Chronos":
                StartCoroutine(ApplyChronosAfterExplanation());
                break;

            case "Gemini":
                if (isNetworked)
                {
                    if (activeGemini == null)
                        StartCoroutine(WaitForEffectAndHandle("Gemini", 0.05f));
                }
                else
                {
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
                }
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

                if (racketTransform != null && jollyPrefab != null)
                {
                    activeJolly = Instantiate(jollyPrefab, racketTransform.position, Quaternion.identity);

                    // Check if it has NetworkObject
                    if (activeJolly.GetComponent<NetworkObject>() == null)
                    {
                        // Safe to parent
                        activeJolly.transform.SetParent(racketTransform);
                        activeJolly.transform.localPosition = new Vector3(0, 0.05f, 0);
                        activeJolly.transform.localRotation = jollyRot;
                        activeJolly.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                    }
                    else
                    {
                        // Follow manually
                        activeJolly.transform.localRotation = jollyRot;
                        activeJolly.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                        StartCoroutine(FollowTransform(activeJolly, racketTransform, 5f));
                    }

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
                if (orbiterPrefab != null)
                {
                    if (isNetworked)
                    {
                        if (activeOrbiter == null)
                            StartCoroutine(WaitForEffectAndHandle("Pisces", 0.05f));
                    }
                    else
                    {
                        Quaternion orbitRot = player.transform.rotation;
                        activeOrbiter = Instantiate(orbiterPrefab, player.transform.position, orbitRot);

                        if (activeOrbiter.GetComponent<NetworkObject>() == null)
                        {
                            activeOrbiter.transform.SetParent(player.transform);
                            activeOrbiter.transform.localPosition = Vector3.zero;
                        }
                        else
                        {
                            StartCoroutine(FollowTransform(activeOrbiter, player.transform, 5f));
                        }

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
                    }
                }
                break;

            case "Tether":
                if (tetherPrefab != null && opponent != null)
                {
                    if (!isNetworked)
                    {
                        activeTether = Instantiate(tetherPrefab, opponent.transform.position, Quaternion.identity);
                        var tether = activeTether.GetComponent<Tether>();
                        if (tether != null)
                            tether.Player = opponent.transform;

                        StartCoroutine(HandleStoneWall(activeTether, 5f));
                        Invoke(nameof(resetSpellEffect), 5f);
                    }
                    else
                    {
                        if (activeTether == null)
                            StartCoroutine(WaitForEffectAndHandle("Tether", 0.05f));
                    }
                }
                break;

            case "Gorbino":
                if (!isNetworked)
                {
                    Gorbino = GameObject.Find("Gorbino");
                    if (Gorbino != null)
                    {
                        activeBall = Instantiate(BallPrefab, Gorbino.transform.position, Quaternion.identity);
                        Gorbino.SetActive(false);
                        Invoke(nameof(resetSpellEffect), 5f);
                    }
                }
                else
                {
                    if (activeBall == null)
                        StartCoroutine(WaitForEffectAndHandle("Gorbino", 0.05f));
                }
                break;

            case "Gambit":
                int spellInt = Random.Range(0, allSpells.Length);
                spellName = allSpells[spellInt];
                castSpell();
                return;
        }

        // Cast spell visually if not an on-hit spell
        if (!oppHitSpell && !plrHitSpell)
        {
            var spellcasting = player.GetComponent<Spellcasting>();
            var netSpellcasting = player.GetComponent<NetworkedSpellcasting>();

            if (spellcasting != null)
                spellcasting.CastSpellNormal(spellName);
            else if (netSpellcasting != null)
                netSpellcasting.CastSpellNormal(spellName);
        }
        else
        {
            Debug.Log($"[SpellEffects] Skipping visual cast for on-hit spell '{spellName}' (oppHitSpell: {oppHitSpell}, plrHitSpell: {plrHitSpell})");
        }
    }

    public void resetSpellEffect()
    {
        GameObject player = GetPlayer();
        GameObject opponent = GetOpponent();

        if (player == null)
        {
            networkSpellActive = false;
            return;
        }

        switch (spellName)
        {
            case "Lightning":
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null) mcm.speed = 7;
                break;

            case "Ice":
                if (opponent != null)
                {
                    // Movement is restored by FreezeOpponentMovement coroutine
                    // But we ensure it's reset here as a safety measure
                    if (isNetworked)
                    {
                        var oppMcm = opponent.GetComponent<MainCharacterMovement>();
                        if (oppMcm != null && oppMcm.speed == 0f)
                        {
                            oppMcm.speed = 7;
                            Debug.Log($"[SpellEffects] Ice Reset: Safety restore opponent {opponent.name} speed to 7");
                        }
                    }
                    else
                    {
                        var oppHit = opponent.GetComponent<OppHitting>();
                        if (oppHit != null && oppHit.speed == 0f)
                            oppHit.speed = 5;
                    }
                }

                // Despawn ice block properly
                DespawnIceBlock();
                break;

            case "Fireball":
                // Spawn flame effect on opponent
                if (flamePrefab != null && opponent != null)
                {
                    activeFlame = Instantiate(flamePrefab, opponent.transform.position, Quaternion.identity);
                    activeFlame.transform.SetParent(opponent.transform);
                    activeFlame.transform.localPosition = new Vector3(0, 1.65f, 0);
                    StartCoroutine(HandleMud(activeFlame, 3f));
                }

                resetOnOppHit = false;
                break;

            case "Shadow":
                resetOnOppHit = false;

                // Restore opponent's material
                if (opponentRenderer != null && originalOpponentMaterial != null)
                {
                    opponentRenderer.material = originalOpponentMaterial;
                    opponentRenderer = null;
                    originalOpponentMaterial = null;
                }
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
                if (activeTether != null)
                {
                    var net = activeTether.GetComponent<NetworkObject>();
                    if (net != null)
                    {
                        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                        {
                            net.Despawn();
                        }
                        else
                        {
                            Destroy(activeTether);
                        }
                    }
                    else
                    {
                        Destroy(activeTether);
                    }
                    activeTether = null;
                }

                if (opponent != null)
                {
                    if (!isNetworked)
                    {
                        var oppHit = opponent.GetComponent<OppHitting>();
                        if (oppHit != null)
                            oppHit.tether = null;
                    }
                }
                break;

            case "Gorbino":
                if (activeBall != null)
                {
                    var net = activeBall.GetComponent<NetworkObject>();
                    if (net != null)
                    {
                        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                        {
                            net.Despawn();
                        }
                        else
                        {
                            Destroy(activeBall);
                        }
                    }
                    else
                        Destroy(activeBall);
                    activeBall = null;
                }
                if (Gorbino != null)
                    Gorbino.SetActive(true);
                break;
        }

        // clear network guard and flags
        networkSpellActive = false;

        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
        spellHit = false;
    }

    private IEnumerator ApplyKnockback(GameObject target, Vector3 knockbackForce, float duration)
    {
        var controller = target.GetComponent<CharacterController>();
        var movement = target.GetComponent<MainCharacterMovement>();

        if (controller == null)
        {
            Debug.LogWarning($"[SpellEffects] Knockback failed: No CharacterController on {target.name}");
            yield break;
        }

        // Temporarily disable player movement control
        bool wasMovementEnabled = true;
        if (movement != null)
        {
            wasMovementEnabled = movement.enabled;
            movement.enabled = false;
        }

        float elapsed = 0f;
        Vector3 totalKnockback = Vector3.zero;

        Debug.Log($"[SpellEffects] Starting knockback on {target.name}, force: {knockbackForce}, duration: {duration}");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration); // Decay over time

            Vector3 frameKnockback = knockbackForce * t * Time.deltaTime;
            controller.Move(frameKnockback);
            totalKnockback += frameKnockback;

            yield return null;
        }

        Debug.Log($"[SpellEffects] Knockback complete on {target.name}, total distance: {totalKnockback.magnitude}");

        // Re-enable player movement control
        if (movement != null)
            movement.enabled = wasMovementEnabled;
    }

    private IEnumerator ApplyTetherRestriction(GameObject target, Vector3 tetherPos, float duration)
    {
        var mcm = target.GetComponent<MainCharacterMovement>();
        if (mcm == null) yield break;

        float maxDistance = 3f; // Maximum distance from tether
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Check distance from tether
            Vector3 targetPos = target.transform.position;
            targetPos.y = tetherPos.y; // Ignore Y axis
            Vector3 tetherPosFlat = tetherPos;

            float distance = Vector3.Distance(new Vector3(targetPos.x, 0, targetPos.z), new Vector3(tetherPosFlat.x, 0, tetherPosFlat.z));

            if (distance > maxDistance)
            {
                // Pull target back towards tether
                Vector3 direction = (new Vector3(tetherPosFlat.x, targetPos.y, tetherPosFlat.z) - targetPos).normalized;
                Vector3 clampedPos = new Vector3(tetherPosFlat.x, targetPos.y, tetherPosFlat.z) + direction * -maxDistance;

                var controller = target.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                    target.transform.position = new Vector3(clampedPos.x, target.transform.position.y, clampedPos.z);
                    controller.enabled = true;
                }
            }

            yield return null;
        }
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

    /// <summary>
    /// Makes an object follow a transform without parenting (useful for NetworkObjects)
    /// </summary>
    private IEnumerator FollowTransform(GameObject follower, Transform target, float duration)
    {
        if (follower == null || target == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration && follower != null && target != null)
        {
            follower.transform.position = target.position;
            follower.transform.rotation = target.rotation;
            elapsed += Time.deltaTime;
            yield return null;
        }
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

    // ---------------- Helpers used above: wait for a server-spawned effect then operate on it ----------------
    private IEnumerator WaitForEffectAndFollow(string spell, float waitSeconds, Transform target)
    {
        float timer = 0f;
        while (timer < 2.0f)
        {
            timer += waitSeconds;
            if (spell == "Ice" && activeIceBlock != null)
            {
                StartCoroutine(FollowTransform(activeIceBlock, target, 5f));
                yield break;
            }
            yield return new WaitForSeconds(waitSeconds);
        }
    }

    private IEnumerator WaitForEffectAndHandleStoneWall(string spell, float waitSeconds)
    {
        float timer = 0f;
        while (timer < 2.0f)
        {
            timer += waitSeconds;
            if (activeStoneWall != null)
            {
                StartCoroutine(HandleStoneWall(activeStoneWall, 5f));
                Invoke(nameof(resetSpellEffect), 5f);
                yield break;
            }
            yield return new WaitForSeconds(waitSeconds);
        }
    }

    private IEnumerator WaitForEffectAndHandle(string spell, float waitSeconds)
    {
        float timer = 0f;
        while (timer < 2.0f)
        {
            timer += waitSeconds;
            switch (spell)
            {
                case "Gemini":
                    if (activeGemini != null)
                    {
                        Invoke(nameof(resetSpellEffect), 5f);
                        yield break;
                    }
                    break;

                case "Pisces":
                    if (activeOrbiter != null)
                    {
                        StartCoroutine(HandleOrbiter(activeOrbiter, 5f));
                        yield break;
                    }
                    break;

                case "Tether":
                    if (activeTether != null)
                    {
                        StartCoroutine(HandleStoneWall(activeTether, 5f));
                        Invoke(nameof(resetSpellEffect), 5f);
                        yield break;
                    }
                    break;

                case "Gorbino":
                    if (activeBall != null)
                    {
                        Invoke(nameof(resetSpellEffect), 5f);
                        yield break;
                    }
                    break;
            }

            yield return new WaitForSeconds(waitSeconds);
        }
    }

    // ====== Helper Methods for Networked Spells =======

    /// <summary>
    /// Freeze opponent movement for Ice spell
    /// </summary>
    private IEnumerator FreezeOpponentMovement(GameObject target, float duration)
    {
        if (target == null) yield break;

        var mcm = target.GetComponent<MainCharacterMovement>();
        var oppHit = target.GetComponent<OppHitting>();

        float originalMcmSpeed = 7f;
        float originalOppSpeed = 5f;

        // Store original speeds
        if (mcm != null)
            originalMcmSpeed = mcm.speed;
        if (oppHit != null)
            originalOppSpeed = oppHit.speed;

        // Freeze movement
        if (mcm != null)
        {
            mcm.speed = 0f;
            Debug.Log($"[SpellEffects] Froze {target.name} movement (MainCharacterMovement)");
        }
        if (oppHit != null)
        {
            oppHit.speed = 0f;
            Debug.Log($"[SpellEffects] Froze {target.name} movement (OppHitting)");
        }

        // Wait for duration
        yield return new WaitForSeconds(duration);

        // Restore movement
        if (mcm != null)
        {
            mcm.speed = originalMcmSpeed;
            Debug.Log($"[SpellEffects] Restored {target.name} movement to {originalMcmSpeed}");
        }
        if (oppHit != null)
        {
            oppHit.speed = originalOppSpeed;
            Debug.Log($"[SpellEffects] Restored {target.name} AI speed to {originalOppSpeed}");
        }
    }

    /// <summary>
    /// Apply knockback to opponent from Fireball
    /// </summary>
    public void ApplyFireballKnockback(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[SpellEffects] ApplyFireballKnockback: target is null");
            return;
        }

        // Determine knockback direction (away from caster)
        GameObject caster = GetPlayer();
        Vector3 knockbackDirection = Vector3.back; // default

        if (caster != null && target != null)
        {
            Vector3 directionFromCaster = (target.transform.position - caster.transform.position).normalized;
            knockbackDirection = new Vector3(directionFromCaster.x, 0, directionFromCaster.z).normalized;
        }

        // Apply knockback force
        Vector3 knockbackForce = knockbackDirection * 8f; // Adjust force as needed
        StartCoroutine(ApplyKnockback(target, knockbackForce, 0.3f));

        Debug.Log($"[SpellEffects] Applied Fireball knockback to {target.name}");
    }

    /// <summary>
    /// Despawn ice block properly (networked or local)
    /// </summary>
    private void DespawnIceBlock()
    {
        if (activeIceBlock == null) return;

        var netObj = activeIceBlock.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            // Networked object - only server can despawn
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Despawn();
                Debug.Log("[SpellEffects] Ice block despawned by server");
            }
            else
            {
                Debug.Log("[SpellEffects] Ice block will be despawned by server");
            }
        }
        else
        {
            // Local object - destroy directly
            Destroy(activeIceBlock);
            Debug.Log("[SpellEffects] Local ice block destroyed");
        }

        activeIceBlock = null;
    }
}
