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

    private Vector3 storedBlinkDirection = Vector3.zero;

    private Coroutine activeLightningCoroutine = null;

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

        switch (spellName)
        {
            case "Lightning":
                var mcm = player.GetComponent<MainCharacterMovement>();
                if (mcm != null)
                {
                    // Stop any existing lightning effect
                    if (activeLightningCoroutine != null)
                    {
                        StopCoroutine(activeLightningCoroutine);
                    }

                    // Start new lightning effect
                    activeLightningCoroutine = StartCoroutine(ApplyLightningSpeed(mcm, 5f));
                    Debug.Log("[SpellEffects] Lightning speed boost applied");
                }
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
                resetOnOppHit = true;  // This tells the system to reset when opponent hits
                resetOnPlrHit = false; // Don't reset when player hits

                // Mark the ball with Fireball status
                GameObject fireballBallObj = GameObject.FindWithTag("Ball");
                if (fireballBallObj != null)
                {
                    CollisionTrackerBall tracker = fireballBallObj.GetComponent<CollisionTrackerBall>();
                    if (tracker != null)
                    {
                        tracker.LastHitWizard = "Player"; // Mark as our ball
                    }
                }

                Debug.Log("[SpellEffects] Fireball armed - opponent will be knocked back on hit");
                break;

            case "Shadow":
                if (opponent != null)
                {
                    Debug.Log($"[SpellEffects] Casting Shadow - making {player.name} invisible to {opponent.name}");
                    ApplyShadowInvisibilityServerRpc(casterClientId, targetClientId);
                    resetOnOppHit = true;
                }
                else
                {
                    Debug.LogWarning("[SpellEffects] Shadow cast failed - no opponent found");
                }
                break;

            case "Green":
                var greenBall = player.GetComponent<NetworkedBall>();
                if (greenBall != null)
                {
                    greenBall.green = true;

                    // Update UI on caster's client
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

                // Green lasts indefinitely until resetF
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
                // Store the current movement direction WHEN THE SPELL IS CAST
                var blinkMCM = player.GetComponent<MainCharacterMovement>();
                if (blinkMCM != null && blinkMCM.controller != null)
                {
                    // Get current velocity from CharacterController
                    Vector3 velocity = blinkMCM.controller.velocity;
                    velocity.y = 0; // Ignore vertical component

                    if (velocity.magnitude > 0.1f)
                    {
                        // Player is moving - blink in that direction
                        storedBlinkDirection = velocity.normalized * 3f;
                    }
                    else
                    {
                        // Player is stationary - blink forward
                        storedBlinkDirection = player.transform.forward * 3f;
                        storedBlinkDirection.y = 0;
                    }

                    Debug.Log($"[SpellEffects] Blink direction stored: {storedBlinkDirection}");
                }
                else
                {
                    // Fallback - blink forward
                    storedBlinkDirection = player.transform.forward * 3f;
                    storedBlinkDirection.y = 0;
                }

                // Perform blink immediately
                PerformBlink(player);
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Jolly":
                if (player != null && jollyPrefab != null)
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
                Invoke(nameof(resetSpellEffect), 5f);
                break;

            case "Mud":
                resetOnOppHit = true;  // Will spawn mud when opponent hits
                resetOnBounce = true;  // Will spawn mud when ball bounces
                Debug.Log("[SpellEffects] Mud spell armed");
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

                    Debug.Log($"[SpellEffects] Spawning Tether at {tetherPos} for opponent {opponent.name}");

                    SpawnEffectServerRpc("Tether", casterClientId, targetClientId, tetherPos, Quaternion.identity);
                }
                else
                {
                    Debug.LogWarning($"[SpellEffects] Tether spawn failed - opponent: {opponent != null}, prefab: {tetherPrefab != null}");
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
                GameObject warpBall = GameObject.FindWithTag("Ball");

                if (warpBall != null)
                {
                    // Disable physics temporarily
                    Rigidbody ballRb = warpBall.GetComponent<Rigidbody>();
                    bool hadGravity = true;

                    if (ballRb != null)
                    {
                        hadGravity = ballRb.useGravity;
                        ballRb.useGravity = false;
                        ballRb.linearVelocity = Vector3.zero;
                    }

                    // Warp position
                    Vector3 ballPos = warpBall.transform.position;
                    ballPos.x = ballX;
                    warpBall.transform.position = ballPos;

                    Debug.Log($"[SpellEffects] Warped ball to x={ballX}");

                    // Re-enable physics
                    if (ballRb != null)
                    {
                        ballRb.useGravity = hadGravity;
                    }
                }
                else
                {
                    Debug.LogWarning("[SpellEffects] Warp failed - ball not found");
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

    private IEnumerator ApplyLightningSpeed(MainCharacterMovement mcm, float duration)
    {
        if (mcm == null)
        {
            Debug.LogWarning("[SpellEffects] Lightning: MCM is null");
            yield break;
        }

        float originalSpeed = mcm.speed;
        float boostedSpeed = 17f;

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
        if (player == null)
        {
            Debug.LogError("[SpellEffects] Chronos failed - no player found");
            yield break;
        }

        // Wait for any existing slowdown to finish
        yield return new WaitUntil(() => !SpellEffects.isSpellSlowdownActive);
        yield return new WaitForSecondsRealtime(0.05f);

        lastOriginalTimeScale = Time.timeScale;
        isSpellSlowdownActive = true;

        // Apply time scale
        Time.timeScale = 0.1f;
        Debug.Log($"[SpellEffects] Chronos applied - timeScale: {Time.timeScale}");

        var mcm = player.GetComponent<MainCharacterMovement>();
        if (mcm != null)
        {
            mcm.speed = 4f;
            mcm.gravity = 5f;
            Debug.Log($"[SpellEffects] Chronos movement adjusted - speed: {mcm.speed}, gravity: {mcm.gravity}");
        }

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
        float spinSpeed = -360f; // degrees per second

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

            // --- MIRROR POSITION (optional, matches your singleplayer logic) ---
            Vector3 mirroredPos = casterT.position;
            mirroredPos.x = -mirroredPos.x;
            geminiT.position = mirroredPos;

            // --- MATCH ROTATION ---
            geminiT.rotation = casterT.rotation;

            yield return null;
        }
    }
}