using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// NetworkedSpellEffects - Complete spell system with server-authoritative spawning
/// Handles both networked and client-side spell effects
/// </summary>

// Welcome to my carnival of madness

// God is no longer here

// Only suffering awaits below

public class NetworkedSpellEffects : NetworkBehaviour
{
    public static NetworkedSpellEffects Instance { get; private set; }

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

    [Header("Spell Object Prefabs - ASSIGN NETWORKED PREFABS HERE")]
    public GameObject iceBlockPrefab;
    public GameObject stoneWallPrefab;
    public GameObject geminiPrefab;
    public GameObject mudPrefab;
    public GameObject orbiterPrefab;
    public GameObject tetherPrefab;
    public GameObject jollyPrefab;
    public GameObject BallPrefab;
    public GameObject flamePrefab;

    [Header("Active References (Runtime)")]
    [HideInInspector] public GameObject activeIceBlock;
    [HideInInspector] public GameObject activeStoneWall;
    [HideInInspector] public GameObject activeGemini;
    [HideInInspector] public GameObject activeMud;
    [HideInInspector] public GameObject activeOrbiter;
    [HideInInspector] public GameObject activeTether;
    [HideInInspector] public GameObject activeJolly;
    [HideInInspector] public GameObject activeBall;
    [HideInInspector] public GameObject activeFlame;

    [Header("Shadow Spell Settings")]
    public Material invisibleMaterial;  // Material opponent sees (invisible/transparent)
    public Material shadowCasterMaterial;  // Material caster sees (visual feedback - e.g. glowing/outlined)
    private Material originalOpponentMaterial;
    private Renderer opponentRenderer;

    [Header("Other Spell Settings")]
    public float fireballForceStrength = 20f;

    [Header("Lightning Trail Objects")]
    private Dictionary<ulong, GameObject> playerLightningTrails = new Dictionary<ulong, GameObject>();

    private static HashSet<string> spellsUsedThisRound = new HashSet<string>();
    private Coroutine explanationRoutine;
    private float lastOriginalTimeScale = 1f;

    public bool spellHit;
    public static bool isSpellSlowdownActive = false;

    private string[] allSpells = { "Lightning", "Ice", "Fireball", "Shadow", "Green", "Stone", "Chronos", "Gemini", "Blink", "Jolly", "Mud", "Warp", "Pisces", "Tether", "Gambit" };

    [SerializeField]
    private string[] gambitSpells = new string[]
    {
    "Lightning",
    "Ice",
    "Fireball",
    "Shadow",
    "Mud",
    "Green",
    "Stone",
    "Chronos",
    "Gemini",
    "Blink",
    "Jolly",
    "Warp",
    "Pisces",
    "Tether",
    "Gorbino"
    };

    /// <summary>
    /// Get the list of spells Gambit can roll
    /// </summary>
    public string[] GetGambitSpells()
    {
        return gambitSpells;
    }

    private bool networkSpellActive = false;
    private GameObject Gorbino;

    private Vector3 storedBlinkDirection = Vector3.zero;

    private Coroutine activeLightningCoroutine = null;

    // Track active networked effects for cleanup
    private Dictionary<string, ulong> activeNetworkEffects = new Dictionary<string, ulong>();

    private Dictionary<ulong, Coroutine> activeFreezeCoroutines = new Dictionary<ulong, Coroutine>();

    public NetworkVariable<bool> IsAnySpellActive =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Global Spell Lock
    [ServerRpc(RequireOwnership = false)]
    public void BeginGlobalSpellLockServerRpc()
    {
        if (IsAnySpellActive.Value) return;
        IsAnySpellActive.Value = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndGlobalSpellLockServerRpc()
    {
        IsAnySpellActive.Value = false;
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
    /// FIXED: Properly returns the local player's character, not spawner objects
    /// </summary>
    public GameObject GetPlayer()
    {
        // First: Try to use the stored context player
        if (currentPlayer != null)
        {
            Debug.Log($"[SpellEffects-GetPlayer] Using stored context player: {currentPlayer.name}");
            return currentPlayer;
        }

        // Second: Try to find by LocalClientId (this is the player casting the spell)
        ulong localId = NetworkManager.Singleton.LocalClientId;
        GameObject player = GetPlayerByClientId(localId);

        if (player != null)
        {
            Debug.Log($"[SpellEffects-GetPlayer] Found local player: {player.name} at position {player.transform.position}");
            return player;
        }

        // Third: Fallback - find ANY NetworkedSpellcasting with valid components
        Debug.LogWarning($"[SpellEffects-GetPlayer] Could not find player by LocalClientId {localId}, searching for valid player...");

        foreach (var spellcasting in FindObjectsOfType<NetworkedSpellcasting>())
        {
            GameObject go = spellcasting.gameObject;

            // Skip objects without player components (these are likely spawners)
            if (go.GetComponent<MainCharacterMovement>() == null &&
                go.GetComponent<CharacterController>() == null &&
                go.GetComponent<Rigidbody>() == null)
            {
                Debug.Log($"[SpellEffects-GetPlayer] Skipping {go.name} - no movement components");
                continue;
            }

            // Make sure it's owned by the local client
            NetworkObject netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == localId)
            {
                Debug.Log($"[SpellEffects-GetPlayer] Found valid player via fallback: {go.name} at {go.transform.position}");
                return go;
            }
        }

        Debug.LogError($"[SpellEffects-GetPlayer] Could not find player for LocalClientId {localId}!");
        return null;
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

    // ========== SHADOW SPELL NETWORK METHODS ==========

    /// <summary>
    /// Server applies Shadow invisibility effect
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ApplyShadowInvisibilityServerRpc(ulong casterClientId, ulong opponentClientId)
    {
        if (!IsServer) return;

        Debug.Log($"[SpellEffects-Server] Applying Shadow invisibility - caster: {casterClientId}, opponent: {opponentClientId}");

        // Tell the OPPONENT client to make the caster invisible
        ApplyShadowToOpponentClientRpc(casterClientId, opponentClientId);

        // Tell the CASTER client to apply visual feedback
        ApplyShadowToCasterClientRpc(casterClientId, opponentClientId);
    }

    /// <summary>
    /// Client applies Shadow invisibility (only opponent sees it)
    /// </summary>
    [ClientRpc]
    private void ApplyShadowToOpponentClientRpc(ulong casterClientId, ulong opponentClientId)
    {
        // Only the OPPONENT client should apply invisibility
        if (NetworkManager.Singleton.LocalClientId != opponentClientId)
        {
            Debug.Log($"[SpellEffects-Client] Not the opponent ({NetworkManager.Singleton.LocalClientId} != {opponentClientId}), ignoring Shadow invisibility");
            return;
        }

        GameObject caster = GetPlayerByClientId(casterClientId);
        if (caster == null)
        {
            Debug.LogWarning($"[SpellEffects-Client] Could not find caster with ID {casterClientId} for Shadow spell");
            return;
        }

        Debug.Log($"[SpellEffects-Client] Applying Shadow invisibility to {caster.name} (opponent view)");

        // Get all renderers in the caster (including children)
        Renderer[] renderers = caster.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Skip certain objects (like racket effects, particles, etc.)
            if (renderer.gameObject.name.Contains("Particle") ||
                renderer.gameObject.name.Contains("Effect") ||
                renderer.gameObject.name.Contains("Trail"))
                continue;

            // Store original material if not already stored
            if (!shadowOriginalMaterials.ContainsKey(renderer))
            {
                shadowOriginalMaterials[renderer] = renderer.material;
                Debug.Log($"[SpellEffects-Client] Stored original material for {renderer.gameObject.name}");
            }

            // Apply invisible material
            if (invisibleMaterial != null)
            {
                renderer.material = invisibleMaterial;
                Debug.Log($"[SpellEffects-Client] Applied invisible material to {renderer.gameObject.name}");
            }
            else
            {
                // Fallback: make semi-transparent
                Material tempMat = new Material(renderer.material);
                Color c = tempMat.color;
                c.a = 0.2f;
                tempMat.color = c;
                renderer.material = tempMat;
                Debug.Log($"[SpellEffects-Client] Applied transparency to {renderer.gameObject.name}");
            }
        }

        // Store who is currently shadow-invisible
        currentShadowCasterId = casterClientId;
    }

    /// <summary>
    /// Client applies Shadow visual feedback (only caster sees it)
    /// </summary>
    [ClientRpc]
    private void ApplyShadowToCasterClientRpc(ulong casterClientId, ulong opponentClientId)
    {
        // Only the CASTER client should apply visual feedback
        if (NetworkManager.Singleton.LocalClientId != casterClientId)
        {
            Debug.Log($"[SpellEffects-Client] Not the caster ({NetworkManager.Singleton.LocalClientId} != {casterClientId}), ignoring Shadow feedback");
            return;
        }

        GameObject caster = GetPlayerByClientId(casterClientId);
        if (caster == null)
        {
            Debug.LogWarning($"[SpellEffects-Client] Could not find caster with ID {casterClientId} for Shadow feedback");
            return;
        }

        Debug.Log($"[SpellEffects-Client] Applying Shadow visual feedback to {caster.name} (caster view)");

        // Get all renderers in the caster (including children)
        Renderer[] renderers = caster.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Skip certain objects (like racket effects, particles, etc.)
            if (renderer.gameObject.name.Contains("Particle") ||
                renderer.gameObject.name.Contains("Effect") ||
                renderer.gameObject.name.Contains("Trail"))
                continue;

            // Store original material if not already stored
            if (!shadowCasterOriginalMaterials.ContainsKey(renderer))
            {
                shadowCasterOriginalMaterials[renderer] = renderer.material;
                Debug.Log($"[SpellEffects-Client] Stored original material for caster feedback on {renderer.gameObject.name}");
            }

            // Apply caster feedback material
            if (shadowCasterMaterial != null)
            {
                renderer.material = shadowCasterMaterial;
                Debug.Log($"[SpellEffects-Client] Applied shadow feedback material to {renderer.gameObject.name}");
            }
            else
            {
                // Fallback: make slightly transparent with blue tint
                Material tempMat = new Material(renderer.material);
                Color c = tempMat.color;
                c.a = 0.7f;
                c.b = Mathf.Min(c.b + 0.3f, 1f); // Add blue tint
                tempMat.color = c;
                renderer.material = tempMat;
                Debug.Log($"[SpellEffects-Client] Applied fallback feedback to {renderer.gameObject.name}");
            }
        }

        // Store who is currently seeing shadow feedback
        currentShadowCasterFeedbackId = casterClientId;
    }

