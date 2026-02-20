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

    // Removed the old Context Variables

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

    // Track active networked effects for cleanup
    private Dictionary<string, ulong> activeNetworkEffects = new Dictionary<string, ulong>();

    private Dictionary<ulong, Coroutine> activeFreezeCoroutines = new Dictionary<ulong, Coroutine>();

    /// <summary>
    /// Tracks state for a single active spell cast by a specific player
    /// </summary>
    [System.Serializable]
    public class ActiveSpellData
    {
        public string spellName;
        public GameObject caster;
        public GameObject target;
        public TennisAI ai;
        public bool resetOnOppHit;
        public bool resetOnPlrHit;
        public bool resetOnBounce;
        public float startTime;
        public Dictionary<string, GameObject> activeEffects = new Dictionary<string, GameObject>();

        // Spell-specific tracking
        public Coroutine activeLightningCoroutine;
        public Coroutine activeFreezeCoroutine;
        public Vector3 storedBlinkDirection;
    }

    private Dictionary<ulong, ActiveSpellData> activeSpells = new Dictionary<ulong, ActiveSpellData>();

    public bool IsAnySpellActive => activeSpells.Count > 0;

    private HashSet<string> globalLockSpells = new HashSet<string> { "Chronos" };
    private string currentGlobalLockSpell = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// UPDATED: Set context for a specific caster
    /// </summary>
    public void SetContext(ulong casterClientId, GameObject player, GameObject opponent, TennisAI ai)
    {
        if (!activeSpells.ContainsKey(casterClientId))
        {
            activeSpells[casterClientId] = new ActiveSpellData();
        }

        activeSpells[casterClientId].caster = player;
        activeSpells[casterClientId].target = opponent;
        activeSpells[casterClientId].ai = ai;
        activeSpells[casterClientId].startTime = Time.time;

        Debug.Log($"[SpellEffects] Context set for client {casterClientId} - Player: {player?.name}, Opponent: {opponent?.name}");
    }

    /// <summary>
    /// UPDATED: Auto-set context for a specific caster
    /// </summary>
    public void AutoSetContext(ulong casterClientId, GameObject ballOwner)
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

        SetContext(casterClientId, player, opponent, ai);
        Debug.Log($"[SpellEffects] AutoSetContext completed for client {casterClientId} -> opponent: {opponent?.name}");
    }

    /// <summary>
    /// UPDATED: Clear context for a specific caster
    /// </summary>
    public void ClearContext(ulong casterClientId)
    {
        if (activeSpells.ContainsKey(casterClientId))
        {
            activeSpells.Remove(casterClientId);
            Debug.Log($"[SpellEffects] Context cleared for client {casterClientId}");
        }
    }

    /// <summary>
    /// UPDATED: Get active spell data for a caster
    /// </summary>
    private ActiveSpellData GetSpellData(ulong casterClientId)
    {
        if (activeSpells.TryGetValue(casterClientId, out ActiveSpellData data))
        {
            return data;
        }

        Debug.LogWarning($"[SpellEffects] No active spell data for client {casterClientId}");
        return null;
    }

    /// <summary>
    /// UPDATED: Check if a specific caster has an active spell
    /// </summary>
    public bool HasActiveSpell(ulong casterClientId)
    {
        return activeSpells.ContainsKey(casterClientId);
    }

    /// <summary>
    /// UPDATED: Get the spell name for a specific caster
    /// </summary>
    public string GetActiveSpellName(ulong casterClientId)
    {
        if (activeSpells.TryGetValue(casterClientId, out ActiveSpellData data))
        {
            return data.spellName;
        }
        return null;
    }

    /// <summary>
    /// Gets the active player (context)
    /// UPDATED: Returns the local player or first active spell's caster
    /// </summary>
    public GameObject GetPlayer()
    {
        // First: Check if we have any active spells and return the first caster
        if (activeSpells.Count > 0)
        {
            foreach (var kvp in activeSpells)
            {
                if (kvp.Value.caster != null)
                {
                    Debug.Log($"[SpellEffects-GetPlayer] Using active spell caster: {kvp.Value.caster.name}");
                    return kvp.Value.caster;
                }
            }
        }

        // Second: Try to find by LocalClientId
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

            // Skip objects without player components
            if (go.GetComponent<MainCharacterMovement>() == null &&
                go.GetComponent<CharacterController>() == null &&
                go.GetComponent<Rigidbody>() == null)
            {
                continue;
            }

            // Make sure it's owned by the local client
            NetworkObject netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == localId)
            {
                Debug.Log($"[SpellEffects-GetPlayer] Found valid player via fallback: {go.name}");
                return go;
            }
        }

        Debug.LogError($"[SpellEffects-GetPlayer] Could not find player for LocalClientId {localId}!");
        return null;
    }

    /// <summary>
    /// Gets the active opponent (context)
    /// UPDATED: Returns opponent from first active spell
    /// </summary>
    private GameObject GetOpponent()
    {
        if (activeSpells.Count > 0)
        {
            foreach (var kvp in activeSpells)
            {
                if (kvp.Value.target != null)
                {
                    return kvp.Value.target;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the active AI (context)
    /// UPDATED: Returns AI from first active spell
    /// </summary>
    private TennisAI GetAI()
    {
        if (activeSpells.Count > 0)
        {
            foreach (var kvp in activeSpells)
            {
                if (kvp.Value.ai != null)
                {
                    return kvp.Value.ai;
                }
            }
        }

        return null;
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

                    // Trigger reset logic
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

    /// <summary>
    /// UPDATED: Cast spell for a specific caster
    /// </summary>
    public void castSpell(ulong casterClientId, string spell)
    {
        // Validate input
        if (string.IsNullOrEmpty(spell))
        {
            Debug.LogError("[SpellEffects] castSpell called with no spell name!");
            return;
        }

        // Check for global lock spells
        if (globalLockSpells.Contains(spell))
        {
            if (!string.IsNullOrEmpty(currentGlobalLockSpell))
            {
                Debug.LogWarning($"[SpellEffects] Cannot cast {spell} - {currentGlobalLockSpell} is active");
                return;
            }
            currentGlobalLockSpell = spell;
        }

        // Get or create spell data
        if (!activeSpells.ContainsKey(casterClientId))
        {
            Debug.LogWarning($"[SpellEffects] No context for client {casterClientId}, attempting to find player...");

            GameObject player = GetPlayerByClientId(casterClientId);
            if (player == null)
            {
                Debug.LogError($"[SpellEffects] Cannot cast spell - no player found for client {casterClientId}");
                return;
            }

            GameObject opponent = null;
            var allPlayers = FindObjectsOfType<NetworkedSpellcasting>();
            foreach (var sc in allPlayers)
            {
                if (sc.gameObject != player)
                {
                    opponent = sc.gameObject;
                    break;
                }
            }

            SetContext(casterClientId, player, opponent, null);
        }

        ActiveSpellData spellData = activeSpells[casterClientId];
        spellData.spellName = spell;
        spellData.startTime = Time.time;

        GameObject casterPlayer = spellData.caster;
        GameObject targetOpponent = spellData.target;

        if (casterPlayer == null)
        {
            Debug.LogError($"[SpellEffects] Cannot cast spell - caster is null for client {casterClientId}");
            return;
        }

        // Show explanation (once per round per spell)
        if (OptionsManager.Instance != null && !spellsUsedThisRound.Contains(spell) && OptionsManager.Instance.spellTips)
        {
            spellsUsedThisRound.Add(spell);
            explanationRoutine = StartCoroutine(ShowSpellExplanation(spell));
        }

        // Get client IDs for network spawning
        ulong targetClientId = targetOpponent?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
        bool isLocalCaster = (NetworkManager.Singleton.LocalClientId == casterClientId);

        Debug.Log($"[SpellEffects] Casting {spell} for client {casterClientId} - isLocalCaster: {isLocalCaster}");

        // Execute spell logic
        switch (spell)
        {
            case "Lightning":
                if (isLocalCaster)
                {
                    var mcm = casterPlayer.GetComponent<MainCharacterMovement>();
                    if (mcm != null)
                    {
                        if (spellData.activeLightningCoroutine != null)
                            StopCoroutine(spellData.activeLightningCoroutine);

                        spellData.activeLightningCoroutine = StartCoroutine(ApplyLightningSpeed(casterClientId, mcm, 5f));
                        ActivateLightningTrail(casterPlayer, true);
                    }
                }
                break;

            case "Ice":
                if (isLocalCaster && targetOpponent != null && iceBlockPrefab != null)
                {
                    SpawnEffectServerRpc("Ice", casterClientId, targetClientId,
                        targetOpponent.transform.position, targetOpponent.transform.rotation);
                    FreezePlayerServerRpc(targetClientId, 5f);
                    ScheduleReset(casterClientId, 5f);
                }
                break;

            case "Fireball":
                if (isLocalCaster)
                {
                    spellData.resetOnOppHit = true;
                    spellData.resetOnPlrHit = false;

                    if (!IsServer)
                    {
                        SetSpellArmedServerRpc(casterClientId, true, false);
                    }

                    GameObject fireballBallObj = GameObject.FindWithTag("Ball");
                    if (fireballBallObj != null)
                    {
                        CollisionTrackerBall tracker = fireballBallObj.GetComponent<CollisionTrackerBall>();
                        if (tracker != null)
                        {
                            tracker.LastHitWizard = "Player";
                        }
                    }
                }
                break;

            case "Shadow":
                if (isLocalCaster && targetOpponent != null)
                {
                    ApplyShadowInvisibilityServerRpc(casterClientId, targetClientId);
                    ScheduleReset(casterClientId, 10f);
                }
                break;

            case "Mud":
                if (isLocalCaster)
                {
                    spellData.resetOnOppHit = true;
                    spellData.resetOnBounce = false;

                    if (!IsServer)
                    {
                        SetSpellArmedServerRpc(casterClientId, true, false);
                    }
                }
                break;

            case "Green":
                if (isLocalCaster)
                {
                    var greenBall = casterPlayer.GetComponent<NetworkedBall>();
                    if (greenBall != null)
                    {
                        greenBall.green = true;

                        if (IsOwner)
                        {
                            var uiManager = casterPlayer.GetComponent<NetworkedUIManager>();
                            if (uiManager != null)
                            {
                                uiManager.UpdateSpellStatus("Green Active", Color.green);
                            }
                        }
                    }
                    ScheduleReset(casterClientId, 10f);
                }
                break;

            case "Stone":
                if (isLocalCaster && casterPlayer != null && stoneWallPrefab != null)
                {
                    Vector3 spawnPos = casterPlayer.transform.position + casterPlayer.transform.forward * 2f;
                    SpawnEffectServerRpc("Stone", casterClientId, targetClientId, spawnPos, Quaternion.identity);
                }
                break;

            case "Chronos":
                if (isLocalCaster)
                {
                    StartCoroutine(ApplyChronosAfterExplanation(casterClientId));
                }
                break;

            case "Gemini":
                if (isLocalCaster && casterPlayer != null && geminiPrefab != null)
                {
                    Vector3 gemPos = casterPlayer.transform.position;
                    gemPos.x = -casterPlayer.transform.position.x;
                    SpawnEffectServerRpc("Gemini", casterClientId, targetClientId, gemPos, casterPlayer.transform.rotation);
                }
                ScheduleReset(casterClientId, 5f);
                break;

            case "Blink":
                if (isLocalCaster)
                {
                    var blinkMCM = casterPlayer.GetComponent<MainCharacterMovement>();
                    if (blinkMCM != null && blinkMCM.controller != null)
                    {
                        Vector3 velocity = blinkMCM.controller.velocity;
                        velocity.y = 0;

                        if (velocity.magnitude > 0.1f)
                            spellData.storedBlinkDirection = velocity.normalized * 3f;
                        else
                        {
                            spellData.storedBlinkDirection = casterPlayer.transform.forward * 3f;
                            spellData.storedBlinkDirection.y = 0;
                        }
                    }
                    else
                    {
                        spellData.storedBlinkDirection = casterPlayer.transform.forward * 3f;
                        spellData.storedBlinkDirection.y = 0;
                    }

                    PerformBlink(casterPlayer, spellData.storedBlinkDirection);
                }
                ScheduleReset(casterClientId, 5f);
                break;

            case "Jolly":
                if (isLocalCaster && casterPlayer != null)
                {
                    var capsule = casterPlayer.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        capsule.radius = 2;
                    }

                    GameObject jollyObject = FindJollyObject(casterPlayer);
                    if (jollyObject != null)
                    {
                        jollyObject.SetActive(true);
                        string key = $"{casterClientId}_Jolly";
                        spellData.activeEffects[key] = jollyObject;
                        HideRacketMesh(casterPlayer, false);
                    }
                }
                ScheduleReset(casterClientId, 5f);
                break;

            case "Warp":
                ScheduleReset(casterClientId, 0.5f);
                break;

            case "Pisces":
                if (isLocalCaster && casterPlayer != null && orbiterPrefab != null)
                {
                    SpawnEffectServerRpc("Pisces", casterClientId, targetClientId,
                        casterPlayer.transform.position, casterPlayer.transform.rotation);
                }
                break;

            case "Tether":
                if (isLocalCaster && targetOpponent != null && tetherPrefab != null)
                {
                    Vector3 tetherPos = targetOpponent.transform.position;
                    tetherPos.y = 1.45f;
                    SpawnEffectServerRpc("Tether", casterClientId, targetClientId, tetherPos, Quaternion.identity);
                }
                ScheduleReset(casterClientId, 5f);
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
                ScheduleReset(casterClientId, 5f);
                break;

                // The Gambit case WAS here, but I've removed it.
                // The system now handles Gambit by catching the Spell name in the Cast routine in NetworkedSpellEffects.cs and changes that value randomly based on the Gambit-Available spells

                // Visual casting is already handled by NetworkedSpellcasting.CastSpellServerRpc
                // No need to call CastSpellNormal here - it would cause duplicate visual spawning
                // The ball visual is managed by NetworkedSpellcasting, not SpellEffects
        }
    }

    /// <summary>
    /// UPDATED: Blink with direction parameter
    /// </summary>
    private void PerformBlink(GameObject player, Vector3 blinkDirection)
    {
        if (player == null)
        {
            Debug.LogWarning("[SpellEffects] Blink failed - no player");
            return;
        }

        if (blinkDirection.magnitude < 0.1f)
        {
            blinkDirection = player.transform.forward * 3f;
            blinkDirection.y = 0;
        }

        Vector3 newPos = player.transform.position + blinkDirection;

        if (player.transform.position.z > 0)
        {
            newPos.z = Mathf.Clamp(newPos.z, 0.5f, 10.9f);
        }
        else
        {
            newPos.z = Mathf.Clamp(newPos.z, -11.5f, -1f);
        }

        newPos.x = Mathf.Clamp(newPos.x, -4.9f, 4.9f);
        newPos.y = player.transform.position.y;

        var movement = player.GetComponent<MainCharacterMovement>();
        var controller = player.GetComponent<CharacterController>();

        if (movement != null) movement.enabled = false;
        if (controller != null) controller.enabled = false;

        player.transform.position = newPos;

        if (controller != null) controller.enabled = true;
        if (movement != null)
        {
            movement.enabled = true;
            movement.ForceMovementRefresh();
        }
    }

    /// <summary>
    /// UPDATED: Reset spell for a specific caster
    /// </summary>
    public void resetSpellEffect(ulong casterClientId)
    {
        if (!activeSpells.TryGetValue(casterClientId, out ActiveSpellData spellData))
        {
            Debug.LogWarning($"[SpellEffects] No active spell to reset for client {casterClientId}");
            return;
        }

        GameObject player = spellData.caster;
        GameObject opponent = spellData.target;
        string spell = spellData.spellName;

        if (player == null)
        {
            Debug.LogWarning($"[SpellEffects] Cannot reset spell - player is null for client {casterClientId}");
            activeSpells.Remove(casterClientId);
            return;
        }

        Debug.Log($"[SpellEffects] Resetting spell '{spell}' for client {casterClientId}");

        switch (spell)
        {
            case "Lightning":
                if (spellData.activeLightningCoroutine != null)
                {
                    StopCoroutine(spellData.activeLightningCoroutine);
                    spellData.activeLightningCoroutine = null;
                }

                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null)
                {
                    mcm.speed = 7;
                }

                ActivateLightningTrail(player, false);
                break;

            case "Green":
                var greenBall = player.GetComponent<NetworkedBall>();
                if (greenBall != null)
                {
                    greenBall.green = false;
                }
                break;

            case "Ice":
                ulong oppClientId = opponent?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                if (oppClientId != ulong.MaxValue)
                {
                    UnfreezePlayerServerRpc(oppClientId);
                }
                break;

            case "Fireball":
                spellData.resetOnOppHit = false;

                if (!IsServer)
                {
                    SetSpellArmedServerRpc(casterClientId, false, false);
                }

                if (flamePrefab != null && opponent != null)
                {
                    ulong localClientId = NetworkManager.Singleton.LocalClientId;
                    if (localClientId == casterClientId)
                    {
                        ulong targetClientId = opponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                        SpawnEffectServerRpc("Flame", casterClientId, targetClientId,
                            opponent.transform.position, Quaternion.identity);
                    }
                }
                break;

            case "Shadow":
                if (player != null && opponent != null)
                {
                    ulong casterCId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    ulong opponentCId = opponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    if (casterCId != ulong.MaxValue && opponentCId != ulong.MaxValue)
                    {
                        RemoveShadowInvisibilityServerRpc(casterCId, opponentCId);
                    }
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
                currentGlobalLockSpell = null;
                break;

            case "Jolly":
                if (player != null)
                {
                    var capsule = player.GetComponent<CapsuleCollider>();
                    if (capsule != null)
                    {
                        capsule.radius = 1;
                    }

                    HideRacketMesh(player, false);

                    string key = $"{casterClientId}_Jolly";
                    if (spellData.activeEffects.TryGetValue(key, out GameObject jollyObj) && jollyObj != null)
                    {
                        jollyObj.SetActive(false);
                        spellData.activeEffects.Remove(key);
                    }
                }
                break;

            case "Mud":
                spellData.resetOnOppHit = false;
                spellData.resetOnBounce = false;

                if (!IsServer)
                {
                    SetSpellArmedServerRpc(casterClientId, false, false);
                }
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
                            savedVelocity = ballRb.linearVelocity;
                            savedAngularVelocity = ballRb.angularVelocity;
                            hadGravity = ballRb.useGravity;
                            ballRb.useGravity = false;
                            ballRb.isKinematic = true;
                        }

                        Vector3 ballPos = warpBall.transform.position;
                        ballPos.x = ballX;
                        warpBall.transform.position = ballPos;

                        if (ballRb != null)
                        {
                            ballRb.isKinematic = false;
                            ballRb.useGravity = hadGravity;
                            ballRb.linearVelocity = savedVelocity;
                            ballRb.angularVelocity = savedAngularVelocity;
                        }
                    }
                }
                break;
        }

        // Clean up spell data
        activeSpells.Remove(casterClientId);
        Debug.Log($"[SpellEffects] Spell '{spell}' reset complete for client {casterClientId}");
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

    // Helper function for Jolly - WITH NULL CHECKS
    private IEnumerator FollowPositionOnlyWithNullChecks(Transform obj, Transform target)
    {
        if (obj == null)
        {
            Debug.LogWarning("[SpellEffects] FollowPositionOnly: obj is null at start");
            yield break;
        }

        if (target == null)
        {
            Debug.LogWarning("[SpellEffects] FollowPositionOnly: target is null at start");
            yield break;
        }

        Debug.Log($"[SpellEffects] Starting FollowPositionOnly: {obj.name} -> {target.name}");
        int frameCount = 0;

        while (obj != null && target != null)
        {
            obj.position = target.position;  // follow position only
                                             // ignore target.rotation completely

            frameCount++;
            yield return null;
        }

        // Log why we stopped
        if (obj == null && target == null)
        {
            Debug.LogWarning($"[SpellEffects] FollowPositionOnly stopped: both obj and target became null (ran {frameCount} frames)");
        }
        else if (obj == null)
        {
            Debug.LogWarning($"[SpellEffects] FollowPositionOnly stopped: obj became null (ran {frameCount} frames)");
        }
        else if (target == null)
        {
            Debug.LogWarning($"[SpellEffects] FollowPositionOnly stopped: target became null (ran {frameCount} frames)");
        }
    }

    // Keep old version for backward compatibility
    private IEnumerator FollowPositionOnly(Transform obj, Transform target)
    {
        return FollowPositionOnlyWithNullChecks(obj, target);
    }

    /// <summary>
    /// UPDATED: Lightning speed boost for specific caster
    /// </summary>
    private IEnumerator ApplyLightningSpeed(ulong casterClientId, MainCharacterMovement mcm, float duration)
    {
        if (mcm == null)
        {
            Debug.LogWarning($"[SpellEffects] Lightning: MCM is null for client {casterClientId}");
            yield break;
        }

        float originalSpeed = mcm.speed;
        float boostedSpeed = 17f;

        ActiveSpellData spellData = GetSpellData(casterClientId);
        if (spellData != null && spellData.spellName == "Chronos")
        {
            boostedSpeed = 70f;
        }

        Debug.Log($"[SpellEffects] Lightning: Setting speed from {originalSpeed} to {boostedSpeed} for client {casterClientId}");
        mcm.speed = boostedSpeed;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (mcm == null) yield break;

            if (mcm.speed != boostedSpeed)
            {
                mcm.speed = boostedSpeed;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (mcm != null)
        {
            mcm.speed = originalSpeed;
            Debug.Log($"[SpellEffects] Lightning: Restored speed to {originalSpeed} for client {casterClientId}");
        }
    }

    /// <summary>
    /// UPDATED: Fireball knockback for specific caster/victim
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ApplyFireballKnockbackServerRpc(ulong casterClientId, ulong victimClientId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ActiveSpellData spellData = GetSpellData(casterClientId);
        if (spellData == null)
        {
            Debug.LogError($"[SpellEffects-ServerRpc] No spell data for caster {casterClientId}");
            return;
        }

        GameObject caster = spellData.caster;
        GameObject victim = GetPlayerByClientId(victimClientId);

        if (victim == null)
        {
            Debug.LogError($"[SpellEffects-ServerRpc] Victim not found for client {victimClientId}");
            return;
        }

        Vector3 knockbackDirection = Vector3.back;
        if (caster != null)
        {
            Vector3 directionFromCaster = (victim.transform.position - caster.transform.position).normalized;
            Vector3 horizontal = new Vector3(directionFromCaster.x, 0, directionFromCaster.z).normalized;
            Vector3 upward = Vector3.up * 0.75f;
            knockbackDirection = (horizontal + upward).normalized;
        }

        ApplyFireballKnockbackClientRpc(victimClientId, knockbackDirection, fireballForceStrength, 0.45f);

        // Reset after knockback
        StartCoroutine(DelayedResetAfterKnockback(casterClientId, 0.65f));
    }
    private IEnumerator DelayedResetAfterKnockback(ulong casterClientId, float delay)
    {
        yield return new WaitForSeconds(delay);
        resetSpellEffect(casterClientId);
    }

    /// <summary>
    /// NEW: Unfreeze a specific player
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void UnfreezePlayerServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        UnfreezePlayerClientRpc(targetClientId);
    }

    [ClientRpc]
    private void UnfreezePlayerClientRpc(ulong targetClientId)
    {
        GameObject target = GetPlayerByClientId(targetClientId);
        if (target != null)
        {
            var mcm = target.GetComponent<MainCharacterMovement>();
            if (mcm != null && mcm.speed == 0f)
            {
                mcm.speed = 7f;
                Debug.Log($"[SpellEffects-Client] Force unfroze {target.name}");
            }
        }
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
            // We need BOTH caster and victim IDs now
            ulong casterClientId = ulong.MaxValue;

            // Try to find which spell data has this victim as the target
            foreach (var kvp in activeSpells)
            {
                if (kvp.Value.target == target)
                {
                    casterClientId = kvp.Key;
                    break;
                }
            }

            if (casterClientId != ulong.MaxValue)
            {
                ApplyFireballKnockbackServerRpc(casterClientId, victimClientId);
            }
            else
            {
                Debug.LogError("[SpellEffects] Cannot apply Fireball knockback - no active spell found for this target");
            }
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
        float gravity = 25f; // Match MainCharacterMovement gravity value

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

    /// <summary>
    /// UPDATED: Chronos with context
    /// </summary>
    private IEnumerator ApplyChronosAfterExplanation(ulong casterClientId)
    {
        ActiveSpellData spellData = GetSpellData(casterClientId);
        if (spellData == null || spellData.caster == null)
        {
            Debug.LogError($"[SpellEffects] Chronos failed - no spell data for client {casterClientId}");
            yield break;
        }

        GameObject player = spellData.caster;
        var mcm = player.GetComponent<MainCharacterMovement>();

        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);
        yield return new WaitForSecondsRealtime(0.05f);

        lastOriginalTimeScale = Time.timeScale;
        isSpellSlowdownActive = true;
        Time.timeScale = 0.1f;

        if (spellData.activeLightningCoroutine != null)
            StopCoroutine(spellData.activeLightningCoroutine);

        spellData.activeLightningCoroutine = StartCoroutine(ApplyLightningSpeed(casterClientId, mcm, 0.4f));

        if (OptionsManager.Instance != null && OptionsManager.Instance.spellTips && !string.IsNullOrEmpty(spellData.spellName))
        {
            if (explanationRoutine != null) StopCoroutine(explanationRoutine);
            explanationRoutine = StartCoroutine(ShowSpellExplanation(spellData.spellName));
            yield return new WaitForSecondsRealtime(explanationDuration);
        }
        else
        {
            yield return new WaitForSecondsRealtime(2.5f);
        }

        Time.timeScale = lastOriginalTimeScale;
        isSpellSlowdownActive = false;

        if (mcm != null)
        {
            mcm.speed = 7f;
            mcm.gravity = 25f;
        }

        yield return new WaitForSeconds(0.1f);
        resetSpellEffect(casterClientId);
    }

    /// <summary>
    /// UPDATED: Set spell armed state for specific caster
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void SetSpellArmedServerRpc(ulong casterClientId, bool oppHit, bool plrHit, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (activeSpells.TryGetValue(casterClientId, out ActiveSpellData spellData))
        {
            spellData.resetOnOppHit = oppHit;
            spellData.resetOnPlrHit = plrHit;
            Debug.Log($"[SpellEffects-Server] Spell armed for client {casterClientId} - resetOnOppHit: {oppHit}");
        }
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
    /// UPDATED: Check all active spells when player hits ball
    /// </summary>
    public void OnPlayerHitBall(GameObject hitter, GameObject ballOwner)
    {
        Debug.Log($"[SpellEffects-OnPlayerHitBall] Checking {activeSpells.Count} active spells");

        if (activeSpells.Count == 0)
            return;

        if (!IsServer)
        {
            Debug.LogWarning("[SpellEffects-OnPlayerHitBall] Called on non-server!");
            return;
        }

        // Check each active spell
        foreach (var kvp in new Dictionary<ulong, ActiveSpellData>(activeSpells))
        {
            ulong casterClientId = kvp.Key;
            ActiveSpellData spellData = kvp.Value;
            string spell = spellData.spellName;

            Debug.Log($"[SpellEffects-OnPlayerHitBall] Checking spell '{spell}' from client {casterClientId}");

            // Fireball: knockback when OPPONENT hits the enchanted ball
            if (spell == "Fireball")
            {
                if (spellData.resetOnOppHit && ballOwner != null && hitter != ballOwner && ballOwner == spellData.caster)
                {
                    Debug.Log($"[SpellEffects-OnPlayerHitBall] Fireball triggered!");

                    ulong victimClientId = hitter.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    if (victimClientId != ulong.MaxValue)
                    {
                        ApplyFireballKnockbackServerRpc(casterClientId, victimClientId);
                    }
                }
            }

            // Shadow: reset when opponent hits
            if (spell == "Shadow")
            {
                if (spellData.resetOnOppHit && ballOwner != null && hitter != ballOwner && ballOwner == spellData.caster)
                {
                    Debug.Log($"[SpellEffects-OnPlayerHitBall] Shadow broken!");
                    resetSpellEffect(casterClientId);
                }
            }

            // Mud: spawn pit when opponent hits
            if (spell == "Mud")
            {
                if (spellData.resetOnOppHit && ballOwner != null && hitter != ballOwner && ballOwner == spellData.caster)
                {
                    Debug.Log($"[SpellEffects-OnPlayerHitBall] Mud triggered!");

                    GameObject ballObject = GameObject.FindWithTag("Ball");
                    if (ballObject != null)
                    {
                        Vector3 spawnPos = ballObject.transform.position;
                        spawnPos.y = 0.94f;

                        ulong victimClientId = hitter.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                        if (victimClientId != ulong.MaxValue)
                        {
                            SpawnEffectServerRpc("Mud", casterClientId, victimClientId, spawnPos, Quaternion.identity);
                            resetSpellEffect(casterClientId);
                        }
                    }
                }
            }
        }
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
    /// Schedule a reset for a specific caster's spell
    /// </summary>
    private void ScheduleReset(ulong casterClientId, float delay)
    {
        StartCoroutine(ScheduledResetCoroutine(casterClientId, delay));
    }

    private IEnumerator ScheduledResetCoroutine(ulong casterClientId, float delay)
    {
        yield return new WaitForSeconds(delay);
        resetSpellEffect(casterClientId);
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

    /// <summary>
    /// Finds the Jolly child object on a player
    /// Searches by name pattern to handle different naming conventions
    /// </summary>
    private GameObject FindJollyObject(GameObject player)
    {
        if (player == null)
        {
            Debug.LogError("[SpellEffects] FindJollyObject: player is null");
            return null;
        }

        // Search patterns for Jolly object
        string[] jollyNames = new string[] { "Jolly", "jolly", "JollyObject", "Jolly Object", "JollyPrefab" };

        // Search all children (including inactive)
        Transform[] allChildren = player.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            // Check exact matches first
            foreach (string name in jollyNames)
            {
                if (child.name == name)
                {
                    Debug.Log($"[SpellEffects] Found Jolly object (exact match): {child.name}");
                    return child.gameObject;
                }
            }

            // Check contains matches
            foreach (string name in jollyNames)
            {
                if (child.name.Contains(name))
                {
                    Debug.Log($"[SpellEffects] Found Jolly object (contains match): {child.name}");
                    return child.gameObject;
                }
            }
        }

        Debug.LogWarning($"[SpellEffects] Could not find Jolly object on {player.name}");
        Debug.LogWarning($"[SpellEffects] Searched {allChildren.Length} children. Please name the Jolly child object 'Jolly' or 'JollyObject'");

        return null;
    }

    /// <summary>
    /// Finds the racket transform on a player
    /// Uses multiple search methods for reliability
    /// </summary>
    private Transform FindRacketTransform(GameObject player)
    {
        if (player == null)
        {
            Debug.LogError("[SpellEffects] FindRacketTransform: player is null");
            return null;
        }

        Transform racketTransform = null;

        // METHOD 1: Try to find by name (most reliable)
        Transform[] allChildren = player.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name.Contains("Racket") || child.name.Contains("racket") ||
                child.name.Contains("Hand.R") || child.name.Contains("HandR"))
            {
                racketTransform = child;
                Debug.Log($"[SpellEffects] Found racket by name: {child.name}");
                return racketTransform;
            }
            else
            {
                Debug.Log("Failed to Find Racket - Method 1");
            }
        }

        // METHOD 2: Fallback to hierarchy path
        Transform body = player.transform.Find("Body");
        if (body != null)
        {
            // Navigate down to racket hand
            racketTransform = body.Find("Armature")?.Find("Hips")?.Find("Spine")?.Find("Spine.001")
                ?.Find("Shoulder.R")?.Find("Upper_Arm.R")?.Find("Forearm.R")?.Find("Hand.R");

            if (racketTransform != null)
            {
                Debug.Log($"[SpellEffects] Found racket via hierarchy at {racketTransform.position}");
                return racketTransform;
            }
            else
            {
                Debug.Log("Failed to Find Racket - Method 2");
            }
        }

        // METHOD 3: Last resort - search for any transform with "Hand" in name
        foreach (Transform child in allChildren)
        {
            if (child.name.Contains("Hand"))
            {
                racketTransform = child;
                Debug.LogWarning($"[SpellEffects] Found potential racket (fallback): {child.name}");
                return racketTransform;
            }
            else
            {
                Debug.Log("Failed to Find Racket - Method 3");
            }
        }

        Debug.LogWarning($"[SpellEffects] Could not find racket transform on {player.name}");
        return null;
    }

    /// <summary>
    /// Hides or shows the racket mesh
    /// </summary>
    private void HideRacketMesh(GameObject player, bool hide)
    {
        if (player == null)
        {
            Debug.LogWarning("[SpellEffects] HideRacketMesh: player is null");
            return;
        }

        // Try multiple paths to find racket mesh
        Transform racketMesh = null;

        // Path 1: Standard hierarchy path
        racketMesh = player.transform.GetChild(2)?.GetChild(0)?.GetChild(4)?.GetChild(0)?.GetChild(0);

        // Path 2: Search by name if path 1 fails
        if (racketMesh == null)
        {
            Transform[] allChildren = player.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name.Contains("RacketMesh") || child.name.Contains("Racket_Mesh") ||
                    child.name.Contains("racketmesh"))
                {
                    racketMesh = child;
                    Debug.Log($"[SpellEffects] Found racket mesh by name: {child.name}");
                    break;
                }
            }
        }

        if (racketMesh != null)
        {
            racketMesh.gameObject.SetActive(!hide);
            Debug.Log($"[SpellEffects] Racket mesh {(hide ? "hidden" : "shown")}");
        }
        else
        {
            Debug.LogWarning($"[SpellEffects] Could not find racket mesh on {player.name}");
        }
    }

    // BACKWARDS COMPATIBILITY SHIT

    /// <summary>
    /// BACKWARD COMPATIBILITY: Old method signature
    /// Attempts to find caster client ID from current player
    /// </summary>
    public void castSpell()
    {
        // Try to determine which client is calling this
        GameObject player = GetPlayer();
        if (player != null)
        {
            ulong clientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
            if (clientId != ulong.MaxValue)
            {
                // Find the spell name from context (this is fragile)
                string spell = null;
                foreach (var kvp in activeSpells)
                {
                    if (kvp.Value.caster == player)
                    {
                        spell = kvp.Value.spellName;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(spell))
                {
                    castSpell(clientId, spell);
                    return;
                }
            }
        }

        Debug.LogError("[SpellEffects] castSpell() called without context - use castSpell(ulong casterClientId, string spell) instead");
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY: Old method signature
    /// </summary>
    public void resetSpellEffect()
    {
        GameObject player = GetPlayer();
        if (player != null)
        {
            ulong clientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
            if (clientId != ulong.MaxValue)
            {
                resetSpellEffect(clientId);
                return;
            }
        }

        Debug.LogWarning("[SpellEffects] resetSpellEffect() called without context");
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY: Old SetContext signature
    /// </summary>
    public void SetContext(GameObject player, GameObject opponent, TennisAI ai)
    {
        if (player != null)
        {
            ulong clientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
            if (clientId != ulong.MaxValue)
            {
                SetContext(clientId, player, opponent, ai);
                return;
            }
        }

        Debug.LogError("[SpellEffects] SetContext() called without valid player NetworkObject");
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY: Old AutoSetContext signature
    /// </summary>
    public void AutoSetContext(GameObject ballOwner)
    {
        if (ballOwner != null)
        {
            ulong clientId = ballOwner.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
            if (clientId != ulong.MaxValue)
            {
                AutoSetContext(clientId, ballOwner);
                return;
            }
        }

        Debug.LogWarning("[SpellEffects] AutoSetContext() called without valid ballOwner NetworkObject");
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY: Old ClearContext signature
    /// </summary>
    public void ClearContext()
    {
        // Clear all contexts (risky but maintains old behavior)
        activeSpells.Clear();
        Debug.LogWarning("[SpellEffects] ClearContext() cleared ALL active spells");
    }
}