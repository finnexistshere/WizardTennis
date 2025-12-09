using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// NetworkedSpellEffects - Complete spell system with server-authoritative spawning
/// Handles both networked and client-side spell effects
/// </summary>
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
    private GameObject Gorbino;

    // Track active networked effects for cleanup
    private Dictionary<string, ulong> activeNetworkEffects = new Dictionary<string, ulong>();

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
    }

    /// <summary>
    /// Client applies Shadow invisibility (only opponent sees it)
    /// </summary>
    [ClientRpc]
    private void ApplyShadowToOpponentClientRpc(ulong casterClientId, ulong opponentClientId)
    {
        // Only the OPPONENT client should apply invisibility
        // The caster should still see themselves normally
        if (NetworkManager.Singleton.LocalClientId != opponentClientId)
        {
            Debug.Log($"[SpellEffects-Client] Not the opponent ({NetworkManager.Singleton.LocalClientId} != {opponentClientId}), ignoring Shadow");
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
                renderer.gameObject.name.Contains("Effect"))
                continue;

            // Store original material if not already stored
            if (!shadowOriginalMaterials.ContainsKey(renderer))
            {
                shadowOriginalMaterials[renderer] = renderer.material;
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
    /// Server removes Shadow invisibility effect
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RemoveShadowInvisibilityServerRpc(ulong casterClientId, ulong opponentClientId)
    {
        if (!IsServer) return;

        Debug.Log($"[SpellEffects-Server] Removing Shadow invisibility");

        // Tell the OPPONENT client to restore visibility
        RemoveShadowFromOpponentClientRpc(casterClientId, opponentClientId);
    }

    /// <summary>
    /// Client removes Shadow invisibility
    /// </summary>
    [ClientRpc]
    private void RemoveShadowFromOpponentClientRpc(ulong casterClientId, ulong opponentClientId)
    {
        // Only the OPPONENT client should remove invisibility
        if (NetworkManager.Singleton.LocalClientId != opponentClientId)
        {
            return;
        }

        if (currentShadowCasterId != casterClientId)
        {
            Debug.LogWarning($"[SpellEffects-Client] Shadow caster mismatch: {currentShadowCasterId} != {casterClientId}");
            return;
        }

        Debug.Log($"[SpellEffects-Client] Removing Shadow invisibility");

        // Restore all original materials
        foreach (var kvp in shadowOriginalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.material = kvp.Value;
            }
        }

        shadowOriginalMaterials.Clear();
        currentShadowCasterId = ulong.MaxValue;

        Debug.Log($"[SpellEffects-Client] Shadow removed - visibility restored");
    }

    // Track Shadow spell materials per renderer
    private Dictionary<Renderer, Material> shadowOriginalMaterials = new Dictionary<Renderer, Material>();
    private ulong currentShadowCasterId = ulong.MaxValue;

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
                    StartCoroutine(FreezeOpponentMovement(target, 5f));
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
                    tetherComp.Player = target.transform;
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;

            case "Jolly":
                if (caster != null)
                {
                    Transform racketTransform = caster.transform.GetChild(2)?.GetChild(1)?.GetChild(0)?.GetChild(0)?.GetChild(1)?.GetChild(0)?.GetChild(0);
                    if (racketTransform != null)
                    {
                        Quaternion jollyRot = Quaternion.identity * Quaternion.Euler(0, -90, 90);
                        effectObj.transform.localRotation = jollyRot;
                        effectObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                        StartCoroutine(FollowTransform(effectObj, racketTransform, 5f));

                        // Hide racket mesh
                        Transform racketMesh = caster.transform.GetChild(2)?.GetChild(0)?.GetChild(4)?.GetChild(0)?.GetChild(0);
                        if (racketMesh != null)
                            racketMesh.gameObject.SetActive(false);
                    }
                }
                StartCoroutine(DespawnEffectAfterDelay(spell, effectObj, 5f));
                break;

            case "Mud":
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
                // This is probably PlayerSpawner — skip
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

        // Get client IDs for network spawning
        ulong casterClientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
        ulong targetClientId = opponent?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

        switch (spellName)
        {
            case "Lightning":
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null) mcm.speed = 17;
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Ice":
                if (opponent != null && iceBlockPrefab != null)
                {
                    // Request server to spawn ice block
                    SpawnEffectServerRpc("Ice", casterClientId, targetClientId, opponent.transform.position, opponent.transform.rotation);
                }
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Fireball":
                // Fireball: Mark the ball so when opponent hits it, they get knocked back
                // The knockback is triggered in NetworkedBall.CheckFireballHit()
                resetOnOppHit = true;

                // Mark the ball with the caster's info so we know who cast Fireball
                GameObject ballObj = GameObject.FindWithTag("Ball");
                if (ballObj != null)
                {
                    CollisionTrackerBall tracker = ballObj.GetComponent<CollisionTrackerBall>();
                    if (tracker != null)
                    {
                        // Mark that this is OUR fireball (not opponent's)
                        tracker.LastHitWizard = "Player";
                    }
                }

                Debug.Log($"[SpellEffects] Fireball armed - when opponent hits the ball, they'll be knocked back");
                break;

            case "Shadow":
                if (!oppHitSpell)
                {
                    oppHitSpell = true;
                }
                else
                {
                    // Make caster invisible to OPPONENT only (not to self)
                    // Request server to apply invisibility
                    if (opponent != null)
                    {
                        ApplyShadowInvisibilityServerRpc(casterClientId, targetClientId);
                    }

                    resetOnOppHit = true;
                }
                break;

            case "Green":
                var greenBall = player.GetComponent<NetworkedBall>();
                if (greenBall != null) greenBall.green = true;
                Invoke(nameof(resetSpellEffect), 1f);
                break;

            case "Stone":
                if (player != null && stoneWallPrefab != null)
                {
                    Vector3 spawnPos = player.transform.position + player.transform.forward * 2f;
                    SpawnEffectServerRpc("Stone", casterClientId, targetClientId, spawnPos, Quaternion.identity);
                }
                break;

            case "Chronos":
                StartCoroutine(ApplyChronosAfterExplanation());
                break;

            case "Gemini":
                if (player != null && geminiPrefab != null)
                {
                    Vector3 gemPos = player.transform.position;
                    gemPos.x = -player.transform.position.x;
                    SpawnEffectServerRpc("Gemini", casterClientId, targetClientId, gemPos, player.transform.rotation);
                }
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Blink":
                // Client-side teleport (no network needed)
                PerformBlink(player);
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Jolly":
                if (player != null && jollyPrefab != null)
                {
                    // Expand collider
                    var capsule = player.GetComponent<CapsuleCollider>();
                    if (capsule != null) capsule.radius = 2;

                    Transform racketTransform = player.transform.GetChild(2)?.GetChild(1)?.GetChild(0)?.GetChild(0)?.GetChild(1)?.GetChild(0)?.GetChild(0);
                    if (racketTransform != null)
                    {
                        SpawnEffectServerRpc("Jolly", casterClientId, targetClientId, racketTransform.position, Quaternion.identity);
                    }
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
                if (player != null && orbiterPrefab != null)
                {
                    SpawnEffectServerRpc("Pisces", casterClientId, targetClientId, player.transform.position, player.transform.rotation);
                }
                break;

            case "Tether":
                if (opponent != null && tetherPrefab != null)
                {
                    Vector3 tetherPos = opponent.transform.position;
                    tetherPos.y = 1.45f;
                    SpawnEffectServerRpc("Tether", casterClientId, targetClientId, tetherPos, Quaternion.identity);
                }
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Gorbino":
                Gorbino = GameObject.Find("Gorbino");
                if (Gorbino != null && BallPrefab != null)
                {
                    SpawnEffectServerRpc("Gorbino", casterClientId, targetClientId, Gorbino.transform.position, Quaternion.identity);
                    Gorbino.SetActive(false);
                }
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Gambit":
                int spellInt = Random.Range(0, allSpells.Length);
                spellName = allSpells[spellInt];
                castSpell();
                return;
        }

        // Visual casting is already handled by NetworkedSpellcasting.CastSpellServerRpc
        // No need to call CastSpellNormal here - it would cause duplicate visual spawning
        // The ball visual is managed by NetworkedSpellcasting, not SpellEffects
    }

    private void PerformBlink(GameObject player)
    {
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
                    var oppMcm = opponent.GetComponent<MainCharacterMovement>();
                    if (oppMcm != null && oppMcm.speed == 0f)
                    {
                        oppMcm.speed = 7;
                        Debug.Log($"[SpellEffects] Ice Reset: Restored opponent speed");
                    }
                }
                // Ice block despawn is handled by DespawnEffectAfterDelay
                activeIceBlock = null;
                break;

            case "Fireball":
                // Spawn flame effect on opponent
                if (flamePrefab != null && opponent != null)
                {
                    ulong casterClientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    ulong targetClientId = opponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    SpawnEffectServerRpc("Flame", casterClientId, targetClientId, opponent.transform.position, Quaternion.identity);
                }

                resetOnOppHit = false;
                break;

            case "Shadow":
                resetOnOppHit = false;

                // Remove Shadow invisibility via server
                if (player != null && opponent != null)
                {
                    ulong casterClientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                    ulong opponentClientId = opponent.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    RemoveShadowInvisibilityServerRpc(casterClientId, opponentClientId);
                }

                // Clean up any local references
                opponentRenderer = null;
                originalOpponentMaterial = null;
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
                resetOnOppHit = false;
                resetOnBounce = false;

                if (mudPrefab != null)
                {
                    GameObject Ball = GameObject.FindWithTag("Ball");
                    if (Ball != null)
                    {
                        Vector3 spawnPos = Ball.transform.position;
                        spawnPos.y = 0.94f;

                        ulong casterClientId = player.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                        SpawnEffectServerRpc("Mud", casterClientId, ulong.MaxValue, spawnPos, Quaternion.identity);
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

    private IEnumerator FreezeOpponentMovement(GameObject target, float duration)
    {
        if (target == null) yield break;

        var mcm = target.GetComponent<MainCharacterMovement>();
        float originalMcmSpeed = 7f;

        if (mcm != null)
            originalMcmSpeed = mcm.speed;

        if (mcm != null)
        {
            mcm.speed = 0f;
            Debug.Log($"[SpellEffects] Froze {target.name} movement");
        }

        yield return new WaitForSeconds(duration);

        if (mcm != null)
        {
            mcm.speed = originalMcmSpeed;
            Debug.Log($"[SpellEffects] Restored {target.name} movement to {originalMcmSpeed}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyFireballKnockbackServerRpc(ulong victimClientId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Get the actual caster from context (who originally cast Fireball)
        GameObject caster = currentPlayer; // This is set by SetContext when spell was cast

        // Find the victim by their client ID
        GameObject victim = GetPlayerByClientId(victimClientId);

        if (victim == null)
        {
            Debug.LogWarning($"[SpellEffects-Server] Fireball knockback - victim not found for client {victimClientId}");
            return;
        }

        Debug.Log($"[SpellEffects-Server] Applying Fireball knockback to {victim.name}");

        // Calculate knockback direction
        Vector3 knockbackDirection = Vector3.back; // Default fallback

        if (caster != null)
        {
            Vector3 directionFromCaster = (victim.transform.position - caster.transform.position).normalized;

            // Horizontal push away from caster
            Vector3 horizontal = new Vector3(directionFromCaster.x, 0, directionFromCaster.z).normalized;

            // Add upward lift
            Vector3 upward = Vector3.up * 0.75f;

            knockbackDirection = (horizontal + upward).normalized;
        }

        // Strong knockback force
        float forceStrength = 20f;
        Vector3 knockbackForce = knockbackDirection * forceStrength;

        // Longer push duration
        float duration = 0.45f;

        // Apply knockback on server
        StartCoroutine(ApplyKnockback(victim, knockbackForce, duration));

        // Tell all clients to also apply knockback for smooth visuals
        ApplyFireballKnockbackClientRpc(victimClientId, knockbackDirection, forceStrength, duration);
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
        StartCoroutine(ApplyKnockback(victim, knockbackForce, duration));
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
        var controller = target.GetComponent<CharacterController>();
        var movement = target.GetComponent<MainCharacterMovement>();

        if (controller == null)
        {
            Debug.LogWarning($"[SpellEffects] Knockback failed: No CharacterController on {target.name}");
            yield break;
        }

        bool wasMovementEnabled = true;
        if (movement != null)
        {
            wasMovementEnabled = movement.enabled;
            movement.enabled = false;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration);

            Vector3 frameKnockback = knockbackForce * t * Time.deltaTime;
            controller.Move(frameKnockback);

            yield return null;
        }

        if (movement != null)
            movement.enabled = wasMovementEnabled;
    }

    private IEnumerator ApplyChronosAfterExplanation()
    {
        GameObject player = GetPlayer();
        if (player == null) yield break;

        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);
        yield return new WaitForSecondsRealtime(0.05f);

        lastOriginalTimeScale = Time.timeScale;
        isSpellSlowdownActive = true;
        Time.timeScale = 0.1f;

        var mcm = player.GetComponent<MainCharacterMovement>();
        if (mcm != null)
        {
            mcm.speed = 4f;
            mcm.gravity = 5f;
        }

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

        Time.timeScale = lastOriginalTimeScale;
        isSpellSlowdownActive = false;

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
        if (orbiter == null || center == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (orbiter == null || center == null) yield break;
            orbiter.transform.RotateAround(center.position, Vector3.up, 180f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Despawn is handled by DespawnEffectAfterDelay
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

    public void OnPlayerHitOpponent(GameObject hitter, GameObject victim)
    {
        if (string.IsNullOrEmpty(spellName)) return;

        if (resetOnOppHit)
        {
            if (spellName == "Fireball")
            {
                if (victim != null)
                {
                    Debug.Log("[SpellEffects] Fireball hit - requesting knockback via ServerRpc");

                    ulong victimClientId = victim.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                    if (victimClientId != ulong.MaxValue)
                    {
                        // Request server to apply knockback
                        ApplyFireballKnockbackServerRpc(victimClientId);
                    }
                    else
                    {
                        Debug.LogError("[SpellEffects] Cannot apply Fireball knockback - victim has no NetworkObject");
                    }
                }
                else
                {
                    Debug.LogError("[SpellEffects] Cannot Apply Knockback! Victim is Null!");
                }
            }

            resetSpellEffect();
        }

        if (spellName == "Mud")
        {
            if (mudPrefab != null && victim != null)
            {
                ulong casterClientId = hitter?.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;
                ulong targetClientId = victim.GetComponent<NetworkObject>()?.OwnerClientId ?? ulong.MaxValue;

                SpawnEffectServerRpc("Mud", casterClientId, targetClientId, victim.transform.position, Quaternion.identity);
            }

            resetSpellEffect();
        }
    }
}