using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedSpellcasting : NetworkBehaviour, ISpellcasting
{
    // Serializable struct to pass spellBook over network
    [System.Serializable]
    public struct SerializedSpellBook : INetworkSerializable
    {
        public string[] addresses;
        public string[] names;

        public SerializedSpellBook(Dictionary<string, string> spellBook)
        {
            addresses = new string[spellBook.Count];
            names = new string[spellBook.Count];

            int i = 0;
            foreach (var kvp in spellBook)
            {
                addresses[i] = kvp.Key;
                names[i] = kvp.Value;
                i++;
            }
        }

        public Dictionary<string, string> ToDictionary()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            if (addresses != null && names != null)
            {
                for (int i = 0; i < addresses.Length && i < names.Length; i++)
                {
                    result[addresses[i]] = names[i];
                }
            }

            return result;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serialize the count
            int count = 0;

            if (serializer.IsWriter)
            {
                count = addresses?.Length ?? 0;
            }

            serializer.SerializeValue(ref count);

            // Initialize arrays on read
            if (serializer.IsReader)
            {
                addresses = new string[count];
                names = new string[count];
            }

            // Serialize each string individually
            for (int i = 0; i < count; i++)
            {
                if (serializer.IsWriter)
                {
                    // Writing
                    serializer.SerializeValue(ref addresses[i]);
                    serializer.SerializeValue(ref names[i]);
                }
                else
                {
                    // Reading
                    string addr = "";
                    string name = "";
                    serializer.SerializeValue(ref addr);
                    serializer.SerializeValue(ref name);
                    addresses[i] = addr;
                    names[i] = name;
                }
            }
        }
    }

    public enum SpellInputDirection : byte
    {
        Left,
        Right,
        Up,
        Down
    }

    [Header("Lightning Trail")]
    [SerializeField] public GameObject lightningTrailObject;
    public GameObject LightningTrailObject => lightningTrailObject;

    [SerializeField] private GameObject arrowKeyPrefab;
    [SerializeField] private Transform arrowSpawnPoint;

    [SerializeField] private float arrowSpawnRadius = 0.15f;
    [SerializeField] private Vector2 randomAngleRange = new Vector2(-20f, 20f);
    [SerializeField] private float arrowMoveSpeed = 0.6f;
    [SerializeField] private float arrowLifetime = 0.6f;
    [SerializeField] private float arrowFadeStart = 0.25f;

    [Header("Arrow Color Settings")]
    [Tooltip("Color for Left arrow (a)")]
    [SerializeField] private Color leftArrowColor = new Color(0.3f, 0.6f, 1f);  // Light Blue

    [Tooltip("Color for Right arrow (A)")]
    [SerializeField] private Color rightArrowColor = new Color(1f, 0.4f, 0.4f);  // Light Red

    [Tooltip("Color for Up arrow (B)")]
    [SerializeField] private Color upArrowColor = new Color(0.4f, 1f, 0.4f);  // Light Green

    [Tooltip("Color for Down arrow (b)")]
    [SerializeField] private Color downArrowColor = new Color(1f, 1f, 0.4f);  // Light Yellow

    // Internal color mapping
    private Dictionary<string, Color> arrowColors = new Dictionary<string, Color>();

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

    public bool green;

    [SerializeField] private TwoHandIKController ikController;
    [SerializeField] private float ikNudgeAmount = 0.08f;
    [SerializeField] private float ikNudgeTime = 0.12f;

    // Queue state
    private bool queuedCast = false;
    private string queuedSpellAddress = "";
    private string queuedSpellName = "";

    // Track original spell addresses for Gambit (so we remove the right spell on cleanup)
    private Dictionary<string, string> gambitTransformations = new Dictionary<string, string>();

    private void Awake()
    {
        AutoSetupReferences();
        InitializeRacketShader();
        InitializeArrowColors();
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

            // Subscribe to language changes
            if (OptionsManager.Instance != null)
            {
                OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;

                // Make sure we ask the Options manager if Left Handed mode is on
                leftHandedMode = OptionsManager.Instance.leftHandedMode;
            }
        }
    }

    private void OnLanguageChanged(Language newLanguage)
    {
        if (!IsOwner) return;

        Debug.Log($"[NetworkedSpellcasting] Language changed to: {LanguageHelper.GetLanguageName(newLanguage)}");
        RefreshSpellBookUI();
    }

    private void OnDestroy()
    {
        if (IsOwner && OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
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

    private void InitializeArrowColors()
    {
        arrowColors["a"] = leftArrowColor;   // Left
        arrowColors["A"] = rightArrowColor;  // Right
        arrowColors["B"] = upArrowColor;     // Up
        arrowColors["b"] = downArrowColor;   // Down

        Debug.Log("[NetworkedSpellcasting] Arrow colors initialized.");
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

        if (CheckSpellInput(out SpellInputDirection dir))
        {
            RegisterInput(DirectionToGlyph(dir));
            TriggerInputFeedback(dir);
        }

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

    private bool CheckSpellInput(out SpellInputDirection dir)
    {
        dir = default;

        KeyCode leftKey = leftHandedMode ? KeyCode.A : KeyCode.LeftArrow;
        KeyCode rightKey = leftHandedMode ? KeyCode.D : KeyCode.RightArrow;
        KeyCode upKey = leftHandedMode ? KeyCode.W : KeyCode.UpArrow;
        KeyCode downKey = leftHandedMode ? KeyCode.S : KeyCode.DownArrow;

        if (Input.GetKeyDown(leftKey)) dir = SpellInputDirection.Left;
        else if (Input.GetKeyDown(rightKey)) dir = SpellInputDirection.Right;
        else if (Input.GetKeyDown(upKey)) dir = SpellInputDirection.Up;
        else if (Input.GetKeyDown(downKey)) dir = SpellInputDirection.Down;
        else return false;

        return true;
    }

    private void RegisterInput(string direction)
    {
        audioSource?.PlayOneShot(SpellInputClick);
        inputSpellAddress += direction;
        lastInputTime = Time.time;
        UpdateSpellBook();
    }

    /// <summary>
    /// UPDATED: Ensure queue handling respects per-player state
    /// </summary>
    private void CheckSpell()
    {
        if (!spellBook.ContainsKey(inputSpellAddress))
            return;

        if (isCasting)
            return;

        // Check if ANY spell is active (not just ours)
        if (NetworkedSpellEffects.Instance != null && NetworkedSpellEffects.Instance.IsAnySpellActive)
        {
            // Check if we already have an active spell
            if (NetworkedSpellEffects.Instance.HasActiveSpell(OwnerClientId))
            {
                Debug.Log($"[NetworkedSpellcasting] Cannot cast - we already have an active spell");
                inputSpellAddress = "";
                UpdateSpellBook();
                return;
            }

            // Another player has a spell active - we can still cast!
            Debug.Log($"[NetworkedSpellcasting] Another player has active spell, but we can still cast");
        }

        // Cast immediately
        ExecuteSpell(inputSpellAddress, spellBook[inputSpellAddress]);
    }

    /// <summary>
    /// UPDATED: Execute spell with caster client ID
    /// </summary>
    private void ExecuteSpell(string spellAddress, string spellName)
    {
        // Get the caster's client ID
        ulong casterClientId = OwnerClientId;

        // Special case: Gambit defers resolution to server
        if (spellName == "Gambit")
        {
            isCasting = true;
            currentActiveSpell = "Gambit";

            audioSource?.PlayOneShot(spellRegisterSound);

            GameObject opponent = FindOpponent();
            ulong opponentNetId = opponent != null && opponent.TryGetComponent<NetworkObject>(out var netObj)
                ? netObj.NetworkObjectId
                : ulong.MaxValue;

            SerializedSpellBook serializedSpellBook = new SerializedSpellBook(spellBook);

            // Pass caster client ID to server
            CastSpellServerRpc(spellAddress, spellName, opponentNetId, serializedSpellBook, casterClientId);

            inputSpellAddress = "";
            UpdateSpellBook();
            return;
        }

        // === NORMAL SPELL FLOW ===
        isCasting = true;
        currentActiveSpell = spellName;

        audioSource?.PlayOneShot(spellRegisterSound);

        GameObject opponentNormal = FindOpponent();
        ulong opponentNetIdNormal = opponentNormal != null && opponentNormal.TryGetComponent<NetworkObject>(out var netObj2)
            ? netObj2.NetworkObjectId
            : ulong.MaxValue;

        SerializedSpellBook normalBook = new SerializedSpellBook(spellBook);

        // Pass caster client ID to server
        CastSpellServerRpc(spellAddress, spellName, opponentNetIdNormal, normalBook, casterClientId);

        float duration = spellDurations.ContainsKey(spellAddress)
            ? spellDurations[spellAddress]
            : spellDuration;

        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, spellAddress, casterClientId));

        inputSpellAddress = "";
        UpdateSpellBook();
    }

    private string DirectionToGlyph(SpellInputDirection dir)
    {
        switch (dir)
        {
            case SpellInputDirection.Left: return "a";
            case SpellInputDirection.Right: return "A";
            case SpellInputDirection.Up: return "B";
            case SpellInputDirection.Down: return "b";
        }
        return "";
    }

    private void TriggerInputFeedback(SpellInputDirection dir)
    {
        // Local IK nudge (owner only)
        TriggerIKNudge(dir);

        // Tell server to broadcast particle
        InputFeedbackServerRpc(dir);
    }

    [ServerRpc]
    private void InputFeedbackServerRpc(SpellInputDirection dir, ServerRpcParams rpcParams = default)
    {
        InputFeedbackClientRpc(dir, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void InputFeedbackClientRpc(SpellInputDirection dir, ulong casterClientId)
    {
        // Find caster
        NetworkedSpellcasting caster = null;
        foreach (var sc in FindObjectsOfType<NetworkedSpellcasting>())
        {
            if (sc.OwnerClientId == casterClientId)
            {
                caster = sc;
                break;
            }
        }

        if (caster == null) return;

        // Spawn directional particle
        caster.PlayInputParticle(dir);

        // IK only on owning client
        if (caster.IsOwner)
        {
            caster.TriggerIKNudge(dir);
        }
    }

    private void PlayInputParticle(SpellInputDirection dir)
    {
        if (arrowKeyPrefab == null || arrowSpawnPoint == null)
            return;

        // Convert direction to glyph
        string arrowSymbol = GetArrowSymbol(dir);

        // Random position around spawn point (world-space UI)
        Vector2 randomCircle = Random.insideUnitCircle * arrowSpawnRadius;
        Vector3 spawnPos =
            arrowSpawnPoint.position +
            new Vector3(randomCircle.x, randomCircle.y, 0f);

        // Random slight rotation
        float randomAngle = Random.Range(randomAngleRange.x, randomAngleRange.y);
        Quaternion spawnRot = Quaternion.Euler(0f, 0f, randomAngle);

        // Spawn arrow
        GameObject arrowInstance =
            Instantiate(arrowKeyPrefab, spawnPos, spawnRot);

        // Set glyph
        TextMeshProUGUI arrowText =
            arrowInstance.GetComponentInChildren<TextMeshProUGUI>();

        if (arrowText != null)
        {
            arrowText.text = arrowSymbol;

            // Apply color based on direction
            if (arrowColors.TryGetValue(arrowSymbol, out Color arrowColor))
            {
                arrowText.color = arrowColor;
            }
        }

        StartCoroutine(AnimateArrowParticle(arrowInstance, arrowText));
    }

    private string GetArrowSymbol(SpellInputDirection dir)
    {
        switch (dir)
        {
            case SpellInputDirection.Left: return "a";
            case SpellInputDirection.Right: return "A";
            case SpellInputDirection.Up: return "B";
            case SpellInputDirection.Down: return "a";
        }

        return "?";
    }

    private IEnumerator AnimateArrowParticle(
    GameObject arrow,
    TextMeshProUGUI arrowText)
    {
        if (arrow == null)
            yield break;

        float elapsed = 0f;
        Vector3 startPos = arrow.transform.position;
        Vector3 moveDir = Vector3.up * arrowMoveSpeed;

        CanvasGroup canvasGroup =
            arrow.GetComponent<CanvasGroup>() ??
            arrow.AddComponent<CanvasGroup>();

        while (elapsed < arrowLifetime)
        {
            elapsed += Time.deltaTime;

            arrow.transform.position =
                startPos + moveDir * elapsed;

            if (elapsed > arrowFadeStart)
            {
                float t =
                    (elapsed - arrowFadeStart) /
                    (arrowLifetime - arrowFadeStart);

                canvasGroup.alpha = 1f - t;

                if (arrowText != null)
                {
                    Color c = arrowText.color;
                    c.a = 1f - t;
                    arrowText.color = c;
                }
            }

            yield return null;
        }

        Destroy(arrow);
    }

    private void TriggerIKNudge(SpellInputDirection dir)
    {
        if (!IsOwner || ikController == null) return;

        Transform player = ikController.transform;
        Vector3 nudge = Vector3.zero;

        switch (dir)
        {
            case SpellInputDirection.Left:
                nudge = -player.right;
                break;

            case SpellInputDirection.Right:
                nudge = player.right;
                break;

            case SpellInputDirection.Up:
                nudge = Vector3.up;
                break;

            case SpellInputDirection.Down:
                nudge = Vector3.down;
                break;
        }

        ikController.Nudge(nudge * ikNudgeAmount, ikNudgeTime);
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

    /// <summary>
    /// UPDATED: Server RPC with caster client ID parameter
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void CastSpellServerRpc(string spellAddress, string spellName, ulong opponentNetId, SerializedSpellBook serializedSpellBook, ulong casterClientId, ServerRpcParams rpcParams = default)
    {
        // Validate that the RPC sender matches the casterClientId
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (senderClientId != casterClientId)
        {
            Debug.LogWarning($"[NSC-Server] Client ID mismatch - sender: {senderClientId}, claimed caster: {casterClientId}");
            return;
        }

        Debug.Log($"[NSC-Server] CastSpellServerRpc called for spell '{spellName}' by client {casterClientId}");

        // Store original spell address for cleanup tracking
        string originalSpellAddress = spellAddress;

        // BEGIN GAMBIT HANDLING
        if (spellName == "Gambit")
        {
            Debug.Log($"[NSC-Server] Gambit cast detected - rolling random spell");

            if (NetworkedSpellEffects.Instance != null)
            {
                string[] gambitSpells = NetworkedSpellEffects.Instance.GetGambitSpells();
                int randomIndex = Random.Range(0, gambitSpells.Length);
                string rolledSpell = gambitSpells[randomIndex];

                Debug.Log($"[NSC-Server] Gambit rolled: {rolledSpell}");

                string rolledAddress = "";
                Dictionary<string, string> casterSpellBook = serializedSpellBook.ToDictionary();

                foreach (var kvp in casterSpellBook)
                {
                    if (kvp.Value == rolledSpell)
                    {
                        rolledAddress = kvp.Key;
                        Debug.Log($"[NSC-Server] Found address '{rolledAddress}' for rolled spell '{rolledSpell}'");
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(rolledAddress))
                {
                    spellName = rolledSpell;
                    spellAddress = rolledAddress;
                    Debug.Log($"[NSC-Server] Gambit transformed to: {spellName} ({spellAddress})");
                }
            }
        }
        // END GAMBIT HANDLING

        // Server resolves caster/opponent NetworkObjects
        GameObject caster = GetRealCaster(casterClientId);
        GameObject opponent = null;

        if (caster != null)
        {
            Debug.Log($"[NSC-Server] Found caster: {caster.name} at position {caster.transform.position}");
        }
        else
        {
            Debug.LogError($"[NSC-Server] Could not find caster for client {casterClientId}!");
            return;
        }

        // Resolve opponent
        if (opponentNetId != ulong.MaxValue)
            opponent = GetSpawnedObjectByNetId(opponentNetId);

        if (opponent == null)
        {
            Debug.Log($"[NSC-Server] Opponent not found by netId, searching...");
            var all = FindObjectsOfType<NetworkedSpellcasting>();
            foreach (var sc in all)
            {
                if (sc.gameObject == caster) continue;

                GameObject go = sc.gameObject;

                if (go.GetComponent<MainCharacterMovement>() == null &&
                    go.GetComponent<CharacterController>() == null &&
                    go.GetComponent<Rigidbody>() == null)
                {
                    continue;
                }

                opponent = go;
                Debug.Log($"[NSC-Server] Found opponent: {opponent.name}");
                break;
            }
        }

        // Set context in SpellEffects with caster client ID
        if (NetworkedSpellEffects.Instance != null)
        {
            TennisAI aiRef = TennisAi != null ? TennisAi : FindObjectOfType<TennisAI>();
            NetworkedSpellEffects.Instance.SetContext(casterClientId, caster, opponent, aiRef);
        }

        // Spawn networked visual if applicable
        ulong visualNetId = 0;
        ulong effectNetId = 0;

        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (ballObj == null)
        {
            ballObj = GameObject.Find("Ball");
            if (ballObj == null && caster != null)
                ballObj = caster.GetComponent<NetworkedSpellcasting>()?.parentObject;
        }

        if (spellVisuals != null && spellVisuals.ContainsKey(spellAddress) && spellVisuals[spellAddress] != null && ballObj != null)
        {
            GameObject visualPrefab = spellVisuals[spellAddress];
            if (visualPrefab.GetComponent<NetworkObject>() != null)
            {
                visualNetId = SpawnPrefabOnServer(visualPrefab, ballObj.transform.position, ballObj.transform.rotation, casterClientId, giveOwnershipToSender: false);
            }
        }

        // Broadcast to all clients with caster client ID
        CastSpellNetworkedClientRpc(spellAddress, spellName, casterClientId, opponentNetId, visualNetId, effectNetId, originalSpellAddress);
    }

    private GameObject GetRealCaster(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NSC-GetRealCaster] NetworkManager is null");
            return null;
        }

        Debug.Log($"[NSC-GetRealCaster] Searching for client {clientId}...");

        foreach (var obj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (obj.OwnerClientId != clientId)
                continue;

            GameObject go = obj.gameObject;

            Debug.Log($"[NSC-GetRealCaster] Found owned object: {go.name} at {go.transform.position}");

            // Must have NetworkedSpellcasting component
            var spellcasting = go.GetComponent<NetworkedSpellcasting>();
            if (spellcasting == null)
            {
                Debug.Log($"[NSC-GetRealCaster] {go.name} has no NetworkedSpellcasting - skipping");
                continue;
            }

            // CRITICAL: Reject spawners (they have no movement/physics components)
            bool hasMovement = go.GetComponent<MainCharacterMovement>() != null;
            bool hasController = go.GetComponent<CharacterController>() != null;
            bool hasRigidbody = go.GetComponent<Rigidbody>() != null;

            Debug.Log($"[NSC-GetRealCaster] {go.name} - Movement:{hasMovement}, Controller:{hasController}, Rigidbody:{hasRigidbody}");

            if (!hasMovement && !hasController && !hasRigidbody)
            {
                Debug.Log($"[NSC-GetRealCaster] {go.name} is a spawner - skipping");
                continue;
            }

            // CRITICAL: Reject objects positioned below the map (spawners are often at y < -10)
            if (go.transform.position.y < -10f)
            {
                Debug.LogWarning($"[NSC-GetRealCaster] {go.name} is below map at {go.transform.position} - skipping");
                continue;
            }

            Debug.Log($"[NSC-GetRealCaster] ? Valid player found: {go.name} at {go.transform.position}");
            return go;
        }

        Debug.LogError($"[NSC-GetRealCaster] No valid player found for client {clientId}");
        return null;
    }

    // ---------- Updated ClientRpc ----------
    /// <summary>
    /// UPDATED: Client RPC with proper caster identification
    /// </summary>
    [ClientRpc]
    private void CastSpellNetworkedClientRpc(string spellAddress, string spellName, ulong casterClientId, ulong opponentNetId, ulong visualNetId, ulong effectNetId, string originalSpellAddress)
    {
        Debug.Log($"[NetworkedSpellcasting] ClientRpc received: {spellName} (original: {originalSpellAddress}) for caster {casterClientId}");

        // Resolve caster/opponent locally
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
            if (IsOwner && OwnerClientId == casterClientId)
                caster = gameObject;
        }

        // UI update: only on the ACTUAL caster's client
        if (caster != null && caster.TryGetComponent<NetworkedSpellcasting>(out var casterSpellcasting))
        {
            if (casterSpellcasting.IsOwner && casterSpellcasting.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                if (casterSpellcasting.uiManager != null)
                {
                    Color uiSpellColor = spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white;
                    casterSpellcasting.uiManager.UpdateSpellStatus(spellName, uiSpellColor);
                }
            }
        }

        // Visual handling - same as before but now properly identified
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

        GameObject localBaseEffect = null;
        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
        {
            localBaseEffect = visualChild.gameObject;
        }
        else
        {
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

        // Clean up existing visual
        if (currentVisualInstance != null)
        {
            NetworkObject netObj = currentVisualInstance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Destroy(currentVisualInstance);
            }
            currentVisualInstance = null;
        }

        // Spawn visual
        if (visualNetId != 0)
        {
            StartCoroutine(WaitForNetworkedVisual(visualNetId, ballObj, localBaseEffect));
        }
        else if (spellVisuals != null && spellVisuals.ContainsKey(spellAddress) && spellVisuals[spellAddress] != null)
        {
            var prefab = spellVisuals[spellAddress];
            try
            {
                Debug.Log($"[NetworkedSpellcasting] Spawning client-side visual for {spellName}");
                currentVisualInstance = Instantiate(prefab, ballObj.transform);
                currentVisualInstance.transform.localPosition = Vector3.zero;
                currentVisualInstance.transform.localRotation = Quaternion.identity;
                currentVisualInstance.transform.localScale = Vector3.one;

                if (localBaseEffect != null)
                {
                    localBaseEffect.SetActive(false);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkedSpellcasting] Visual spawn failed: {e.Message}");
            }
        }

        // Racket shader - only for actual caster
        if (caster != null && caster.TryGetComponent<NetworkedSpellcasting>(out var casterSC))
        {
            if (casterSC.IsOwner && casterSC.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                if (casterSC.racketShader != null && spellColors.ContainsKey(spellAddress) && spellColors2.ContainsKey(spellAddress))
                {
                    casterSC.racketShader.SetColor("_Racket_Color_Top", spellColors[spellAddress]);
                    casterSC.racketShader.SetColor("_Racket_Color_Bottom", spellColors2[spellAddress]);
                }
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
        if (caster != null && caster.TryGetComponent<NetworkedSpellcasting>(out var audioSC))
        {
            if (audioSC.IsOwner && audioSC.OwnerClientId == NetworkManager.Singleton.LocalClientId)
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
        }

        // Register with SpellEffects using caster client ID
        if (SpellEffects != null)
        {
            try
            {
                // Context already set by server, just trigger the spell
                SpellEffects.castSpell(casterClientId, spellName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkedSpellcasting] Error casting spell: {e.Message}");
            }
        }

        // Start cleanup timer - pass caster client ID
        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelayAllClients(duration, localBaseEffect, originalSpellAddress, casterClientId));
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

    /// <summary>
    /// UPDATED: Reset visual with caster client ID
    /// </summary>
    private IEnumerator ResetVisualAfterDelayAllClients(float delay, GameObject localBaseEffect, string originalSpellAddress, ulong casterClientId)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log($"[NetworkedSpellcasting] Resetting visual after {delay}s on client for caster {casterClientId} - spell: {originalSpellAddress}");

        // Handle visual cleanup
        if (currentVisualInstance != null)
        {
            NetworkObject netObj = currentVisualInstance.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                Debug.Log($"[NetworkedSpellcasting] Destroying client-side visual");
                Destroy(currentVisualInstance);
            }
            else
            {
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
                    Debug.Log($"[NetworkedSpellcasting] Client clearing networked visual reference");
                }
            }
            currentVisualInstance = null;
        }

        // Re-enable base effect
        if (localBaseEffect != null)
        {
            localBaseEffect.SetActive(true);
            Debug.Log($"[NetworkedSpellcasting] Re-enabled base effect");
        }

        // Only reset UI/shader on the ACTUAL caster's client
        if (OwnerClientId == casterClientId && IsOwner)
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

            // Remove the spell from the caster's book
            RemoveSpell(originalSpellAddress);

            Debug.Log($"[NetworkedSpellcasting] Removed spell from book: {originalSpellAddress}");
        }

        // Reset SpellEffects for this specific caster
        if (NetworkedSpellEffects.Instance != null)
        {
            NetworkedSpellEffects.Instance.resetSpellEffect(casterClientId);
        }
    }

    /// <summary>
    /// UPDATED: Include caster client ID
    /// </summary>
    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect, string spellAddress, ulong casterClientId)
    {
        yield return new WaitForSeconds(delay);

        // This runs on the caster only - just handle the caster-specific cleanup
        if (IsOwner && OwnerClientId == casterClientId)
        {
            currentActiveSpell = "";
            isCasting = false;
            uiManager?.UpdateSpellStatus("None", Color.white);

            if (lightningTrailObject != null && lightningTrailObject.activeSelf)
            {
                lightningTrailObject.SetActive(false);
            }
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
        {
            if (string.IsNullOrEmpty(inputSpellAddress))
            {
                spellAddressText.text = "";
            }
            else
            {
                // Use rich text to color each character
                spellAddressText.text = ColorizeSpellAddress(inputSpellAddress);
            }
        }

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
    /// Converts a spell address into colored rich text
    /// Example: "aAB" -> <color=#5599FF>a</color><color=#FF6666>A</color><color=#66FF66>B</color>
    /// </summary>
    private string ColorizeSpellAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return "";

        System.Text.StringBuilder coloredText = new System.Text.StringBuilder();

        foreach (char c in address)
        {
            string direction = c.ToString();

            if (arrowColors.TryGetValue(direction, out Color color))
            {
                // Convert color to hex for TextMeshPro rich text
                string hexColor = ColorUtility.ToHtmlStringRGB(color);
                coloredText.Append($"<color=#{hexColor}>{direction}</color>");
            }
            else
            {
                // Fallback: no color
                coloredText.Append(direction);
            }
        }

        return coloredText.ToString();
    }

    /// <summary>
    /// Public method for SpellTextEntry to get colorized addresses
    /// </summary>
    public string GetColorizedAddress(string address)
    {
        return ColorizeSpellAddress(address);
    }

    /// <summary>
    /// Get the color for a specific arrow direction
    /// </summary>
    public Color GetArrowColor(string direction)
    {
        if (arrowColors.TryGetValue(direction, out Color color))
            return color;

        return Color.white; // Default
    }

    /// <summary>
    /// Refresh the spell book UI when language changes
    /// Called by SpellLocalizationManager
    /// </summary>
    public void RefreshSpellBookUI()
    {
        // Only refresh on owner client (the player who owns this character)
        if (!IsOwner) return;

        if (SpellLocalizationManager.Instance == null) return;

        Language currentLang = SpellLocalizationManager.Instance.CurrentLanguage;
        Debug.Log($"[NetworkedSpellcasting] Refreshing spell book UI for language: {LanguageHelper.GetLanguageName(currentLang)}");

        // Update all spell names in the spellBook dictionary
        Dictionary<string, string> updatedSpellBook = new Dictionary<string, string>();

        foreach (var kvp in spellBook)
        {
            string address = kvp.Key;
            string localizedName = SpellLocalizationManager.Instance.GetLocalizedSpellName(address, kvp.Value);
            updatedSpellBook[address] = localizedName;
        }

        // Replace the spell book with updated names
        spellBook = updatedSpellBook;

        // Refresh the visual UI
        UpdateSpellBook();

        // Update current active spell display if there is one
        if (!string.IsNullOrEmpty(currentActiveSpell) && uiManager != null)
        {
            // Find the spell address for the current active spell
            string activeAddress = "";
            foreach (var kvp in spellBook)
            {
                if (kvp.Value == currentActiveSpell)
                {
                    activeAddress = kvp.Key;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(activeAddress))
            {
                Color uiColor = spellColors.ContainsKey(activeAddress)
                    ? spellColors[activeAddress]
                    : Color.white;

                uiManager.UpdateSpellStatus(currentActiveSpell, uiColor);
            }
        }
    }

    /// <summary>
    /// UPDATED: Public method for external scripts with client ID
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

        if (isCasting)
        {
            Debug.Log("[NetworkedSpellcasting] CastSpellNormal blocked, another spell is active.");
            return;
        }

        isCasting = true;
        currentActiveSpell = spellName;

        audioSource?.PlayOneShot(spellRegisterSound);

        GameObject opponent = FindOpponent();
        ulong opponentNetId = opponent != null && opponent.TryGetComponent<NetworkObject>(out var netObj)
            ? netObj.NetworkObjectId
            : ulong.MaxValue;

        SerializedSpellBook serializedSpellBook = new SerializedSpellBook(spellBook);

        // Pass owner client ID
        ulong casterClientId = OwnerClientId;
        CastSpellServerRpc(spellAddress, spellName, opponentNetId, serializedSpellBook, casterClientId);

        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, spellAddress, casterClientId));
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