    /// <summary>
    /// Server removes Shadow invisibility effect
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RemoveShadowInvisibilityServerRpc(ulong casterClientId, ulong opponentClientId)
    {
        if (!IsServer) return;

        Debug.Log($"[SpellEffects-Server] Removing Shadow invisibility");

        // Tell the OPPONENT client to restore visibility
        RemoveShadowFromOpponentClientRpc(casterClientId, opponentClientId);

        // Tell the CASTER client to remove visual feedback
        RemoveShadowFromCasterClientRpc(casterClientId, opponentClientId);
    }

    /// <summary>
    /// Client removes Shadow invisibility (opponent restores normal view)
    /// </summary>
    [ClientRpc]
    private void RemoveShadowFromOpponentClientRpc(ulong casterClientId, ulong opponentClientId)
    {
        // Only the OPPONENT client should remove invisibility
        if (NetworkManager.Singleton.LocalClientId != opponentClientId)
        {
            return;
        }

        Debug.Log($"[SpellEffects-Client] Removing Shadow invisibility (opponent view)");
        Debug.Log($"[SpellEffects-Client] Materials to restore: {shadowOriginalMaterials.Count}");

        // Restore all original materials
        int restoredCount = 0;
        foreach (var kvp in shadowOriginalMaterials)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.material = kvp.Value;
                restoredCount++;
                Debug.Log($"[SpellEffects-Client] Restored material for {kvp.Key.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[SpellEffects-Client] Null renderer or material in restoration - Renderer: {kvp.Key != null}, Material: {kvp.Value != null}");
            }
        }

        Debug.Log($"[SpellEffects-Client] Restored {restoredCount} materials out of {shadowOriginalMaterials.Count}");

        shadowOriginalMaterials.Clear();
        currentShadowCasterId = ulong.MaxValue;

        Debug.Log($"[SpellEffects-Client] Shadow removed - visibility restored");
    }

    /// <summary>
    /// Client removes Shadow visual feedback (caster restores normal view)
    /// </summary>
    [ClientRpc]
    private void RemoveShadowFromCasterClientRpc(ulong casterClientId, ulong opponentClientId)
    {
        // Only the CASTER client should remove feedback
        if (NetworkManager.Singleton.LocalClientId != casterClientId)
        {
            return;
        }

        Debug.Log($"[SpellEffects-Client] Removing Shadow visual feedback (caster view)");
        Debug.Log($"[SpellEffects-Client] Materials to restore: {shadowCasterOriginalMaterials.Count}");

        // Restore all original materials
        int restoredCount = 0;
        foreach (var kvp in shadowCasterOriginalMaterials)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.material = kvp.Value;
                restoredCount++;
                Debug.Log($"[SpellEffects-Client] Restored feedback material for {kvp.Key.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"[SpellEffects-Client] Null renderer or material in caster restoration - Renderer: {kvp.Key != null}, Material: {kvp.Value != null}");
            }
        }

        Debug.Log($"[SpellEffects-Client] Restored {restoredCount} feedback materials out of {shadowCasterOriginalMaterials.Count}");

        shadowCasterOriginalMaterials.Clear();
        currentShadowCasterFeedbackId = ulong.MaxValue;

        Debug.Log($"[SpellEffects-Client] Shadow feedback removed - normal view restored");
    }

    // Track Shadow spell materials per renderer - SEPARATE dictionaries for opponent and caster
    private Dictionary<Renderer, Material> shadowOriginalMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, Material> shadowCasterOriginalMaterials = new Dictionary<Renderer, Material>();
    private ulong currentShadowCasterId = ulong.MaxValue;
    private ulong currentShadowCasterFeedbackId = ulong.MaxValue;

    // ========== NETWORK SPAWNING SYSTEM ==========

    /// <summary>
    /// Request server to spawn a networked effect
    /// Called by castSpell() when a spell needs a networked object
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void SpawnEffectServerRpc(string spell, ulong casterClientId, ulong targetClientId, Vector3 position, Quaternion rotation, ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[SpellEffects] SpawnEffectServerRpc called on non-server!");
            return;
        }

        Debug.Log($"[SpellEffects-Server] Spawning effect '{spell}' at {position}");

        GameObject prefab = GetPrefabForSpell(spell);
        if (prefab == null)
        {
            Debug.LogWarning($"[SpellEffects-Server] No prefab assigned for spell '{spell}'");
            return;
        }

        // Check if it's a networked prefab
        if (prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"[SpellEffects-Server] Prefab '{spell}' has no NetworkObject - use client-side spawning instead");
            // Tell clients to spawn locally
            SpawnClientSideEffectClientRpc(spell, casterClientId, targetClientId, position, rotation);
            return;
        }

        // Instantiate on server
        GameObject instance = Instantiate(prefab, position, rotation);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn();
            ulong netId = netObj.NetworkObjectId;

            Debug.Log($"[SpellEffects-Server] Spawned networked '{spell}' with ID {netId}");

            // Track for cleanup
            activeNetworkEffects[spell] = netId;

            // Notify all clients
            NotifyEffectSpawnedClientRpc(spell, netId, casterClientId, targetClientId);
        }
    }

    /// <summary>
    /// Server notifies all clients that an effect was spawned
    /// </summary>
    [ClientRpc]
    private void NotifyEffectSpawnedClientRpc(string spell, ulong effectNetId, ulong casterClientId, ulong targetClientId)
    {
        Debug.Log($"[SpellEffects-Client] Notification: '{spell}' spawned with ID {effectNetId}");

        // Wait for the object to appear in spawn manager, then apply behaviors
        StartCoroutine(WaitForNetworkEffectAndApplyBehavior(spell, effectNetId, casterClientId, targetClientId));
    }

    /// <summary>
    /// For client-side only effects (no NetworkObject needed)
    /// </summary>
    [ClientRpc]
    private void SpawnClientSideEffectClientRpc(string spell, ulong casterClientId, ulong targetClientId, Vector3 position, Quaternion rotation)
    {
        Debug.Log($"[SpellEffects-Client] Spawning client-side effect '{spell}'");

        GameObject prefab = GetPrefabForSpell(spell);
        if (prefab == null) return;

        GameObject instance = Instantiate(prefab, position, rotation);

        // Apply behavior immediately
        GameObject caster = GetPlayerByClientId(casterClientId);
        GameObject target = GetPlayerByClientId(targetClientId);
        ApplyEffectBehavior(spell, instance, caster, target);
    }

    /// <summary>
    /// Wait for networked object to appear in spawn manager
    /// </summary>
    private IEnumerator WaitForNetworkEffectAndApplyBehavior(string spell, ulong effectNetId, ulong casterClientId, ulong targetClientId)
    {
        int attempts = 0;
        GameObject effectObj = null;

        while (attempts < 20 && effectObj == null)
        {
            effectObj = GetSpawnedObjectByNetId(effectNetId);
            if (effectObj != null) break;

            yield return new WaitForSeconds(0.05f);
            attempts++;
        }

        if (effectObj == null)
        {
            Debug.LogWarning($"[SpellEffects-Client] Failed to find effect '{spell}' with ID {effectNetId} after {attempts} attempts");
            yield break;
        }

        Debug.Log($"[SpellEffects-Client] Found effect '{spell}' after {attempts} attempts");

        // Store reference
        StoreEffectReference(spell, effectObj);

        // Apply client-side behavior
        GameObject caster = GetPlayerByClientId(casterClientId);
        GameObject target = GetPlayerByClientId(targetClientId);
        ApplyEffectBehavior(spell, effectObj, caster, target);
    }

    /// <summary>
    /// Store reference to spawned effect
    /// </summary>
    private void StoreEffectReference(string spell, GameObject effectObj)
    {
        switch (spell)
        {
            case "Ice": activeIceBlock = effectObj; break;
            case "Stone": activeStoneWall = effectObj; break;
            case "Gemini": activeGemini = effectObj; break;
            case "Mud": activeMud = effectObj; break;
            case "Pisces": activeOrbiter = effectObj; break;
            case "Tether": activeTether = effectObj; break;
            case "Jolly": activeJolly = effectObj; break;
            case "Gorbino": activeBall = effectObj; break;
            case "Flame": activeFlame = effectObj; break;
        }
    }

    /// <summary>
    /// Apply client-side behaviors to spawned effect
    /// </summary>
    private void ApplyEffectBehavior(string spell, GameObject effectObj, GameObject caster, GameObject target)
    {
        if (effectObj == null) return;

        Debug.Log($"[SpellEffects] Applying behavior for '{spell}'");

        switch (spell)
        {
            case "Ice":
                if (target != null)
                {
                    StartCoroutine(FollowTransform(effectObj, target.transform, 5f));
                    // REMOVED: Freeze is now handled by network RPCs
                }
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;

            case "Stone":
                {
                    if (effectObj == null)
                    {
                        Debug.LogWarning("[SpellEffects-Client] Stone effectObj is null!");
                        return;
                    }

                    var returner = effectObj.GetComponent<SimpleBallReturner>();
                    if (returner == null)
                    {
                        Debug.LogWarning("[SpellEffects-Client] Stone missing SimpleBallReturner!");
                        return;
                    }

                    // Assign aim target + opponent exactly like non-networked version
                    if (caster != null && caster.GetComponent<NetworkedBall>() != null)
                    {
                        returner.aimTarget = caster.GetComponent<NetworkedBall>().aimTarget.transform;
                    }

                    if (target != null)
                    {
                        returner.opponent = target.transform;
                    }

                    // Run the behaviour locally on each client
                    StartCoroutine(HandleStoneWall(effectObj, 5f));

                    // Trigger your reset logic
                    Invoke(nameof(resetSpellEffect), 5f);

                    Debug.Log("[SpellEffects-Client] Stone Wall Behavior Applied");
                }
                break;

            case "Gemini":
                if (caster != null)
                {
                    var casterMCM = caster.GetComponent<MainCharacterMovement>();
                    if (casterMCM != null)
                    {
                        // Start mimicking behavior
                        StartCoroutine(MirrorMovement(effectObj, casterMCM, 5f));
                        Debug.Log($"[SpellEffects] Gemini now mirrors {caster.name}'s movement");
                    }
                    else
                    {
                        Debug.LogWarning("[SpellEffects] Gemini: caster has no MainCharacterMovement!");
                    }
                }

                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;


            case "Pisces":
                if (caster != null)
                    StartCoroutine(HandleOrbiter(effectObj, caster.transform, 5f));
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;

            case "Tether":
                var tetherComp = effectObj.GetComponent<Tether>();
                if (tetherComp != null && target != null)
                {
                    tetherComp.Player = target.transform;
                    Debug.Log($"[SpellEffects] Tether assigned to {target.name}");
                }
                else
                {
                    Debug.LogWarning($"[SpellEffects] Tether setup failed - comp: {tetherComp != null}, target: {target != null}");
                }
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;

            case "Jolly":
                if (caster != null)
                {
                    Transform racketTransform = caster.transform.GetChild(2)?.GetChild(1)?.GetChild(0)
                        ?.GetChild(0)?.GetChild(1)?.GetChild(0)?.GetChild(0);

                    if (racketTransform != null)
                    {
                        // DO NOT parent (otherwise rotation is inherited)
                        // effectObj.transform.SetParent(racketTransform, false); // REMOVE THIS

                        // Keep Jolly's rotation and scale
                        effectObj.transform.rotation = Quaternion.Euler(0, 90, 0);
                        effectObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                        // Follow POSITION ONLY
                        StartCoroutine(FollowPositionOnly(effectObj.transform, racketTransform));

                        // Hide racket mesh
                        Transform racketMesh = caster.transform.GetChild(2)?.GetChild(0)?.GetChild(4)?.GetChild(0)?.GetChild(0);
                        if (racketMesh != null)
                            racketMesh.gameObject.SetActive(false);
                    }
                }

                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;


            case "Mud":
                // Ensure mud spawns on ground and scales up like non-networked version
                if (effectObj != null)
                {
                    // Set initial position on ground
                    Vector3 mudPos = effectObj.transform.position;
                    mudPos.y = 0.94f; // Match non-networked version
                    effectObj.transform.position = mudPos;

                    // Apply scaling animation
                    StartCoroutine(HandleMud(effectObj, 5f));

                    Debug.Log($"[SpellEffects] Mud pit spawned at {mudPos}");
                }
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;

            case "Flame":
                if (target != null)
                {
                    effectObj.transform.SetParent(target.transform);
                    effectObj.transform.localPosition = new Vector3(0, 1.65f, 0);
                }
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 3f));
                break;

            case "Gorbino":
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;
        }
    }

    /// <summary>
    /// Get prefab for a spell
    /// </summary>
    private GameObject GetPrefabForSpell(string spell)
    {
        switch (spell)
        {
            case "Ice": return iceBlockPrefab;
            case "Stone": return stoneWallPrefab;
            case "Gemini": return geminiPrefab;
            case "Mud": return mudPrefab;
            case "Pisces": return orbiterPrefab;
            case "Tether": return tetherPrefab;
            case "Jolly": return jollyPrefab;
            case "Gorbino": return BallPrefab;
            case "Flame": return flamePrefab;
            default: return null;
        }
    }

    /// <summary>
    /// Helper: Get spawned object by NetworkObjectId
    /// </summary>
    private GameObject GetSpawnedObjectByNetId(ulong netId)
    {
        if (NetworkManager.Singleton == null) return null;
        if (netId == 0 || netId == ulong.MaxValue) return null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject netObj))
            return netObj?.gameObject;

        return null;
    }

    /// <summary>
    /// Helper: Get player by client ID
    /// </summary>
    private GameObject GetPlayerByClientId(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        foreach (var kvp in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            NetworkObject netObj = kvp.Value;
            if (netObj == null) continue;

            if (netObj.OwnerClientId != clientId) continue;

            GameObject go = netObj.gameObject;

            // Must have REAL spellcasting component
            var spellcasting = go.GetComponent<NetworkedSpellcasting>();
            if (spellcasting == null) continue;

            // Reject spawners / placeholders (no movement, no collider etc)
            if (go.GetComponent<MainCharacterMovement>() == null &&
                go.GetComponent<CharacterController>() == null &&
                go.GetComponent<Rigidbody>() == null)
            {
                // This is probably PlayerSpawner � skip
                continue;
            }

            return go;
        }

        return null;
    }

    /// <summary>
    /// Request server to despawn an effect
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void DespawnEffectServerRpc(string spell, ulong effectNetId)
    {
        if (!IsServer) return;

        GameObject effectObj = GetSpawnedObjectByNetId(effectNetId);
        if (effectObj != null)
        {
            NetworkObject netObj = effectObj.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
                Debug.Log($"[SpellEffects-Server] Despawned '{spell}' effect");
            }
        }

        activeNetworkEffects.Remove(spell);
    }

    /// <summary>
    /// Despawn effect after delay (handles both networked and local)
    /// </summary>
    private IEnumerator DespawnEffectAfterDelay(string spell, GameObject effectObj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (effectObj == null) yield break;

        NetworkObject netObj = effectObj.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            // Networked object - request server to despawn
            if (IsServer)
            {
                if (netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                    Debug.Log($"[SpellEffects] Server despawned '{spell}'");
                }
            }
            else
            {
                // Client requests server to despawn
                DespawnEffectServerRpc(spell, netObj.NetworkObjectId);
                Debug.Log($"[SpellEffects] Client requested despawn for '{spell}'");
            }
        }
        else
        {
            // Local object - destroy directly
            Destroy(effectObj);
            Debug.Log($"[SpellEffects] Destroyed local '{spell}' effect");
        }

        // Clear reference
        StoreEffectReference(spell, null);
    }

    // ========== OLD INTERFACE FOR BACKWARD COMPATIBILITY ==========

    /// <summary>
    /// Register a server-spawned networked effect (legacy interface)
    /// </summary>
    public void RegisterNetworkedEffect(string spell, ulong netId)
    {
        if (netId == 0) return;

        Debug.Log($"[SpellEffects] RegisterNetworkedEffect (legacy): '{spell}' with ID {netId}");

        // Use the new system
        GameObject caster = GetPlayer();
        GameObject target = GetOpponent();

        ulong casterClientId = caster?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
        ulong targetClientId = target?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

        StartCoroutine(WaitForNetworkEffectAndApplyBehavior(spell, netId, casterClientId, targetClientId));
    }

    // ========== MAIN SPELL CASTING ==========

    public void castSpell()
    {
        // Check for valid spell name FIRST
        if (string.IsNullOrEmpty(spellName))
        {
            Debug.LogError("[SpellEffects] castSpell called with no spell name!");
            networkSpellActive = false;  // Clear the lock
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

        // Get client IDs for network spawning
        ulong casterClientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
        ulong targetClientId = opponent?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

        bool isLocalCaster = (player != null && NetworkManager.Singleton.LocalClientId == casterClientId);

        Debug.Log($"[SpellEffects] Casting {spellName} - isLocalCaster: {isLocalCaster}");

        switch (spellName)
        {
            case "Lightning":
                if (isLocalCaster)
                {
                    var mcm = player.GetComponent<MainCharacterMovement>();
                    if (mcm != null)
                    {
                        if (activeLightningCoroutine != null)
                            StopCoroutine(activeLightningCoroutine);

                        activeLightningCoroutine = StartCoroutine(ApplyLightningSpeed(mcm, 5f));
                        Debug.Log("[SpellEffects] Lightning speed boost applied");

                        // Activate lightning trail for this player
                        ActivateLightningTrail(player, true);
                    }
                }
                break;

            case "Ice":
                if (isLocalCaster && opponent != null && iceBlockPrefab != null)
                {
                    // Spawn ice block on opponent
                    SpawnEffectServerRpc("Ice", casterClientId, targetClientId,
                        opponent.transform.position, opponent.transform.rotation);

                    // Freeze opponent across all clients
                    FreezePlayerServerRpc(targetClientId, 5f);

                    Debug.Log($"[SpellEffects] Ice spawned and freeze requested for opponent");

                    // Force a reset across all clients
                    ScheduleReset(5f);
                }
                break;

            case "Fireball":
                Debug.Log($"[SpellEffects-castSpell] === FIREBALL CAST START ===");
                Debug.Log($"[SpellEffects-castSpell] LocalClientId: {NetworkManager.Singleton.LocalClientId}");
                Debug.Log($"[SpellEffects-castSpell] isLocalCaster: {isLocalCaster}");

                if (isLocalCaster)
                {
                    // Set locally
                    resetOnOppHit = true;
                    resetOnPlrHit = false;

                    // Tell server to arm Fireball
                    if (!IsServer)
                    {
                        Debug.Log("[SpellEffects-castSpell] Client telling server to arm Fireball");
                        SetSpellArmedServerRpc(true, false);
                    }
                    else
                    {
                        Debug.Log("[SpellEffects-castSpell] Already on server, Fireball armed locally");
                    }

                    GameObject fireballBallObj = GameObject.FindWithTag("Ball");
                    if (fireballBallObj != null)
                    {
                        CollisionTrackerBall tracker = fireballBallObj.GetComponent<CollisionTrackerBall>();
                        if (tracker != null)
                        {
                            tracker.LastHitWizard = "Player";
                            Debug.Log("[SpellEffects-castSpell] Marked ball tracker LastHitWizard = Player");
                        }
                    }

                    Debug.Log($"[SpellEffects-castSpell] Fireball armed - resetOnOppHit: {resetOnOppHit}");
                }
                Debug.Log($"[SpellEffects-castSpell] === FIREBALL CAST END ===");
                break;

            case "Shadow":
                if (isLocalCaster && opponent != null)
                {
                    Debug.Log($"[SpellEffects] Casting Shadow - making {player.name} invisible to {opponent.name}");

                    // Apply Shadow invisibility
                    ApplyShadowInvisibilityServerRpc(casterClientId, targetClientId);

                    // Schedule automatic reset after 10 seconds (time-based only)
                    ScheduleReset(10f);

                    Debug.Log("[SpellEffects] Shadow will auto-reset in 10 seconds");
                }
                else if (isLocalCaster)
                {
                    Debug.LogWarning("[SpellEffects] Shadow cast failed - no opponent found");
                }
                break;

            case "Mud":
                if (isLocalCaster)
                {
                    // Set locally - arm Mud to trigger on opponent hit
                    resetOnOppHit = true;
                    resetOnBounce = false; // Mud triggers on hit, not bounce

                    // Tell server to arm Mud
                    if (!IsServer)
                    {
                        SetSpellArmedServerRpc(true, false);
                    }

                    Debug.Log("[SpellEffects] Mud spell armed - will spawn pit when opponent hits ball");
                }
                break;

            case "Green":
                if (isLocalCaster)
                {
                    var greenBall = player.GetComponent<NetworkedBall>();
                    if (greenBall != null)
                    {
                        greenBall.green = true;

                        if (IsOwner)
                        {
                            var uiManager = player.GetComponent<NetworkedUIManager>();
                            if (uiManager != null)
                            {
                                uiManager.UpdateSpellStatus("Green Active", Color.green);
                            }
                        }

                        Debug.Log("[SpellEffects] Green spell activated");
                    }

                    // Green lasts 10 seconds
                    ScheduleReset(10f);
                }
                break;

            case "Stone":
                if (isLocalCaster && player != null && stoneWallPrefab != null)
                {
                    Vector3 spawnPos = player.transform.position + player.transform.forward * 2f;
                    SpawnEffectServerRpc("Stone", casterClientId, targetClientId, spawnPos, Quaternion.identity);
                }
                break;

            case "Chronos":
                if (isLocalCaster)
                {
                    StartCoroutine(ApplyChronosAfterExplanation());
                }
                break;

            case "Gemini":
                if (isLocalCaster && player != null && geminiPrefab != null)
                {
                    Vector3 gemPos = player.transform.position;
                    gemPos.x = -player.transform.position.x;
                    SpawnEffectServerRpc("Gemini", casterClientId, targetClientId, gemPos, player.transform.rotation);
                }
                ScheduleReset(5f);
                break;

            case "Blink":
                if (isLocalCaster)
                {
                    var blinkMCM = player.GetComponent<MainCharacterMovement>();
                    if (blinkMCM != null && blinkMCM.controller != null)
                    {
                        Vector3 velocity = blinkMCM.controller.velocity;
                        velocity.y = 0;

                        if (velocity.magnitude > 0.1f)
                            storedBlinkDirection = velocity.normalized * 3f;
                        else
                        {
                            storedBlinkDirection = player.transform.forward * 3f;
                            storedBlinkDirection.y = 0;
                        }
                    }
                    else
                    {
                        storedBlinkDirection = player.transform.forward * 3f;
                        storedBlinkDirection.y = 0;
                    }

                    PerformBlink(player);
                }
                ScheduleReset(5f);
                break;

            case "Jolly":
                if (isLocalCaster && player != null && jollyPrefab != null)
                {
                    // Expand player collider
                    var capsule = player.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        capsule.radius = 2;
                        Debug.Log("[SpellEffects] Jolly - expanded collider");
                    }

                    // Find racket transform - use more reliable path
                    Transform racketTransform = null;

                    // Try to find by name (more reliable)
                    Transform[] allChildren = player.GetComponentsInChildren<Transform>();
                    foreach (Transform child in allChildren)
                    {
                        if (child.name.Contains("Racket") || child.name.Contains("racket"))
                        {
                            racketTransform = child;
                            Debug.Log($"[SpellEffects] Found racket: {child.name}");
                            break;
                        }
                    }

                    // Fallback to hierarchy path if name search fails
                    if (racketTransform == null)
                    {
                        Transform body = player.transform.Find("Body");
                        if (body != null)
                        {
                            // Navigate down to racket
                            racketTransform = body.Find("Armature")?.Find("Hips")?.Find("Spine")?.Find("Spine.001")
                                ?.Find("Shoulder.R")?.Find("Upper_Arm.R")?.Find("Forearm.R")?.Find("Hand.R");

                            if (racketTransform != null)
                                Debug.Log($"[SpellEffects] Found racket via hierarchy at {racketTransform.position}");
                        }
                    }

                    if (racketTransform != null)
                    {
                        // Spawn at racket position
                        SpawnEffectServerRpc("Jolly", casterClientId, targetClientId, racketTransform.position, Quaternion.identity);
                        Debug.Log($"[SpellEffects] Jolly spawn requested at {racketTransform.position}");
                    }
                    else
                    {
                        Debug.LogWarning("[SpellEffects] Jolly - could not find racket transform!");
                        // Fallback: spawn at player position
                        SpawnEffectServerRpc("Jolly", casterClientId, targetClientId, player.transform.position, Quaternion.identity);
                    }
                }
                ScheduleReset(5f);
                break;

            case "Warp":
                ScheduleReset(0.5f);
                break;

            case "Pisces":
                if (isLocalCaster && player != null && orbiterPrefab != null)
                {
                    SpawnEffectServerRpc("Pisces", casterClientId, targetClientId,
                        player.transform.position, player.transform.rotation);
                }
                break;

            case "Tether":
                if (isLocalCaster && opponent != null && tetherPrefab != null)
                {
                    Vector3 tetherPos = opponent.transform.position;
                    tetherPos.y = 1.45f;

                    SpawnEffectServerRpc("Tether", casterClientId, targetClientId, tetherPos, Quaternion.identity);
                    Debug.Log($"[SpellEffects] Tether spawn requested at {tetherPos}");
                }
                else if (isLocalCaster)
                {
                    Debug.LogWarning($"[SpellEffects] Tether spawn failed");
                }
                ScheduleReset(5f);
                break;

            case "Gorbino":
                if (isLocalCaster)
                {
                    Gorbino = GameObject.Find("Gorbino");
                    if (Gorbino != null && BallPrefab != null)
                    {
                        SpawnEffectServerRpc("Gorbino", casterClientId, targetClientId,
                            Gorbino.transform.position, Quaternion.identity);
                        Gorbino.SetActive(false);
                    }
                }
                ScheduleReset(5f);
                break;

                // The Gambit case WAS here, but I've removed it.
                // The system now handles Gambit by catching the Spell name in the Cast routine in NetworkedSpellEffects.cs and changes that value randomly based on the Gambit-Available spells

                // Visual casting is already handled by NetworkedSpellcasting.CastSpellServerRpc
                // No need to call CastSpellNormal here - it would cause duplicate visual spawning
                // The ball visual is managed by NetworkedSpellcasting, not SpellEffects
        }
    }

    private void PerformBlink(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("[SpellEffects] Blink failed - no player");
            return;
        }

        // Use the stored direction
        Vector3 blinkDirection = storedBlinkDirection;

        if (blinkDirection.magnitude < 0.1f)
        {
            // Fallback - shouldn't happen, but just in case
            blinkDirection = player.transform.forward * 3f;
            blinkDirection.y = 0;
        }

        // Calculate new position
        Vector3 newPos = player.transform.position + blinkDirection;

        // Clamp to court boundaries
        if (player.transform.position.z > 0)
        {
            // Front court (player side)
            newPos.z = Mathf.Clamp(newPos.z, 0.5f, 10.9f);
        }
        else
        {
            // Back court (opponent side)
            newPos.z = Mathf.Clamp(newPos.z, -11.5f, -1f);
        }

        newPos.x = Mathf.Clamp(newPos.x, -4.9f, 4.9f);
        newPos.y = player.transform.position.y; // Keep same height

        Debug.Log($"[SpellEffects] Blink: {player.transform.position} -> {newPos} (direction: {blinkDirection})");

        // Disable components temporarily
        var movement = player.GetComponent<MainCharacterMovement>();
        var controller = player.GetComponent<CharacterController>();

        if (movement != null) movement.enabled = false;
        if (controller != null) controller.enabled = false;

        // Teleport
        player.transform.position = newPos;

        // Re-enable components
        if (controller != null) controller.enabled = true;
        if (movement != null)
        {
            movement.enabled = true;
            movement.ForceMovementRefresh(); // Reset velocity/grounding
        }

        Debug.Log($"[SpellEffects] Blink completed to {player.transform.position}");

        // Clear stored direction
        storedBlinkDirection = Vector3.zero;
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
                // Stop the coroutine if it's still running
                if (activeLightningCoroutine != null)
                {
                    StopCoroutine(activeLightningCoroutine);
                    activeLightningCoroutine = null;
                }

                // Restore speed
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null)
                {
                    mcm.speed = 7;
                    Debug.Log("[SpellEffects] Lightning reset: Speed restored to 7");
                }

                // Deactivate lightning trail for this player
                Debug.Log($"[SpellEffects] Attempting to deactivate lightning trail for {player?.name}");
                ActivateLightningTrail(player, false);
                break;

            case "Green":
                var greenBall = player.GetComponent<NetworkedBall>();
                if (greenBall != null)
                {
                    greenBall.green = false;
                    Debug.Log("[SpellEffects] Green deactivated");
                }
                break;

            case "Ice":
                // Clear freeze tracking
                if (opponent != null)
                {
                    ulong oppClientId = opponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    if (oppClientId != ulong.MaxValue && activeFreezeCoroutines.ContainsKey(oppClientId))
                    {
                        if (activeFreezeCoroutines[oppClientId] != null)
                        {
                            StopCoroutine(activeFreezeCoroutines[oppClientId]);
                        }
                        activeFreezeCoroutines.Remove(oppClientId);

                        // Force restore movement
                        var oppMcm = opponent.GetComponent<MainCharacterMovement>();
                        if (oppMcm != null && oppMcm.speed == 0f)
                        {
                            oppMcm.speed = 7;
                            Debug.Log($"[SpellEffects] Ice Reset: Force restored opponent speed");
                        }
                    }
                }
                activeIceBlock = null;
                break;

            case "Fireball":
                // Clear locally
                resetOnOppHit = false;

                // Tell server to disarm
                if (!IsServer)
                {
                    SetSpellArmedServerRpc(false, false);
                }

                // Spawn flame effect on the victim (currentOpponent was set by OnPlayerHitBall)
                if (flamePrefab != null && currentOpponent != null)
                {
                    ulong localClientId = NetworkManager.Singleton.LocalClientId;
                    GameObject casterPlayer = currentPlayer ?? GetPlayer();
                    ulong playerClientId = casterPlayer?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    // Only caster spawns the flame
                    if (localClientId == playerClientId)
                    {
                        ulong targetClientId = currentOpponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                        SpawnEffectServerRpc("Flame", playerClientId, targetClientId,
                            currentOpponent.transform.position, Quaternion.identity);

                        Debug.Log($"[SpellEffects] Fireball reset - spawning flame on {currentOpponent.name}");
                    }
                }
                break;

            case "Shadow":
                Debug.Log($"[SpellEffects-Reset] === SHADOW RESET START ===");
                Debug.Log($"[SpellEffects-Reset] player: {player?.name}, opponent: {opponent?.name}");
                Debug.Log($"[SpellEffects-Reset] IsServer: {IsServer}");

                // Remove Shadow invisibility via server
                if (player != null && opponent != null)
                {
                    ulong casterClientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    ulong opponentClientId = opponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    Debug.Log($"[SpellEffects-Reset] Calling RemoveShadowInvisibilityServerRpc - caster: {casterClientId}, opponent: {opponentClientId}");

                    if (casterClientId != ulong.MaxValue && opponentClientId != ulong.MaxValue)
                    {
                        RemoveShadowInvisibilityServerRpc(casterClientId, opponentClientId);
                    }
                    else
                    {
                        Debug.LogError($"[SpellEffects-Reset] Invalid client IDs - caster: {casterClientId}, opponent: {opponentClientId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[SpellEffects-Reset] Cannot remove Shadow via server - player or opponent is null!");

                    // Emergency cleanup - try to restore materials locally
                    Debug.Log($"[SpellEffects-Reset] Attempting emergency local cleanup");

                    if (shadowOriginalMaterials.Count > 0)
                    {
                        Debug.Log($"[SpellEffects-Reset] Emergency local cleanup - restoring {shadowOriginalMaterials.Count} materials");
                        foreach (var kvp in shadowOriginalMaterials)
                        {
                            if (kvp.Key != null && kvp.Value != null)
                            {
                                kvp.Key.material = kvp.Value;
                            }
                        }
                        shadowOriginalMaterials.Clear();
                        currentShadowCasterId = ulong.MaxValue;
                    }

                    if (shadowCasterOriginalMaterials.Count > 0)
                    {
                        Debug.Log($"[SpellEffects-Reset] Emergency local cleanup - restoring {shadowCasterOriginalMaterials.Count} caster materials");
                        foreach (var kvp in shadowCasterOriginalMaterials)
                        {
                            if (kvp.Key != null && kvp.Value != null)
                            {
                                kvp.Key.material = kvp.Value;
                            }
                        }
                        shadowCasterOriginalMaterials.Clear();
                        currentShadowCasterFeedbackId = ulong.MaxValue;
                    }
                }

                // Clean up any local references
                opponentRenderer = null;
                originalOpponentMaterial = null;

                Debug.Log($"[SpellEffects-Reset] === SHADOW RESET END ===");
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
                // Despawn handled by DespawnEffectAfterDelay
                activeGemini = null;
                break;

            case "Jolly":
                var capsule = player.GetComponent<CapsuleCollider>();
                if (capsule != null) capsule.radius = 1;

                Transform racketMesh = player.transform.GetChild(2)?.GetChild(0)?.GetChild(4)?.GetChild(0)?.GetChild(0);
                if (racketMesh != null)
                    racketMesh.gameObject.SetActive(true);

                // Despawn handled by DespawnEffectAfterDelay
                activeJolly = null;
                break;

            case "Mud":
                Debug.Log($"[SpellEffects-Reset] === MUD RESET START ===");

                // Clear locally
                resetOnOppHit = false;
                resetOnBounce = false;

                // Tell server to disarm
                if (!IsServer)
                {
                    SetSpellArmedServerRpc(false, false);
                }

                Debug.Log($"[SpellEffects-Reset] Mud disarmed");
                Debug.Log($"[SpellEffects-Reset] === MUD RESET END ===");
                break;

            case "Warp":
                {
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
                    GameObject warpBall = GameObject.FindWithTag("Ball");

                    if (warpBall != null)
                    {
                        Rigidbody ballRb = warpBall.GetComponent<Rigidbody>();

                        Vector3 savedVelocity = Vector3.zero;
                        Vector3 savedAngularVelocity = Vector3.zero;
                        bool hadGravity = true;

                        if (ballRb != null)
                        {
                            // Cache momentum
                            savedVelocity = ballRb.linearVelocity;
                            savedAngularVelocity = ballRb.angularVelocity;
                            hadGravity = ballRb.useGravity;

                            // Temporarily stabilize
                            ballRb.useGravity = false;
                            ballRb.isKinematic = true;
                        }

                        // Warp position only
                        Vector3 ballPos = warpBall.transform.position;
                        ballPos.x = ballX;
                        warpBall.transform.position = ballPos;

                        Debug.Log($"[SpellEffects] Warped ball to x={ballX}");

                        if (ballRb != null)
                        {
                            // Restore physics & momentum
                            ballRb.isKinematic = false;
                            ballRb.useGravity = hadGravity;
                            ballRb.linearVelocity = savedVelocity;
                            ballRb.angularVelocity = savedAngularVelocity;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[SpellEffects] Warp failed - ball not found");
                    }

                    break;
                }

            case "Pisces":
                // Despawn handled by DespawnEffectAfterDelay
                activeOrbiter = null;
                break;

            case "Tether":
                // Despawn handled by DespawnEffectAfterDelay
                activeTether = null;
                break;

            case "Gorbino":
                // Despawn handled by DespawnEffectAfterDelay
                activeBall = null;
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

    // ========== EFFECT BEHAVIORS ==========

    private IEnumerator FreezeOpponentMovement(GameObject target, float duration, ulong targetClientId)
    {
        if (target == null)
        {
            Debug.LogWarning($"[SpellEffects] FreezeOpponentMovement: target is null");
            yield break;
        }

        var mcm = target.GetComponent<MainCharacterMovement>();
        if (mcm == null)
        {
            Debug.LogWarning($"[SpellEffects] FreezeOpponentMovement: {target.name} has no MainCharacterMovement");
            yield break;
        }

        float originalMcmSpeed = mcm.speed;

        Debug.Log($"[SpellEffects] Freezing {target.name} - original speed: {originalMcmSpeed}");
        mcm.speed = 0f;

        yield return new WaitForSeconds(duration);

        // CRITICAL: Always restore movement, even if component became null
        if (target != null && mcm != null)
        {
            mcm.speed = originalMcmSpeed;
            Debug.Log($"[SpellEffects] RESTORED {target.name} movement to {originalMcmSpeed}");
        }
        else
        {
            Debug.LogWarning($"[SpellEffects] Could not restore {target?.name ?? "null"} movement - object or component destroyed");
        }

        // Remove from active dictionary
        if (activeFreezeCoroutines.ContainsKey(targetClientId))
        {
            activeFreezeCoroutines.Remove(targetClientId);
            Debug.Log($"[SpellEffects] Removed freeze tracking for client {targetClientId}");
        }
    }

    // Helper function for Jolly
    private IEnumerator FollowPositionOnly(Transform obj, Transform target)
    {
        while (obj != null && target != null)
        {
            obj.position = target.position;  // follow position
                                             // ignore target.rotation completely
            yield return null;
        }
    }

    private IEnumerator ApplyLightningSpeed(MainCharacterMovement mcm, float duration)
    {
        if (mcm == null)
        {
            Debug.LogWarning("[SpellEffects] Lightning: MCM is null");
            yield break;
        }

        float originalSpeed = mcm.speed;
        float boostedSpeed;

        if (spellName == "Chronos")
        {
            boostedSpeed = 70f;
        }
        else
        {
            boostedSpeed = 17f;
        }

            Debug.Log($"[SpellEffects] Lightning: Setting speed from {originalSpeed} to {boostedSpeed}");
        mcm.speed = boostedSpeed;

        float elapsed = 0f;

        // Continuously enforce the speed for the duration
        while (elapsed < duration)
        {
            if (mcm == null) yield break;

            // Keep enforcing the boosted speed
            if (mcm.speed != boostedSpeed)
            {
                Debug.Log($"[SpellEffects] Lightning: Speed changed to {mcm.speed}, re-applying boost");
                mcm.speed = boostedSpeed;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Restore original speed
        if (mcm != null)
        {
            mcm.speed = originalSpeed;
            Debug.Log($"[SpellEffects] Lightning: Restored speed to {originalSpeed}");
        }

        activeLightningCoroutine = null;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyFireballKnockbackServerRpc(ulong victimClientId, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[SpellEffects-ServerRpc] === ApplyFireballKnockbackServerRpc START ===");
        Debug.Log($"[SpellEffects-ServerRpc] IsServer: {IsServer}");
        Debug.Log($"[SpellEffects-ServerRpc] victimClientId: {victimClientId}");

        if (!IsServer)
        {
            Debug.LogWarning("[SpellEffects-ServerRpc] EXIT - Not server!");
            return;
        }

        // Use context: currentPlayer = caster, currentOpponent = victim
        GameObject caster = currentPlayer;
        GameObject victim = GetPlayerByClientId(victimClientId);

        Debug.Log($"[SpellEffects-ServerRpc] caster (from context): {caster?.name ?? "NULL"}");
        Debug.Log($"[SpellEffects-ServerRpc] victim (from GetPlayerByClientId): {victim?.name ?? "NULL"}");

        if (victim == null)
        {
            Debug.LogError($"[SpellEffects-ServerRpc] ? FAILED - victim not found for client {victimClientId}");
            return;
        }

        // Check for required components
        var controller = victim.GetComponent<CharacterController>();
        var movement = victim.GetComponent<MainCharacterMovement>();

        Debug.Log($"[SpellEffects-ServerRpc] Victim components - CharacterController: {controller != null}, MainCharacterMovement: {movement != null}");

        if (controller == null)
        {
            Debug.LogError($"[SpellEffects-ServerRpc] ? FAILED - victim {victim.name} has no CharacterController!");
            return;
        }

        Debug.Log($"[SpellEffects-ServerRpc] ? Applying Fireball knockback - caster: {caster?.name ?? "NULL"}, victim: {victim.name}");

        // Calculate knockback direction
        Vector3 knockbackDirection = Vector3.back; // Default fallback

        if (caster != null)
        {
            Vector3 directionFromCaster = (victim.transform.position - caster.transform.position).normalized;
            Vector3 horizontal = new Vector3(directionFromCaster.x, 0, directionFromCaster.z).normalized;
            Vector3 upward = Vector3.up * 0.75f;
            knockbackDirection = (horizontal + upward).normalized;

            Debug.Log($"[SpellEffects-ServerRpc] Calculated direction from caster: {knockbackDirection}");
        }
        else
        {
            Vector3 awayFromCenter = (victim.transform.position - Vector3.zero).normalized;
            knockbackDirection = (new Vector3(awayFromCenter.x, 0, awayFromCenter.z) + Vector3.up * 0.75f).normalized;

            Debug.Log($"[SpellEffects-ServerRpc] Using fallback direction (no caster): {knockbackDirection}");
        }

        Vector3 knockbackForce = knockbackDirection * fireballForceStrength;
        float duration = 0.45f;

        Debug.Log($"[SpellEffects-ServerRpc] knockbackForce: {knockbackForce}, duration: {duration}");

        Debug.Log($"[SpellEffects-ServerRpc] Broadcasting ApplyFireballKnockbackClientRpc to all clients");
        ApplyFireballKnockbackClientRpc(victimClientId, knockbackDirection, fireballForceStrength, duration);

        // Reset spell AFTER knockback completes (add small buffer)
        Debug.Log($"[SpellEffects-ServerRpc] Scheduling resetSpellEffect in {duration + 0.2f}s");
        Invoke(nameof(resetSpellEffect), duration + 0.2f);

        Debug.Log($"[SpellEffects-ServerRpc] === ApplyFireballKnockbackServerRpc END ===");
    }

    /// <summary>
    /// Applies knockback and guarantees input restoration
    /// </summary>
    private IEnumerator ApplyKnockbackWithRestore(GameObject target, Vector3 knockbackForce, float duration)
    {
        if (target == null) yield break;

        var controller = target.GetComponent<CharacterController>();
        var movement = target.GetComponent<MainCharacterMovement>();

        if (controller == null)
        {
            Debug.LogError($"[ApplyKnockbackWithRestore] No CharacterController on {target.name}");
            yield break;
        }

        // Disable input at START
        bool wasInputDisabled = false;
        if (movement != null)
        {
            wasInputDisabled = movement.inputDisabled;
            movement.inputDisabled = true;
            Debug.Log($"[ApplyKnockbackWithRestore] Disabled input on {target.name} (was: {wasInputDisabled})");
        }

        // Apply the knockback
        yield return StartCoroutine(ApplyKnockback(target, knockbackForce, duration));

        // GUARANTEED restore at END
        if (movement != null)
        {
            // CRITICAL: Clear the moveDirection before re-enabling
            movement.ClearMoveDirection();

            movement.inputDisabled = wasInputDisabled; // Restore original state
            Debug.Log($"[ApplyKnockbackWithRestore] ? Re-enabled input for {target.name}");
        }
    }

    /// <summary>
    /// Client applies knockback for smooth local visuals
    /// </summary>
    [ClientRpc]
    private void ApplyFireballKnockbackClientRpc(ulong victimClientId, Vector3 knockbackDirection, float forceStrength, float duration)
    {
        GameObject victim = GetPlayerByClientId(victimClientId);
        if (victim == null) return;

        Debug.Log($"[SpellEffects-Client] Applying Fireball knockback to {victim.name}");

        Vector3 knockbackForce = knockbackDirection * forceStrength;

        // Use the new method that guarantees restoration
        StartCoroutine(ApplyKnockbackWithRestore(victim, knockbackForce, duration));
    }

    // Keep the old method for backward compatibility but mark it as obsolete
    [System.Obsolete("Use ApplyFireballKnockbackServerRpc instead")]
    public void ApplyFireballKnockback(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[SpellEffects] ApplyFireballKnockback: target is null");
            return;
        }

        // If we're on the server, we can call it directly
        if (IsServer)
        {
            ulong victimClientId = target.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
            ApplyFireballKnockbackServerRpc(victimClientId);
        }
        else
        {
            Debug.LogWarning("[SpellEffects] ApplyFireballKnockback called on client - use ApplyFireballKnockbackServerRpc instead");
        }
    }

    private IEnumerator ApplyKnockback(GameObject target, Vector3 knockbackForce, float duration)
    {
        Debug.Log($"[ApplyKnockback] === START === target: {target?.name}, IsServer: {IsServer}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        if (target == null)
        {
            Debug.LogError($"[ApplyKnockback] ? Target is NULL!");
            yield break;
        }

        var controller = target.GetComponent<CharacterController>();
        var movement = target.GetComponent<MainCharacterMovement>();
        var netObj = target.GetComponent<NetworkObject>();

        Debug.Log($"[ApplyKnockback] Target: {target.name}");
        Debug.Log($"[ApplyKnockback] - CharacterController: {controller != null}");
        Debug.Log($"[ApplyKnockback] - MainCharacterMovement: {movement != null}");
        Debug.Log($"[ApplyKnockback] - NetworkObject: {netObj != null}");
        Debug.Log($"[ApplyKnockback] - NetworkObject.OwnerClientId: {netObj?.OwnerClientId}");
        Debug.Log($"[ApplyKnockback] - Initial Position: {target.transform.position}");

        if (controller == null)
        {
            Debug.LogError($"[ApplyKnockback] ? FAILED - No CharacterController on {target.name}");
            yield break;
        }

        Debug.Log($"[ApplyKnockback] CharacterController properties:");
        Debug.Log($"  - enabled: {controller.enabled}");
        Debug.Log($"  - isGrounded: {controller.isGrounded}");
        Debug.Log($"  - detectCollisions: {controller.detectCollisions}");
        Debug.Log($"  - height: {controller.height}");
        Debug.Log($"  - radius: {controller.radius}");

        bool wasMovementEnabled = true;
        if (movement != null)
        {
            wasMovementEnabled = movement.enabled;
            movement.enabled = false;
            Debug.Log($"[ApplyKnockback] Disabled MainCharacterMovement (was: {wasMovementEnabled})");
        }
        else
        {
            Debug.LogWarning($"[ApplyKnockback] No MainCharacterMovement component found on {target.name}");
        }

        float elapsed = 0f;
        Vector3 startPosition = target.transform.position;
        float totalDistance = 0f;

        // Track vertical velocity for gravity
        float verticalVelocity = knockbackForce.y;
        float gravity = 25f; // Match your MainCharacterMovement gravity value

        Debug.Log($"[ApplyKnockback] Starting knockback loop:");
        Debug.Log($"  - knockbackForce: {knockbackForce}");
        Debug.Log($"  - duration: {duration}");
        Debug.Log($"  - force magnitude: {knockbackForce.magnitude}");
        Debug.Log($"  - initial vertical velocity: {verticalVelocity}");

        int frameCount = 0;
        while (elapsed < duration)
        {
            if (target == null)
            {
                Debug.LogError("[ApplyKnockback] Target became null during knockback!");
                yield break;
            }

            if (controller == null)
            {
                Debug.LogError("[ApplyKnockback] CharacterController became null during knockback!");
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration);

            // Calculate horizontal knockback (decreases over time)
            Vector3 horizontalKnockback = new Vector3(knockbackForce.x, 0, knockbackForce.z) * t * Time.deltaTime;

            // Apply gravity to vertical velocity
            if (!controller.isGrounded)
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity, -2f); // Small downward force when grounded
            }

            // Combine horizontal and vertical movement
            Vector3 frameMovement = horizontalKnockback + new Vector3(0, verticalVelocity * Time.deltaTime, 0);

            Vector3 positionBefore = target.transform.position;

            // Try to move
            CollisionFlags flags = controller.Move(frameMovement);

            // If we hit something above or below, stop vertical velocity
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0)
            {
                verticalVelocity = 0;
            }
            if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0)
            {
                verticalVelocity = 0;
            }

            Vector3 positionAfter = target.transform.position;
            float frameMoveDistance = Vector3.Distance(positionBefore, positionAfter);
            totalDistance += frameMoveDistance;

            // Log every 10 frames to avoid spam
            if (frameCount % 10 == 0 || frameCount < 5)
            {
                Debug.Log($"[ApplyKnockback] Frame {frameCount}: t={t:F3}, elapsed={elapsed:F3}");
                Debug.Log($"  - frameMovement: {frameMovement}");
                Debug.Log($"  - verticalVelocity: {verticalVelocity:F3}");
                Debug.Log($"  - position before: {positionBefore}");
                Debug.Log($"  - position after: {positionAfter}");
                Debug.Log($"  - moved: {frameMoveDistance:F3}m");
                Debug.Log($"  - collision flags: {flags}");
                Debug.Log($"  - controller.isGrounded: {controller.isGrounded}");
            }

            frameCount++;
            yield return null;
        }

        Vector3 endPosition = target.transform.position;
        Vector3 totalMovement = endPosition - startPosition;

        Debug.Log($"[ApplyKnockback] === KNOCKBACK COMPLETE ===");
        Debug.Log($"  - Total frames: {frameCount}");
        Debug.Log($"  - Start position: {startPosition}");
        Debug.Log($"  - End position: {endPosition}");
        Debug.Log($"  - Total movement: {totalMovement}");
        Debug.Log($"  - Total distance: {totalDistance:F3}m");
        Debug.Log($"  - Expected movement: ~{(knockbackForce.magnitude * duration):F3}m");

        if (totalDistance < 0.1f)
        {
            Debug.LogError($"[ApplyKnockback] ??? KNOCKBACK FAILED - Player barely moved ({totalDistance:F3}m)!");
            Debug.LogError($"[ApplyKnockback] Possible causes:");
            Debug.LogError($"  1. CharacterController is disabled or constrained");
            Debug.LogError($"  2. Player is colliding with something immovable");
            Debug.LogError($"  3. Another script is overriding position");
            Debug.LogError($"  4. Network ownership issue");
        }
        else
        {
            Debug.Log($"[ApplyKnockback] ? Knockback successful - moved {totalDistance:F3}m");
        }

        if (movement != null)
        {
            movement.enabled = wasMovementEnabled;
            Debug.Log($"[ApplyKnockback] Re-enabled MainCharacterMovement");
        }

        Debug.Log($"[ApplyKnockback] === END ===");
    }

    private IEnumerator ApplyChronosAfterExplanation()
    {
        GameObject player = GetPlayer();
        if (player == null)
        {
            Debug.LogError("[SpellEffects] Chronos failed - no player found");
            yield break;
        }
        var mcm = player.GetComponent<MainCharacterMovement>();

        // Wait for any existing slowdown to finish
        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);
        yield return new WaitForSecondsRealtime(0.05f);

        lastOriginalTimeScale = Time.timeScale;
        isSpellSlowdownActive = true;

        // Apply time scale
        Time.timeScale = 0.1f;
        Debug.Log($"[SpellEffects] Chronos applied - timeScale: {Time.timeScale}");

        // Stealing the Lightning Coroutine to Alter the Caster's speed
        if (activeLightningCoroutine != null)
            StopCoroutine(activeLightningCoroutine);

        activeLightningCoroutine = StartCoroutine(ApplyLightningSpeed(mcm, 0.4f));

        // Show explanation if enabled
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

        // Restore time scale
        Time.timeScale = lastOriginalTimeScale;
        isSpellSlowdownActive = false;
        Debug.Log($"[SpellEffects] Chronos ended - timeScale restored: {Time.timeScale}");

        // Reset movement
        if (mcm != null)
        {
            mcm.speed = 7f;
            mcm.gravity = 25f;
        }

        Invoke(nameof(resetSpellEffect), 0.1f);
    }

    /// <summary>
    /// Tell the server to arm Fireball (or other on-hit spells)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void SetSpellArmedServerRpc(bool oppHit, bool plrHit, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        resetOnOppHit = oppHit;
        resetOnPlrHit = plrHit;

        Debug.Log($"[SpellEffects-Server] Spell armed - resetOnOppHit: {resetOnOppHit}, resetOnPlrHit: {resetOnPlrHit}");
    }

    private IEnumerator ShowSpellExplanation(string spell)
    {
        if (spellExplanationUI == null || spellExplanationText == null)
            yield break;

        spellExplanationText.text = spell;
        spellExplanationUI.SetActive(true);

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
            if (go == null || target == null) yield break;
            go.transform.position = target.position;
            go.transform.rotation = target.rotation;
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator HandleStoneWall(GameObject wall, float duration)
    {
        if (wall == null) yield break;

        float t = 0f;
        Vector3 startScale = wall.transform.localScale;
        Vector3 targetScale = startScale;
        wall.transform.localScale = Vector3.zero;

        while (t < 0.25f)
        {
            t += Time.deltaTime;
            if (wall == null) yield break;
            wall.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t / 0.25f);
            yield return null;
        }

        yield return new WaitForSeconds(duration);

        float s = 0f;
        while (s < 0.25f)
        {
            s += Time.deltaTime;
            if (wall == null) yield break;
            wall.transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, s / 0.25f);
            yield return null;
        }

        // Despawn is handled by DespawnEffectAfterDelay
    }

    private IEnumerator HandleOrbiter(GameObject orbiter, Transform center, float duration)
    {
        if (orbiter == null || center == null)
        {
            Debug.LogWarning("[SpellEffects] Orbiter setup failed - null reference");
            yield break;
        }

        Debug.Log($"[SpellEffects] Orbiter following {center.name} for {duration}s");

        float elapsed = 0f;
        float spinSpeed = -180f; // degrees per second

        while (elapsed < duration)
        {
            if (orbiter == null || center == null) yield break;

            // Follow caster
            orbiter.transform.position = center.position;

            // Spin on its own local Y axis
            orbiter.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[SpellEffects] Orbiter duration complete");
    }

    public void DeleteIceBlock()
    {
        if (activeIceBlock != null)
        {
            NetworkObject netObj = activeIceBlock.GetComponent<NetworkObject>();
            if (netObj != null && IsServer)
            {
                netObj.Despawn(true);
            }
            activeIceBlock = null;
        }
    }

    /// <summary>
    /// Called when a player hits the ball - checks if spell should trigger
    /// Server-side only (called by NetworkedCollisionTrackerBall on server)
    /// </summary>
    public void OnPlayerHitBall(GameObject hitter, GameObject ballOwner)
    {
        Debug.Log($"[SpellEffects-OnPlayerHitBall] === START ===");
        Debug.Log($"[SpellEffects-OnPlayerHitBall] spellName: '{spellName}' (empty={string.IsNullOrEmpty(spellName)})");
        Debug.Log($"[SpellEffects-OnPlayerHitBall] IsServer: {IsServer}");
        Debug.Log($"[SpellEffects-OnPlayerHitBall] hitter: {hitter?.name ?? "NULL"}");
        Debug.Log($"[SpellEffects-OnPlayerHitBall] ballOwner: {ballOwner?.name ?? "NULL"}");

        if (string.IsNullOrEmpty(spellName))
        {
            Debug.Log("[SpellEffects-OnPlayerHitBall] EXIT - spellName is null/empty");
            return;
        }

        if (!IsServer)
        {
            Debug.LogWarning("[SpellEffects-OnPlayerHitBall] EXIT - Not server!");
            return;
        }

        Debug.Log($"[SpellEffects-OnPlayerHitBall] Active spell: {spellName}, resetOnOppHit: {resetOnOppHit}");

        // Fireball: knockback when OPPONENT hits the enchanted ball
        if (spellName == "Fireball")
        {
            Debug.Log($"[SpellEffects-OnPlayerHitBall] FIREBALL CHECK:");
            Debug.Log($"  - resetOnOppHit: {resetOnOppHit}");
            Debug.Log($"  - ballOwner: {ballOwner?.name ?? "NULL"}");
            Debug.Log($"  - hitter: {hitter?.name ?? "NULL"}");
            Debug.Log($"  - hitter != ballOwner: {hitter != ballOwner}");
            Debug.Log($"  - currentPlayer (context): {currentPlayer?.name ?? "NULL"}");
            Debug.Log($"  - currentOpponent (context): {currentOpponent?.name ?? "NULL"}");

            if (resetOnOppHit && ballOwner != null && hitter != ballOwner)
            {
                Debug.Log($"[SpellEffects-OnPlayerHitBall] ✓ FIREBALL TRIGGERED!");

                ulong victimClientId = hitter.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                Debug.Log($"[SpellEffects-OnPlayerHitBall] Victim ClientId: {victimClientId}");

                if (victimClientId != ulong.MaxValue)
                {
                    // CRITICAL: Set context BEFORE calling ServerRpc
                    // This ensures the server has the right caster/victim reference
                    Debug.Log($"[SpellEffects-OnPlayerHitBall] Setting context - caster:{ballOwner.name}, victim:{hitter.name}");
                    SetContext(ballOwner, hitter, null);

                    Debug.Log($"[SpellEffects-OnPlayerHitBall] Calling ApplyFireballKnockbackServerRpc({victimClientId})");
                    ApplyFireballKnockbackServerRpc(victimClientId);
                }
                else
                {
                    Debug.LogError("[SpellEffects-OnPlayerHitBall] ✗ FAILED - hitter has no NetworkObject");
                }
            }
            else
            {
                Debug.Log($"[SpellEffects-OnPlayerHitBall] ✗ Fireball not triggered - armed:{resetOnOppHit}");
            }

            Debug.Log($"[SpellEffects-OnPlayerHitBall] === END (Fireball) ===");
            return;
        }

        // Shadow: reset when opponent hits
        if (spellName == "Shadow" && resetOnOppHit)
        {
            if (ballOwner != null && hitter != ballOwner)
            {
                Debug.Log($"[SpellEffects-OnPlayerHitBall] Shadow broken by {hitter.name} hitting the ball");
                resetSpellEffect();
            }
        }

        if (spellName == "Mud")
        {
            Debug.Log($"[SpellEffects-OnPlayerHitBall] MUD CHECK:");
            Debug.Log($"  - resetOnOppHit: {resetOnOppHit}");
            Debug.Log($"  - ballOwner: {ballOwner?.name ?? "NULL"}");
            Debug.Log($"  - hitter: {hitter?.name ?? "NULL"}");

            if (resetOnOppHit && ballOwner != null && hitter != ballOwner)
            {
                Debug.Log($"[SpellEffects-OnPlayerHitBall] ✓ MUD TRIGGERED!");

                // Find the ball to get its position (like the non-networked version)
                GameObject ballObject = GameObject.FindWithTag("Ball");

                if (ballObject != null)
                {
                    Vector3 spawnPos = ballObject.transform.position;
                    spawnPos.y = 0.94f; // Match the non-networked version's ground height

                    Debug.Log($"[SpellEffects-OnPlayerHitBall] Spawning mud pit at ball position: {spawnPos}");

                    ulong casterClientId = ballOwner.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    ulong victimClientId = hitter.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    if (casterClientId != ulong.MaxValue && victimClientId != ulong.MaxValue)
                    {
                        SpawnEffectServerRpc("Mud", casterClientId, victimClientId, spawnPos, Quaternion.identity);

                        // Reset the spell after spawning
                        resetSpellEffect();
                    }
                    else
                    {
                        Debug.LogError("[SpellEffects-OnPlayerHitBall] ✗ FAILED - invalid client IDs");
                    }
                }
                else
                {
                    Debug.LogError("[SpellEffects-OnPlayerHitBall] ✗ FAILED - Ball not found!");
                }
            }
            else
            {
                Debug.Log($"[SpellEffects-OnPlayerHitBall] ✗ Mud not triggered");
            }

            Debug.Log($"[SpellEffects-OnPlayerHitBall] === END (Mud) ===");
            return;
        }

        Debug.Log($"[SpellEffects-OnPlayerHitBall] === END ===");
    }

    private IEnumerator MirrorMovement(GameObject gemini, MainCharacterMovement casterMCM, float duration)
    {
        float elapsed = 0f;

        // Cache transform for speed
        Transform geminiT = gemini.transform;
        Transform casterT = casterMCM.transform;

        while (elapsed < duration && gemini != null && casterMCM != null)
        {
            elapsed += Time.deltaTime;

            // --- COPY MOVEMENT INPUT ---
            Vector3 casterVelocity = casterMCM.controller.velocity;

            // --- MIRROR POSITION ---
            Vector3 mirroredPos = casterT.position;
            mirroredPos.x = -mirroredPos.x;
            geminiT.position = mirroredPos;

            // --- MATCH ROTATION ---
            geminiT.rotation = casterT.rotation;

            yield return null;
        }
    }

    /// <summary>
    /// Helper: Schedule a reset and cancel any pending resets for this spell
    /// </summary>
    private void ScheduleReset(float delay)
    {
        // Small catch so this doesn't fire unessecarily
        if (spellName == "Ice")
        {
            // Force all Active Freeze Routines to stop
            ForceUnfreezeAllPlayers();
        }

        CancelInvoke(nameof(resetSpellEffect));

        // Schedule new reset
        Invoke(nameof(resetSpellEffect), delay);
    }

    /// <summary>
    /// Server RPC to freeze a player across all clients
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void FreezePlayerServerRpc(ulong targetClientId, float duration, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        Debug.Log($"[SpellEffects-Server] Freezing player {targetClientId} for {duration}s");

        // Cancel any existing freeze for this player
        if (activeFreezeCoroutines.ContainsKey(targetClientId))
        {
            Debug.Log($"[SpellEffects-Server] Canceling existing freeze for player {targetClientId}");
            // ClientRpc will handle cleanup on each client
        }

        // Tell ALL clients to freeze this player
        FreezePlayerClientRpc(targetClientId, duration);
    }

    /// <summary>
    /// Client RPC to freeze a player locally
    /// </summary>
    [ClientRpc]
    private void FreezePlayerClientRpc(ulong targetClientId, float duration)
    {
        GameObject target = GetPlayerByClientId(targetClientId);
        if (target == null)
        {
            Debug.LogWarning($"[SpellEffects-Client] Cannot freeze - target {targetClientId} not found");
            return;
        }

        // Cancel existing freeze coroutine for this player
        if (activeFreezeCoroutines.ContainsKey(targetClientId) && activeFreezeCoroutines[targetClientId] != null)
        {
            StopCoroutine(activeFreezeCoroutines[targetClientId]);
            Debug.Log($"[SpellEffects-Client] Stopped existing freeze for {target.name}");
        }

        Debug.Log($"[SpellEffects-Client] Freezing {target.name} locally for {duration}s");
        Coroutine freezeRoutine = StartCoroutine(FreezeOpponentMovement(target, duration, targetClientId));
        activeFreezeCoroutines[targetClientId] = freezeRoutine;
    }

    public void ForceUnfreezeAllPlayers()
    {
        Debug.Log($"[SpellEffects] Force unfreezing all players - {activeFreezeCoroutines.Count} active freezes");

        foreach (var kvp in activeFreezeCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }

            GameObject player = GetPlayerByClientId(kvp.Key);
            if (player != null)
            {
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null && mcm.speed == 0f)
                {
                    mcm.speed = 7f;
                    Debug.Log($"[SpellEffects] Force restored {player.name} movement");
                }
            }
        }

        activeFreezeCoroutines.Clear();
    }

    private IEnumerator HandleMud(GameObject mud, float duration)
    {
        if (mud == null) yield break;

        Vector3 endScale = mud.transform.localScale;
        Vector3 startScale = new Vector3(0.1f, 0.01f, 0.1f);
        mud.transform.localScale = startScale;

        float t = 0f;
        while (t < 1f)
        {
            if (mud == null) yield break;
            t += Time.deltaTime * 2f; // speed of growth
            mud.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        // Wait for the mud's duration
        yield return new WaitForSeconds(duration - 0.5f);

        // Optional: Shrink before despawn
        float shrink = 0f;
        while (shrink < 0.5f)
        {
            if (mud == null) yield break;
            shrink += Time.deltaTime;
            mud.transform.localScale = Vector3.Lerp(endScale, Vector3.zero, shrink / 0.5f);
            yield return null;
        }
    }

    /// <summary>
    /// Activates or deactivates the lightning trail for a specific player
    /// </summary>
    private void ActivateLightningTrail(GameObject player, bool active)
    {
        if (player == null)
        {
            Debug.LogWarning("[SpellEffects] ActivateLightningTrail: player is null");
            return;
        }

        Debug.Log($"[SpellEffects] ActivateLightningTrail called - player: {player.name}, active: {active}");

        // Get the player's NetworkedSpellcasting component to access their trail object
        var spellcasting = player.GetComponent<NetworkedSpellcasting>();
        if (spellcasting == null)
        {
            Debug.LogWarning($"[SpellEffects] Player {player.name} has no NetworkedSpellcasting component");
            return;
        }

        GameObject trailObject = spellcasting.LightningTrailObject;

        Debug.Log($"[SpellEffects] Trail object from spellcasting: {(trailObject != null ? trailObject.name : "NULL")}");

        if (trailObject == null)
        {
            Debug.LogWarning($"[SpellEffects] Player {player.name} has no Lightning Trail Object assigned!");
            return;
        }

        // Check current state before changing
        bool currentState = trailObject.activeSelf;
        Debug.Log($"[SpellEffects] Trail {trailObject.name} current state: {currentState}, setting to: {active}");

        // Store reference for tracking
        ulong clientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
        if (clientId != ulong.MaxValue)
        {
            if (active)
            {
                playerLightningTrails[clientId] = trailObject;
                Debug.Log($"[SpellEffects] Added trail to tracking dictionary for client {clientId}");
            }
            else
            {
                playerLightningTrails.Remove(clientId);
                Debug.Log($"[SpellEffects] Removed trail from tracking dictionary for client {clientId}");
            }
        }
        else
        {
            Debug.LogWarning($"[SpellEffects] Could not get client ID for {player.name}");
        }

        // Activate/deactivate the trail
        trailObject.SetActive(active);

        // Verify the change took effect
        bool newState = trailObject.activeSelf;
        Debug.Log($"[SpellEffects] Trail {trailObject.name} new state after SetActive: {newState}");

        if (newState != active)
        {
            Debug.LogError($"[SpellEffects] FAILED to change trail state! Expected: {active}, Got: {newState}");
        }
        else
        {
            Debug.Log($"[SpellEffects] ✓ Lightning trail {(active ? "activated" : "deactivated")} successfully for {player.name}");
        }
    }

    /// <summary>
    /// Deactivates all active lightning trails (useful for cleanup)
    /// </summary>
    public void DeactivateAllLightningTrails()
    {
        foreach (var kvp in playerLightningTrails)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetActive(false);
                Debug.Log($"[SpellEffects] Deactivated lightning trail for client {kvp.Key}");
            }
        }

        playerLightningTrails.Clear();
    }

    /// <summary>
    /// Called when the local player wins a point - plays victory sound
    /// </summary>
    public void OnPointWon()
    {
        if (pointSource != null && pointWon != null)
        {
            pointSource.PlayOneShot(pointWon, pointSFXVolume);
            Debug.Log("[SpellEffects] Point won sound played");
        }
    }

    /// <summary>
    /// Called when the local player loses a point - plays defeat sound
    /// </summary>
    public void OnPointLost()
    {
        if (pointSource != null && pointlost != null)
        {
            pointSource.PlayOneShot(pointlost, pointSFXVolume);
            Debug.Log("[SpellEffects] Point lost sound played");
        }
    }
}