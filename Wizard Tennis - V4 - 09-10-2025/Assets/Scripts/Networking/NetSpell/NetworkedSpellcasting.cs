using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedSpellcasting : NetworkBehaviour, ISpellcasting
{
    // --- Racket Shader Reference ---
    [SerializeField] public Material racketShader;

    // --- Spell Dictionaries ---
    public Dictionary<string, string> spellBook { get; private set; } = new Dictionary<string, string>();
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();
    public Dictionary<string, GameObject> spellVisuals = new Dictionary<string, GameObject>();
    public Dictionary<string, bool> boolBook = new Dictionary<string, bool>();
    public Dictionary<string, Color> spellColors = new Dictionary<string, Color>();
    public Dictionary<string, Color> spellColors2 = new Dictionary<string, Color>();
    public Dictionary<string, AudioClip> spellAudio = new Dictionary<string, AudioClip>();
    public Dictionary<string, AudioClip> wizardAudio = new Dictionary<string, AudioClip>();
    public Dictionary<string, float> spellDurations = new Dictionary<string, float>();

    // --- UI References ---
    [Header("UI References")]
    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public SpellTextEntry spellTextPrefab;

    // --- Ball Visuals ---
    [Header("Ball Visual")]
    public GameObject parentObject;
    public GameObject baseEffectObject;
    private GameObject currentVisualInstance;

    [Header("Spell Settings")]
    public float spellDuration = 5f;

    [Header("Particle Systems")]
    public ParticleSystem hitParticle;
    public SpellParticleColor spellParticleColor;
    public float inputTimeout = 2f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip SpellInputClick;
    public AudioClip spellRegisterSound;

    // --- Linked Components ---
    public TennisAI TennisAi;
    public SpellFloorImage spellFloorImage;
    public NetworkedSpellEffects SpellEffects;

    // --- Internal State ---
    private string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false;
    private GameObject currentBall;

    [Header("Settings")]
    public bool leftHandedMode = false;

    private float ballCheckInterval = 0.5f;
    private float nextBallCheckTime = 0f;

    private NetworkedUIManager uiManager;

    private void Awake()
    {
        AutoSetupReferences();
        InitializeRacketShader();
    }

    public override void OnNetworkSpawn()
    {
        // Only the owner needs to find the ball for visuals
        if (IsOwner)
        {
            StartCoroutine(DelayedBallSearch());

            uiManager = GetComponent<NetworkedUIManager>();
            if (uiManager == null)
                Debug.LogWarning($"[NetworkedSpellcasting] Player {OwnerClientId} missing NetworkedUIManager!");
        }
    }

    private IEnumerator DelayedBallSearch()
    {
        // Wait a bit for all network objects to spawn
        yield return new WaitForSeconds(0.5f);

        // Force ball search
        currentBall = null;
        TryFindAndLinkBall();

        // Try again if failed
        if (currentBall == null)
        {
            yield return new WaitForSeconds(1f);
            TryFindAndLinkBall();
        }
    }

    private void AutoSetupReferences()
    {
        if (racketShader == null)
        {
            var mats = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in mats)
            {
                if (mat.name.Contains("Racket"))
                {
                    racketShader = mat;
                    break;
                }
            }
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (SpellEffects == null)
            SpellEffects = FindObjectOfType<NetworkedSpellEffects>();

        if (spellFloorImage == null)
            spellFloorImage = FindObjectOfType<SpellFloorImage>();

        if (spellParticleColor == null)
            spellParticleColor = FindObjectOfType<SpellParticleColor>();

        if (TennisAi == null)
            TennisAi = FindObjectOfType<TennisAI>();

        if (spellBookPanel == null)
            spellBookPanel = GameObject.Find("SpellBook");

        if (spellAddressText == null)
            spellAddressText = FindObjectOfType<TextMeshProUGUI>();

        if (spellTextPrefab == null)
            spellTextPrefab = Resources.Load<SpellTextEntry>("SpellTextEntry");

        if (hitParticle == null)
            hitParticle = FindObjectOfType<ParticleSystem>();
    }

    private void InitializeRacketShader()
    {
        if (racketShader != null && racketShader.HasProperty("_Racket_Color_Top") && racketShader.HasProperty("_Racket_Color_Bottom"))
        {
            racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
            racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
        }
    }

    private void Update()
    {
        // Only owner needs ball for visuals and input
        if (!IsOwner) return;

        // Periodic ball check for owner only
        if (Time.time >= nextBallCheckTime)
        {
            nextBallCheckTime = Time.time + ballCheckInterval;
            TryFindAndLinkBall();
        }

        if (isCasting) return;

        UpdateSpellBook();

        if (!string.IsNullOrEmpty(inputSpellAddress) && Time.time - lastInputTime >= inputTimeout)
        {
            inputSpellAddress = "";
            UpdateSpellBook();
        }

        if (CheckSpellInput(out string direction))
            RegisterInput(direction);

        if (!string.IsNullOrEmpty(inputSpellAddress) && spellBook.ContainsKey(inputSpellAddress))
            CheckSpell();
    }

    private void TryFindAndLinkBall()
    {
        // Only owner needs ball reference for visuals
        if (!IsOwner) return;

        // Check if current ball is still valid
        if (currentBall != null && currentBall.activeInHierarchy)
            return;

        // Need to find a new ball
        currentBall = null;

        // Try multiple methods to find the ball
        GameObject ballObj = GameObject.FindWithTag("Ball");

        // Fallback: try finding by name
        if (ballObj == null)
            ballObj = GameObject.Find("Ball");

        // Fallback: search for Ball component
        if (ballObj == null)
        {
            Ball ballComponent = FindObjectOfType<Ball>();
            if (ballComponent != null)
                ballObj = ballComponent.gameObject;
        }

        // Fallback: search for NetworkObject with ball-like name
        if (ballObj == null)
        {
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.gameObject.name.ToLower().Contains("ball"))
                {
                    ballObj = netObj.gameObject;
                    break;
                }
            }
        }

        if (ballObj == null)
        {
            // Only log occasionally to avoid spam
            if (Time.frameCount % 100 == 0)
                Debug.LogWarning($"[NetworkedSpellcasting] Owner Client {OwnerClientId} cannot find Ball.");
            return;
        }

        currentBall = ballObj;
        parentObject = ballObj;

        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
        {
            baseEffectObject = visualChild.gameObject;
        }
        else
        {
            // Try alternative child names
            for (int i = 0; i < ballObj.transform.childCount; i++)
            {
                Transform child = ballObj.transform.GetChild(i);
                if (child.name.ToLower().Contains("ball"))
                {
                    baseEffectObject = child.gameObject;
                    break;
                }
            }
        }

        Debug.Log($"[NetworkedSpellcasting] Owner Client {OwnerClientId} connected to Ball: {ballObj.name}. BaseEffect: {(baseEffectObject != null ? baseEffectObject.name : "NULL")}");
    }

    private bool CheckSpellInput(out string direction)
    {
        direction = "";
        KeyCode leftKey = leftHandedMode ? KeyCode.A : KeyCode.LeftArrow;
        KeyCode rightKey = leftHandedMode ? KeyCode.D : KeyCode.RightArrow;
        KeyCode upKey = leftHandedMode ? KeyCode.W : KeyCode.UpArrow;
        KeyCode downKey = leftHandedMode ? KeyCode.S : KeyCode.DownArrow;

        if (Input.GetKeyDown(leftKey)) direction = "a";
        if (Input.GetKeyDown(rightKey)) direction = "A";
        if (Input.GetKeyDown(upKey)) direction = "B";
        if (Input.GetKeyDown(downKey)) direction = "b";

        return !string.IsNullOrEmpty(direction);
    }

    private void RegisterInput(string direction)
    {
        audioSource?.PlayOneShot(SpellInputClick);
        inputSpellAddress += direction;
        lastInputTime = Time.time;
        UpdateSpellBook();
    }

    private void CheckSpell()
    {
        if (!spellBook.ContainsKey(inputSpellAddress) || isCasting) return;

        string spellName = spellBook[inputSpellAddress];
        isCasting = true;
        currentActiveSpell = spellName;

        audioSource?.PlayOneShot(spellRegisterSound);

        // Get opponent NetworkObjectId for reliable network lookup
        GameObject opponent = FindOpponent();
        ulong opponentNetId = opponent != null && opponent.TryGetComponent<NetworkObject>(out var netObj)
            ? netObj.NetworkObjectId
            : ulong.MaxValue;

        // Request server to cast spell (server-authoritative)
        CastSpellServerRpc(inputSpellAddress, spellName, opponentNetId);

        float duration = spellDurations.ContainsKey(inputSpellAddress) ? spellDurations[inputSpellAddress] : spellDuration;

        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, inputSpellAddress));
        inputSpellAddress = "";
        UpdateSpellBook();
    }

    // ---------- Helper: lookup spawned object by NetworkObjectId ----------
    private GameObject GetSpawnedObjectByNetId(ulong netId)
    {
        if (NetworkManager.Singleton == null) return null;
        if (netId == 0 || netId == ulong.MaxValue) return null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out var netObj) && netObj != null)
            return netObj.gameObject;
        return null;
    }

    // ---------- Helper: server-side spawn, returns NetworkObjectId (0 on failure) ----------
    private ulong SpawnPrefabOnServer(GameObject prefab, Vector3 pos, Quaternion rot, ulong ownerClientId = ulong.MaxValue, bool giveOwnershipToSender = false)
    {
        if (prefab == null) return 0;
        if (NetworkManager.Singleton == null) return 0;
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NetworkedSpellcasting] SpawnPrefabOnServer called on non-server.");
            return 0;
        }

        var instance = Instantiate(prefab, pos, rot);
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            // Non-networked prefab — just return 0 (clients will not see this as a network object)
            return 0;
        }

        // Choose ownership if requested (useful for visuals owned by the caster)
        if (giveOwnershipToSender && ownerClientId != ulong.MaxValue)
        {
            netObj.SpawnWithOwnership(ownerClientId);
        }
        else
        {
            netObj.Spawn();
        }

        return netObj.NetworkObjectId;
    }

    // ---------- ServerRpc: validate and spawn networked effects (server authoritative) ----------
    [ServerRpc(RequireOwnership = false)]
    private void CastSpellServerRpc(string spellAddress, string spellName, ulong opponentNetId, ServerRpcParams rpcParams = default)
    {
        // Who requested the cast?
        ulong casterClientId = rpcParams.Receive.SenderClientId;

        // Server resolves caster/opponent NetworkObjects
        GameObject caster = null;
        GameObject opponent = null;

        // Try to find the caster by spawn manager ownership
        foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
        {
            if (kv.Value != null && kv.Value.OwnerClientId == casterClientId)
            {
                caster = kv.Value.gameObject;
                break;
            }
        }

        // Resolve opponent by network id if present
        if (opponentNetId != ulong.MaxValue)
            opponent = GetSpawnedObjectByNetId(opponentNetId);

        // As fallback, try finding another NetworkedSpellcasting object
        if (caster == null)
        {
            var all = FindObjectsOfType<NetworkedSpellcasting>();
            foreach (var sc in all)
            {
                if (sc.OwnerClientId == casterClientId)
                {
                    caster = sc.gameObject;
                    break;
                }
            }
        }
        if (opponent == null)
        {
            // pick any other networked player object
            var all = FindObjectsOfType<NetworkedSpellcasting>();
            foreach (var sc in all)
            {
                if (sc.gameObject != caster)
                {
                    opponent = sc.gameObject;
                    break;
                }
            }
        }

        // Now: spawn authoritative networked prefabs where appropriate, collect their netIds
        ulong visualNetId = 0;  // networked spell visual attached to ball (if any)
        ulong effectNetId = 0;  // specific networked effect instance (ice block, stone wall, etc.)

        // --- Spawn the spell visual prefab (if it's a NetworkObject) and attach it near the ball ---
        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (ballObj == null)
        {
            // fallback: try to find any object named Ball or use caster's child
            ballObj = GameObject.Find("Ball");
            if (ballObj == null && caster != null)
                ballObj = caster.GetComponent<NetworkedSpellcasting>()?.parentObject; // may be null
        }

        if (spellVisuals != null && spellVisuals.ContainsKey(spellAddress) && spellVisuals[spellAddress] != null && ballObj != null)
        {
            GameObject visualPrefab = spellVisuals[spellAddress];
            if (visualPrefab.GetComponent<NetworkObject>() != null)
            {
                // spawn server-side at ball position
                visualNetId = SpawnPrefabOnServer(visualPrefab, ballObj.transform.position, ballObj.transform.rotation, casterClientId, giveOwnershipToSender: false);
            }
        }

        // --- Spawn spell-specific networked "effect" prefabs (gameplay objects) ---
        switch (spellName)
        {
            case "Ice":
                if (opponent != null && SpellEffects != null && SpellEffects.iceBlockPrefab != null && SpellEffects.iceBlockPrefab.GetComponent<NetworkObject>() != null)
                {
                    effectNetId = SpawnPrefabOnServer(SpellEffects.iceBlockPrefab, opponent.transform.position, opponent.transform.rotation);
                }
                break;

            case "Stone":
                {
                    // Safety: verify prefab is valid
                    if (SpellEffects == null ||
                        SpellEffects.stoneWallPrefab == null ||
                        SpellEffects.stoneWallPrefab.GetComponent<NetworkObject>() == null)
                        break;

                    // Resolve the REAL caster based on client ID
                    GameObject trueCaster = GetRealCaster(casterClientId);

                    if (trueCaster == null)
                    {
                        Debug.LogWarning("[NetSpellCasting] Stone: Could not find real caster!");
                        break;
                    }

                    // Distance in front of the caster for the spawn
                    float forwardDistance = 2.0f;

                    // Clean forward (ignore vertical tilt)
                    Vector3 forward = trueCaster.transform.forward;
                    forward.y = 0f;
                    forward.Normalize();

                    // Compute spawn position
                    Vector3 spawnPos = trueCaster.transform.position + forward * forwardDistance;

                    // Compute rotation
                    Quaternion spawnRot = Quaternion.LookRotation(forward, Vector3.up);

                    Debug.Log($"[NetSpellCasting] Stone spawned at {spawnPos} for caster: {trueCaster.name}");

                    // Server authoritative spawn
                    effectNetId = SpawnPrefabOnServer(
                        SpellEffects.stoneWallPrefab,
                        spawnPos,
                        spawnRot
                    );
                }
                break;

            case "Gemini":
                if (caster != null && SpellEffects != null && SpellEffects.geminiPrefab != null && SpellEffects.geminiPrefab.GetComponent<NetworkObject>() != null)
                {
                    Vector3 gemPos = caster.transform.position;
                    gemPos.x = -caster.transform.position.x;
                    effectNetId = SpawnPrefabOnServer(SpellEffects.geminiPrefab, gemPos, caster.transform.rotation);
                }
                break;

            case "Pisces":
                if (caster != null && SpellEffects != null && SpellEffects.orbiterPrefab != null && SpellEffects.orbiterPrefab.GetComponent<NetworkObject>() != null)
                {
                    effectNetId = SpawnPrefabOnServer(SpellEffects.orbiterPrefab, caster.transform.position, caster.transform.rotation);
                }
                break;

            case "Tether":
                if (opponent != null && SpellEffects != null && SpellEffects.tetherPrefab != null && SpellEffects.tetherPrefab.GetComponent<NetworkObject>() != null)
                {
                    Vector3 tetherPos = opponent.transform.position;
                    tetherPos.y = 1.45f;
                    effectNetId = SpawnPrefabOnServer(SpellEffects.tetherPrefab, tetherPos, Quaternion.identity);
                }
                break;

            case "Gorbino":
                var gorb = GameObject.Find("Gorbino");
                if (gorb != null && SpellEffects != null && SpellEffects.BallPrefab != null && SpellEffects.BallPrefab.GetComponent<NetworkObject>() != null)
                {
                    effectNetId = SpawnPrefabOnServer(SpellEffects.BallPrefab, gorb.transform.position, Quaternion.identity);
                    gorb.SetActive(false);
                }
                break;

                // Add more server-spawn cases as needed
        }

        // Broadcast to clients with the spawned network IDs (0 means "none")
        CastSpellNetworkedClientRpc(spellAddress, spellName, casterClientId, opponentNetId, visualNetId, effectNetId);
    }

    private GameObject GetRealCaster(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (obj.OwnerClientId != clientId)
                continue;

            GameObject go = obj.gameObject;

            // Must be a player (have spellcasting OR movement)
            if (go.GetComponent<NetworkedSpellcasting>() != null)
            {
                return go;
            }
        }

        return null;
    }

    // ---------- Updated ClientRpc ----------
    [ClientRpc]
    private void CastSpellNetworkedClientRpc(string spellAddress, string spellName, ulong casterClientId, ulong opponentNetId, ulong visualNetId, ulong effectNetId)
    {
        Debug.Log($"[NetworkedSpellcasting] ClientRpc received: {spellName} for caster {casterClientId}");

        // Resolve caster/opponent locally (best-effort)
        GameObject caster = null;
        GameObject opponent = null;

        foreach (var sc in FindObjectsOfType<NetworkedSpellcasting>())
        {
            if (sc.OwnerClientId == casterClientId)
                caster = sc.gameObject;
            if (sc.GetComponent<NetworkObject>() != null && sc.GetComponent<NetworkObject>().NetworkObjectId == opponentNetId)
                opponent = sc.gameObject;
        }

        if (opponent == null)
            opponent = GetSpawnedObjectByNetId(opponentNetId);

        if (caster == null)
        {
            // fallback: local owner
            if (IsOwner) caster = gameObject;
        }

        // UI update: only on local caster client
        if (IsOwner && caster != null && caster.GetComponent<NetworkedSpellcasting>()?.OwnerClientId == OwnerClientId)
        {
            if (uiManager != null)
            {
                Color uiSpellColor = spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white;
                uiManager.UpdateSpellStatus(spellName, uiSpellColor);
            }
        }

        // === VISUAL HANDLING - WORKS ON ALL CLIENTS ===
        // Find the ball on THIS client
        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (ballObj == null)
            ballObj = GameObject.Find("Ball");
        if (ballObj == null)
        {
            Ball ballComponent = FindObjectOfType<Ball>();
            if (ballComponent != null)
                ballObj = ballComponent.gameObject;
        }

        if (ballObj == null)
        {
            Debug.LogWarning($"[NetworkedSpellcasting] Client couldn't find ball for visual!");
            return;
        }

        // Find the base effect on THIS client's ball
        GameObject localBaseEffect = null;
        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
        {
            localBaseEffect = visualChild.gameObject;
        }
        else
        {
            // Try alternative child names
            for (int i = 0; i < ballObj.transform.childCount; i++)
            {
                Transform child = ballObj.transform.GetChild(i);
                if (child.name.ToLower().Contains("ball"))
                {
                    localBaseEffect = child.gameObject;
                    break;
                }
            }
        }

        // Clean up any existing visual on THIS client
        if (currentVisualInstance != null)
        {
            NetworkObject netObj = currentVisualInstance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                // Client-side only visual, destroy it
                Destroy(currentVisualInstance);
            }
            // If it has NetworkObject, server will handle despawn
            currentVisualInstance = null;
        }

        // Check if server spawned a networked visual
        if (visualNetId != 0)
        {
            // Wait a frame for network object to be available
            StartCoroutine(WaitForNetworkedVisual(visualNetId, ballObj, localBaseEffect));
        }
        else if (spellVisuals != null && spellVisuals.ContainsKey(spellAddress) && spellVisuals[spellAddress] != null)
        {
            // No networked visual - spawn client-side visual
            var prefab = spellVisuals[spellAddress];
            try
            {
                Debug.Log($"[NetworkedSpellcasting] Spawning client-side visual for {spellName}");
                currentVisualInstance = Instantiate(prefab, ballObj.transform);
                currentVisualInstance.transform.localPosition = Vector3.zero;
                currentVisualInstance.transform.localRotation = Quaternion.identity;
                currentVisualInstance.transform.localScale = Vector3.one;

                // Disable base effect on THIS client
                if (localBaseEffect != null)
                {
                    localBaseEffect.SetActive(false);
                    Debug.Log($"[NetworkedSpellcasting] Disabled base effect on this client");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkedSpellcasting] Visual spawn failed: {e.Message}");
            }
        }

        // Racket shader (only for local caster)
        if (caster != null && caster.GetComponent<NetworkedSpellcasting>()?.OwnerClientId == OwnerClientId)
        {
            if (racketShader != null && spellColors.ContainsKey(spellAddress) && spellColors2.ContainsKey(spellAddress))
            {
                racketShader.SetColor("_Racket_Color_Top", spellColors[spellAddress]);
                racketShader.SetColor("_Racket_Color_Bottom", spellColors2[spellAddress]);
            }
        }

        // Floor image & particle color (global)
        if (spellFloorImage != null)
        {
            try { spellFloorImage.ShowSpell(spellName, spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white); } catch { }
        }

        if (spellParticleColor != null)
        {
            try { spellParticleColor.SetSpellColor(spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white); } catch { }
        }

        // Play audio only on caster client
        if (caster != null && caster.GetComponent<NetworkedSpellcasting>()?.OwnerClientId == OwnerClientId)
        {
            if (wizardAudio.TryGetValue(spellAddress, out AudioClip wizClip) && wizClip != null)
            {
                spellAudio.TryGetValue(spellAddress, out AudioClip spellClip);
                StartCoroutine(PlaySpellSequence(wizClip, spellClip, 0.35f));
            }
            else if (spellAudio.TryGetValue(spellAddress, out AudioClip spellClipOnly) && spellClipOnly != null)
            {
                audioSource?.PlayOneShot(spellClipOnly);
            }
        }

        // Register spawned effect on SpellEffects
        if (SpellEffects != null)
        {
            try
            {
                TennisAI aiRef = TennisAi != null ? TennisAi : FindObjectOfType<TennisAI>();
                SpellEffects.SetContext(caster, opponent, aiRef);

                if (effectNetId != 0)
                {
                    SpellEffects.RegisterNetworkedEffect(spellName, effectNetId);
                }

                SpellEffects.plrHitSpell = boolBook.ContainsKey(spellName) && boolBook[spellName];
                SpellEffects.castSpell();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkedSpellcasting] Error casting spell: {e.Message}");
            }
        }

        // Start cleanup timer on ALL clients
        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelayAllClients(duration, localBaseEffect, spellAddress));
    }

    // Helper coroutine to wait for networked visual to be available
    private IEnumerator WaitForNetworkedVisual(ulong visualNetId, GameObject ballObj, GameObject localBaseEffect)
    {
        int attempts = 0;
        GameObject netVisual = null;

        while (attempts < 20 && netVisual == null) // Try for 1 second (20 * 0.05s)
        {
            netVisual = GetSpawnedObjectByNetId(visualNetId);
            if (netVisual != null)
                break;

            yield return new WaitForSeconds(0.05f);
            attempts++;
        }

        if (netVisual != null)
        {
            Debug.Log($"[NetworkedSpellcasting] Found networked visual after {attempts} attempts");
            currentVisualInstance = netVisual;

            // Disable base effect
            if (localBaseEffect != null)
            {
                localBaseEffect.SetActive(false);
                Debug.Log($"[NetworkedSpellcasting] Disabled base effect for networked visual");
            }
        }
        else
        {
            Debug.LogWarning($"[NetworkedSpellcasting] Failed to find networked visual with ID {visualNetId}");
        }
    }

    // New method: Reset visual on ALL clients (not just caster)
    private IEnumerator ResetVisualAfterDelayAllClients(float delay, GameObject localBaseEffect, string spellAddress)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log($"[NetworkedSpellcasting] Resetting visual after {delay}s on client");

        // Handle visual cleanup
        if (currentVisualInstance != null)
        {
            NetworkObject netObj = currentVisualInstance.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                // Client-only object, safe to destroy
                Debug.Log($"[NetworkedSpellcasting] Destroying client-side visual");
                Destroy(currentVisualInstance);
            }
            else
            {
                // Has NetworkObject - only server can despawn
                if (IsServer)
                {
                    Debug.Log($"[NetworkedSpellcasting] Server despawning networked visual");
                    if (netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(currentVisualInstance);
                    }
                }
                else
                {
                    Debug.Log($"[NetworkedSpellcasting] Client clearing networked visual reference (server will despawn)");
                }
            }
            currentVisualInstance = null;
        }

        // Re-enable base effect on THIS client
        if (localBaseEffect != null)
        {
            localBaseEffect.SetActive(true);
            Debug.Log($"[NetworkedSpellcasting] Re-enabled base effect");
        }

        // Only reset UI/shader on the caster's client
        if (IsOwner)
        {
            spellParticleColor?.ResetColor();

            if (racketShader != null && racketShader.HasProperty("_Racket_Color_Top") && racketShader.HasProperty("_Racket_Color_Bottom"))
            {
                racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
                racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
            }

            currentActiveSpell = "";
            isCasting = false;
            uiManager?.UpdateSpellStatus("None", Color.white);
            RemoveSpell(spellAddress);
        }
    }

    // Keep your original ResetVisualAfterDelay for the caster's CheckSpell method
    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect, string spellAddress)
    {
        yield return new WaitForSeconds(delay);

        // This runs on the caster only - just handle the caster-specific cleanup
        // The visual cleanup is handled by ResetVisualAfterDelayAllClients on all clients

        if (IsOwner)
        {
            currentActiveSpell = "";
            isCasting = false;
            uiManager?.UpdateSpellStatus("None", Color.white);
        }
    }

    // SwapVisual is no longer used but kept for compatibility
    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (newPrefab == null)
        {
            Debug.LogWarning("[NetworkedSpellcasting] SwapVisual: newPrefab is null");
            return;
        }
        if (parentTransform == null)
        {
            Debug.LogWarning("[NetworkedSpellcasting] SwapVisual: parentTransform is null");
            return;
        }

        // Destroy previous visual instance
        if (currentVisualInstance != null)
        {
            NetworkObject netObj = currentVisualInstance.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                Destroy(currentVisualInstance);
            }
            else
            {
                // Only server can despawn networked objects
                if (IsServer)
                {
                    if (netObj.IsSpawned)
                    {
                        netObj.Despawn(true);
                    }
                    else
                    {
                        Destroy(currentVisualInstance);
                    }
                }
            }
            currentVisualInstance = null;
        }

        // Disable the base effect
        baseEffect?.SetActive(false);

        // Instantiate as child of parentTransform
        currentVisualInstance = Instantiate(newPrefab, parentTransform);
        currentVisualInstance.transform.SetParent(parentTransform, false);
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;

        Debug.Log($"[NetworkedSpellcasting] SwapVisual: spawned visual '{currentVisualInstance.name}' on '{parentTransform.name}'");
    }

    // Server RPC to handle visual cleanup across network
    [ServerRpc(RequireOwnership = false)]
    private void DespawnVisualServerRpc(ulong visualNetId)
    {
        if (!IsServer) return;

        GameObject visualObj = GetSpawnedObjectByNetId(visualNetId);
        if (visualObj != null)
        {
            NetworkObject netObj = visualObj.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
    }

    public void AddSpell(string address, string name, float value, GameObject visualPrefab, bool onHitBool, Color SpellColor1, Color SpellColor2, AudioClip spellCastAudio, AudioClip wizardSpellSound, float duration)
    {
        if (!spellBook.ContainsKey(address))
        {
            spellBook[address] = name;
            debuffBook[address] = value;
            if (visualPrefab != null) spellVisuals[address] = visualPrefab;
            boolBook[name] = onHitBool;
            spellColors[address] = SpellColor1;
            spellColors2[address] = SpellColor2;
            spellAudio[address] = spellCastAudio;
            wizardAudio[address] = wizardSpellSound;
            spellDurations[address] = duration;
        }
    }

    private IEnumerator PlaySpellSequence(AudioClip wizardClip, AudioClip spellClip, float delay)
    {
        if (wizardClip != null)
            audioSource?.PlayOneShot(wizardClip);

        yield return new WaitForSeconds(delay);

        if (spellClip != null)
            audioSource?.PlayOneShot(spellClip);
    }

    private GameObject FindOpponent()
    {
        foreach (var sc in FindObjectsOfType<NetworkedSpellcasting>())
        {
            if (sc != this)
                return sc.gameObject;
        }
        return null;
    }

    private void UpdateSpellBook()
    {
        if (spellAddressText != null)
            spellAddressText.text = string.IsNullOrEmpty(inputSpellAddress) ? "" : inputSpellAddress;

        foreach (GameObject currentSpell in GameObject.FindGameObjectsWithTag("SpellUI"))
            Destroy(currentSpell);

        foreach (KeyValuePair<string, string> item in spellBook)
        {
            if (!item.Key.StartsWith(inputSpellAddress)) continue;
            if (spellBookPanel != null && spellTextPrefab != null)
            {
                SpellTextEntry newEntry = Instantiate(spellTextPrefab, spellBookPanel.transform, false);
                newEntry.gameObject.tag = "SpellUI";
                newEntry.SetText(item.Value, item.Key);
            }
        }
    }

    /// <summary>
    /// Public method for external scripts (like SpellEffects) to trigger a spell by name.
    /// </summary>
    public void CastSpellNormal(string spellName)
    {
        // Find the corresponding spell address
        string spellAddress = "";
        foreach (var kvp in spellBook)
        {
            if (kvp.Value == spellName)
            {
                spellAddress = kvp.Key;
                break;
            }
        }

        if (string.IsNullOrEmpty(spellAddress))
        {
            Debug.LogWarning($"[NetworkedSpellcasting] CastSpellNormal: Spell '{spellName}' not found in spell book.");
            return;
        }

        // Set casting state
        if (isCasting)
        {
            Debug.Log("[NetworkedSpellcasting] CastSpellNormal blocked, another spell is active.");
            return;
        }

        isCasting = true;
        currentActiveSpell = spellName;

        // Play register sound locally
        audioSource?.PlayOneShot(spellRegisterSound);

        // Get opponent NetworkObjectId
        GameObject opponent = FindOpponent();
        ulong opponentNetId = opponent != null && opponent.TryGetComponent<NetworkObject>(out var netObj)
            ? netObj.NetworkObjectId
            : ulong.MaxValue;

        // Trigger the networked spell cast for all clients via server
        CastSpellServerRpc(spellAddress, spellName, opponentNetId);

        // Reset visuals after spell duration
        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, spellAddress));
    }

    private void RemoveSpell(string address)
    {
        if (!spellBook.ContainsKey(address)) return;

        string spellName = spellBook[address];
        spellBook.Remove(address);
        debuffBook.Remove(address);
        spellVisuals.Remove(address);
        spellColors.Remove(address);
        spellColors2.Remove(address);
        spellAudio.Remove(address);
        wizardAudio.Remove(address);
        boolBook.Remove(spellName);

        UpdateSpellBook();
    }

    public void ResetForNewRound()
    {
        // Called by Networked Game Manager
        // Force ball refresh when a new round starts
        if (IsOwner)
        {
            currentBall = null;
            TryFindAndLinkBall();
        }
    }

    /// <summary>
    /// Force refresh ball reference - useful when ball respawns
    /// </summary>
    public void ForceRefreshBall()
    {
        currentBall = null;
        TryFindAndLinkBall();
    }
}