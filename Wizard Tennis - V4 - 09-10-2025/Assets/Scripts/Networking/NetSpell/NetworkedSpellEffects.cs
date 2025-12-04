using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// SpellEffects - client side handler for visual and client-only behaviour.
/// </summary>
public class NetworkedSpellEffects : MonoBehaviour
{
    // Context references (dynamically set per spell cast)
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
    public Material invisibleMaterial;
    private Material originalOpponentMaterial;
    private Renderer opponentRenderer;

    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();
    private Coroutine explanationRoutine;
    private float lastOriginalTimeScale = 1f;

    public bool spellHit;
    public static bool isSpellSlowdownActive = false;

    private string[] allSpells = { "Lightning", "Ice", "Fireball", "Shadow", "Green", "Stone", "Chronos", "Gemini", "Blink", "Jolly", "Mud", "Warp", "Pisces", "Tether" };

    private bool networkSpellActive = false;

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

        // Use NetworkedSpellManager if available
        if (NetworkedSpellManager.Instance != null)
        {
            var caster = ballOwner.GetComponent<NetworkedSpellcasting>();
            if (caster != null)
            {
                var opp = NetworkedSpellManager.Instance.GetOpponent(caster);
                opponent = opp?.gameObject;
            }
        }
        else
        {
            // Fallback: Determine opponent deterministically
            var all = FindObjectsOfType<NetworkedSpellcasting>();
            foreach (var sc in all)
            {
                if (sc.gameObject != ballOwner)
                {
                    opponent = sc.gameObject;
                    break;
                }
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
    /// Gets the active player (context)
    /// </summary>
    private GameObject GetPlayer()
    {
        return currentPlayer;
    }

    /// <summary>
    /// Gets the active opponent (context)
    /// </summary>
    private GameObject GetOpponent()
    {
        return currentOpponent;
    }

    /// <summary>
    /// Gets the active AI (context)
    /// </summary>
    private TennisAI GetAI()
    {
        return currentAI;
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
                    // Unhandled net-spawned effect
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
        // Prevent duplicate activation
        if (networkSpellActive)
        {
            Debug.Log($"[SpellEffects] castSpell ignored because another spell is active ({spellName})");
            return;
        }
        networkSpellActive = true;

        GameObject player = GetPlayer();
        GameObject opponent = GetOpponent();

        if (player == null)
        {
            Debug.LogWarning("[SpellEffects] No player context! Attempting to auto-find players...");

            // Use NetworkedSpellManager if available
            if (NetworkedSpellManager.Instance != null && NetworkedSpellManager.Instance.GetPlayerCount() >= 1)
            {
                var players = NetworkedSpellManager.Instance.GetAllPlayers();
                if (players.Count >= 1)
                {
                    player = players[0].gameObject;
                    if (players.Count >= 2)
                        opponent = players[1].gameObject;
                }
            }
            else
            {
                // Fallback: Find both networked players
                NetworkedSpellcasting[] allPlayers = FindObjectsOfType<NetworkedSpellcasting>();
                if (allPlayers.Length >= 2)
                {
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

            if (player == null)
            {
                Debug.LogError("[SpellEffects] Cannot cast spell - no player found even after auto-search!");
                networkSpellActive = false;
                return;
            }

            // Set the context so we don't have to search again
            SetContext(player, opponent, null);
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
                    // Freeze opponent movement
                    StartCoroutine(FreezeOpponentMovement(opponent, 5f));

                    // Ice block is spawned by server and registered via RegisterNetworkedEffect
                    // No need to spawn here - just wait for it
                }
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Fireball":
                // Networked: knockback happens on hit via ApplyFireballKnockback (called from NetworkedBall)
                if (opponent != null)
                {
                    Debug.Log($"[SpellEffects] Fireball: Armed and ready. Opponent: {opponent.name}");
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
                    if (player != null)
                    {
                        // Make CASTER (player) invisible on **caster's client** only
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

                    // Cast spell visually
                    var netSpellcastingComp = player.GetComponent<NetworkedSpellcasting>();
                    if (netSpellcastingComp != null)
                        netSpellcastingComp.CastSpellNormal(spellName);

                    resetOnOppHit = true;
                }
                break;

            case "Green":
                var greenBall = player.GetComponent<NetworkedBall>();
                if (greenBall != null) greenBall.green = true;
                Invoke(nameof(resetSpellEffect), 1f);
                break;

            case "Stone":
                // Server spawned and RegisterNetworkedEffect will handle the wall on clients
                if (activeStoneWall == null)
                {
                    StartCoroutine(WaitForEffectAndHandleStoneWall("Stone", 0.05f));
                }
                break;

            case "Chronos":
                StartCoroutine(ApplyChronosAfterExplanation());
                break;

            case "Gemini":
                // Server spawned and RegisterNetworkedEffect will handle
                if (activeGemini == null)
                    StartCoroutine(WaitForEffectAndHandle("Gemini", 0.05f));
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
                // Server spawned and RegisterNetworkedEffect will handle
                if (activeOrbiter == null)
                    StartCoroutine(WaitForEffectAndHandle("Pisces", 0.05f));
                break;

            case "Tether":
                // Server spawned and RegisterNetworkedEffect will handle
                if (activeTether == null)
                    StartCoroutine(WaitForEffectAndHandle("Tether", 0.05f));
                break;

            case "Gorbino":
                // Server spawned and RegisterNetworkedEffect will handle
                if (activeBall == null)
                    StartCoroutine(WaitForEffectAndHandle("Gorbino", 0.05f));
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
            var netSpellcasting = player.GetComponent<NetworkedSpellcasting>();
            if (netSpellcasting != null)
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
                    var oppMcm = opponent.GetComponent<MainCharacterMovement>();
                    if (oppMcm != null && oppMcm.speed == 0f)
                    {
                        oppMcm.speed = 7;
                        Debug.Log($"[SpellEffects] Ice Reset: Safety restore opponent {opponent.name} speed to 7");
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

                var netBallComp = player.GetComponent<NetworkedBall>();
                if (netBallComp != null && netBallComp.aimTarget != null && netBallComp.aimTarget.position.x > 0)
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

        // Clear network guard and flags
        networkSpellActive = false;

        spellName = null;
        plrHitSpell = false;
        oppHitSpell = false;
        spellHit = false;
    }

    /// <summary>
    /// Freeze opponent movement for Ice spell
    /// </summary>
    private IEnumerator FreezeOpponentMovement(GameObject target, float duration)
    {
        if (target == null) yield break;

        var mcm = target.GetComponent<MainCharacterMovement>();
        float originalMcmSpeed = 7f;

        // Store original speed
        if (mcm != null)
            originalMcmSpeed = mcm.speed;

        // Freeze movement
        if (mcm != null)
        {
            mcm.speed = 0f;
            Debug.Log($"[SpellEffects] Froze {target.name} movement (MainCharacterMovement)");
        }

        // Wait for duration
        yield return new WaitForSeconds(duration);

        // Restore movement
        if (mcm != null)
        {
            mcm.speed = originalMcmSpeed;
            Debug.Log($"[SpellEffects] Restored {target.name} movement to {originalMcmSpeed}");
        }
    }

    /// <summary>
    /// Apply knockback to opponent from Fireball (PUBLIC so NetworkedBall can call it)
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
        Vector3 knockbackForce = knockbackDirection * 8f;
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

    private IEnumerator ApplyChronosAfterExplanation()
    {
        GameObject player = GetPlayer();
        if (player == null) yield break;

        // Wait until any other slowdowns finish
        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);
        yield return new WaitForSecondsRealtime(0.05f);

        // Slow global time for client visuals only
        lastOriginalTimeScale = Time.timeScale;
        isSpellSlowdownActive = true;
        Time.timeScale = 0.1f;

        // Reduce player movement locally
        var mcm = player.GetComponent<MainCharacterMovement>();
        if (mcm != null)
        {
            mcm.speed = 4f;
            mcm.gravity = 5f;
        }

        // Show explanation if necessary
        if (OptionsManager.Instance != null && OptionsManager.Instance.spellTips && !string.IsNullOrEmpty(spellName))
        {
            if (explanationRoutine != null) StopCoroutine(explanationRoutine);
            explanationRoutine = StartCoroutine(ShowSpellExplanation(spellName));
            yield return new WaitForSecondsRealtime(explanationDuration);
        }
        else
        {
            yield return new WaitForSecondsRealtime(2.5f);
        }

        // Restore time
        Time.timeScale = lastOriginalTimeScale;
        isSpellSlowdownActive = false;

        // Restore player movement
        if (mcm != null)
        {
            mcm.speed = 7f;
            mcm.gravity = 25f;
        }

        Invoke(nameof(resetSpellEffect), 0.1f);
    }

    private IEnumerator ShowSpellExplanation(string spell)
    {
        if (spellExplanationUI == null || spellExplanationText == null)
            yield break;

        spellExplanationText.text = spell;
        spellExplanationUI.SetActive(true);

        // slow-motion effect for emphasis (client-only)
        float saved = Time.timeScale;
        Time.timeScale = slowTimeScale;

        yield return new WaitForSecondsRealtime(explanationDuration);

        Time.timeScale = saved;
        spellExplanationUI.SetActive(false);
    }

    private IEnumerator FollowTransform(GameObject go, Transform target, float duration)
    {
        if (go == null || target == null) yield break;

        float timer = 0f;
        while (timer < duration)
        {
            if (go == null) yield break;
            go.transform.position = target.position;
            go.transform.rotation = target.rotation;
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForEffectAndHandle(string spell, float pollDelay)
    {
        // Poll until the effect appears (registered via RegisterNetworkedEffect)
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (spell == "Gemini" && activeGemini != null) break;
            if (spell == "Pisces" && activeOrbiter != null) break;
            if (spell == "Tether" && activeTether != null) break;
            if (spell == "Gorbino" && activeBall != null) break;

            elapsed += pollDelay;
            yield return new WaitForSeconds(pollDelay);
        }

        // If still null after timeout, log
        if (spell == "Gemini" && activeGemini == null) Debug.LogWarning("[SpellEffects] Gemini didn't appear (timeout)");
        if (spell == "Pisces" && activeOrbiter == null) Debug.LogWarning("[SpellEffects] Pisces didn't appear (timeout)");
        if (spell == "Tether" && activeTether == null) Debug.LogWarning("[SpellEffects] Tether didn't appear (timeout)");
        if (spell == "Gorbino" && activeBall == null) Debug.LogWarning("[SpellEffects] Gorbino didn't appear (timeout)");
    }

    private IEnumerator WaitForEffectAndHandleStoneWall(string spell, float pollDelay)
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (activeStoneWall != null) break;

            elapsed += pollDelay;
            yield return new WaitForSeconds(pollDelay);
        }

        if (activeStoneWall != null)
        {
            StartCoroutine(HandleStoneWall(activeStoneWall, 5f));
        }
        else
        {
            Debug.LogWarning("[SpellEffects] Stone wall did not register within timeout.");
        }
    }

    private IEnumerator HandleStoneWall(GameObject wall, float duration)
    {
        if (wall == null) yield break;

        float t = 0f;
        Vector3 startScale = wall.transform.localScale;
        Vector3 targetScale = startScale;
        // growth animation (client-side)
        wall.transform.localScale = Vector3.zero;

        while (t < 0.25f)
        {
            t += Time.deltaTime;
            wall.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t / 0.25f);
            yield return null;
        }

        // stay for duration
        yield return new WaitForSeconds(duration);

        // shrink and destroy if not networked (server should despawn networked)
        float s = 0f;
        while (s < 0.25f)
        {
            s += Time.deltaTime;
            wall.transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, s / 0.25f);
            yield return null;
        }

        var net = wall.GetComponent<NetworkObject>();
        if (net != null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                net.Despawn();
            else
                Destroy(wall);
        }
        else
        {
            Destroy(wall);
        }
    }

    private IEnumerator HandleOrbiter(GameObject orbiter, float duration)
    {
        if (orbiter == null) yield break;

        float elapsed = 0f;
        Transform center = GetPlayer()?.transform ?? transform;
        while (elapsed < duration)
        {
            if (orbiter == null) yield break;
            // Simple orbit around player
            orbiter.transform.RotateAround(center.position, Vector3.up, 180f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (orbiter != null)
            Destroy(orbiter);
    }

    private IEnumerator HandleMud(GameObject mud, float duration)
    {
        if (mud == null) yield break;
        // simple lifetime
        yield return new WaitForSeconds(duration);
        if (mud != null)
            Destroy(mud);
    }

    /// <summary>
    /// Despawn ice block properly (exposed for external callers)
    /// </summary>
    public void DeleteIceBlock()
    {
        DespawnIceBlock();
    }

    /// <summary>
    /// Called by other systems when player hits opponent (on-hit spells)
    /// </summary>
    public void OnPlayerHitOpponent(GameObject hitter, GameObject victim)
    {
        // Called by your game when a hit occurs (the networked Ball/Hit system should call this)
        // If the currently active spell is an on-hit spell, resolve effect here.
        if (string.IsNullOrEmpty(spellName)) return;

        if (resetOnOppHit)
        {
            // Do immediate effect e.g. for Fireball spawn flame and then reset
            if (spellName == "Fireball")
            {
                ApplyFireballKnockback(victim);
            }

            resetSpellEffect();
        }

        // Example: if Mud causes a slow on hit
        if (spellName == "Mud")
        {
            // spawn local mud on the victim's feet
            if (mudPrefab != null && victim != null)
            {
                var m = Instantiate(mudPrefab, victim.transform.position, Quaternion.identity);
                m.transform.SetParent(victim.transform);
                m.transform.localPosition = new Vector3(0, 0.1f, 0);
                StartCoroutine(HandleMud(m, 4f));
            }

            resetSpellEffect();
        }
    }
}
