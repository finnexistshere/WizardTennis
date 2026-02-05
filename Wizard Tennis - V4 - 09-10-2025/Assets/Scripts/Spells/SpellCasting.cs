using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class Spellcasting : MonoBehaviour, ISpellcasting
{
    // --- Racket Shader Reference ---
    [SerializeField] private Material racketShader;

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
    public SpellEffects SpellEffects;

    // --- Internal State ---
    private string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false;
    private GameObject currentBall;
    private Color floorVisualColor;

    [Header("Settings")]
    public bool leftHandedMode = false;
    private float ballCheckInterval = 0.5f;
    private float nextBallCheckTime = 0f;

    [Header("Visual Feedback")]
    public TwoHandIKController ikController;  // Reference to IK controller
    public GameObject arrowKeyPrefab;  // Prefab with Canvas + TextMeshPro showing arrow
    public Transform arrowSpawnPoint;  // Where arrows spawn (e.g., above player's head)

    [Header("IK Nudge Settings")]
    public float nudgeDistance = 0.15f;  // How far to nudge hands
    public float nudgeDuration = 0.2f;   // How long the nudge lasts

    [Header("Arrow Particle Settings")]
    public float arrowLifetime = 0.8f;
    public float arrowFadeStart = 0.3f;  // When to start fading (seconds)
    public float arrowMoveSpeed = 2f;
    public Vector2 randomAngleRange = new Vector2(-30f, 30f);  // Random angle variance
    public float arrowSpawnRadius = 0.3f;  // Random spawn offset

    [Header("Arrow Color Settings")]
    [Tooltip("Color for Left arrow (a)")]
    public Color leftArrowColor = new Color(0.3f, 0.6f, 1f);  // Light Blue

    [Tooltip("Color for Right arrow (A)")]
    public Color rightArrowColor = new Color(1f, 0.4f, 0.4f);  // Light Red

    [Tooltip("Color for Up arrow (B)")]
    public Color upArrowColor = new Color(0.4f, 1f, 0.4f);  // Light Green

    [Tooltip("Color for Down arrow (b)")]
    public Color downArrowColor = new Color(1f, 1f, 0.4f);  // Light Yellow

    // Internal mapping
    private Dictionary<string, Color> arrowColors = new Dictionary<string, Color>();

    // ================================================================
    // INITIALIZATION
    // ================================================================

    private void Awake()
    {
        AutoSetupReferences();
        InitializeRacketShader();
        InitializeArrowColors();
    }

    private void InitializeArrowColors()
    {
        arrowColors["a"] = leftArrowColor;   // Left
        arrowColors["A"] = rightArrowColor;  // Right
        arrowColors["B"] = upArrowColor;     // Up
        arrowColors["b"] = downArrowColor;   // Down

        Debug.Log("[Spellcasting] Arrow colors initialized.");
    }

    private void Start()
    {
        // Subscribe to language changes
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }
    }

    private void AutoSetupReferences()
    {
        // --- Racket Shader ---
        if (racketShader == null)
        {
            Debug.LogWarning("[Spellcasting] Racket shader missing! Attempting to find a Material named 'Racket'.");
            var mats = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in mats)
            {
                if (mat.name.Contains("Racket"))
                {
                    racketShader = mat;
                    Debug.Log("[Spellcasting] Found and assigned racket shader: " + mat.name);
                    break;
                }
            }
        }

        // --- Audio ---
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
                Debug.Log("[Spellcasting] Found and linked AudioSource.");
            else
                Debug.LogWarning("[Spellcasting] AudioSource missing!");
        }

        // --- Spell Effects ---
        if (SpellEffects == null)
        {
            SpellEffects = FindObjectOfType<SpellEffects>();
            if (SpellEffects != null)
                Debug.Log("[Spellcasting] Linked SpellEffects.");
            else
                Debug.LogWarning("[Spellcasting] SpellEffects reference missing!");
        }

        // --- Spell Floor Image ---
        if (spellFloorImage == null)
        {
            spellFloorImage = FindObjectOfType<SpellFloorImage>();
            if (spellFloorImage != null)
                Debug.Log("[Spellcasting] Linked SpellFloorImage.");
            else
                Debug.LogWarning("[Spellcasting] SpellFloorImage reference missing!");
        }

        // --- Spell Particle Color ---
        if (spellParticleColor == null)
        {
            spellParticleColor = FindObjectOfType<SpellParticleColor>();
            if (spellParticleColor != null)
                Debug.Log("[Spellcasting] Linked SpellParticleColor.");
            else
                Debug.LogWarning("[Spellcasting] SpellParticleColor reference missing!");
        }

        // --- TennisAI ---
        if (TennisAi == null)
        {
            TennisAi = FindObjectOfType<TennisAI>();
            if (TennisAi != null)
                Debug.Log("[Spellcasting] Linked TennisAI.");
            else
                Debug.LogWarning("[Spellcasting] TennisAI reference missing!");
        }

        // --- UI References ---
        if (spellBookPanel == null)
        {
            spellBookPanel = GameObject.Find("SpellBookPanel");
            Debug.Log(spellBookPanel ? "[Spellcasting] Found SpellBookPanel." : "[Spellcasting] SpellBookPanel not found!");
        }

        if (spellAddressText == null)
        {
            spellAddressText = FindObjectOfType<TextMeshProUGUI>();
            Debug.Log(spellAddressText ? "[Spellcasting] Found spellAddressText." : "[Spellcasting] No TextMeshProUGUI found!");
        }

        if (spellTextPrefab == null)
        {
            spellTextPrefab = Resources.Load<SpellTextEntry>("SpellTextEntry");
            Debug.Log(spellTextPrefab ? "[Spellcasting] Loaded SpellTextEntry prefab." : "[Spellcasting] SpellTextEntry prefab missing!");
        }

        // --- Particle Systems ---
        if (hitParticle == null)
        {
            hitParticle = FindObjectOfType<ParticleSystem>();
            Debug.Log(hitParticle ? "[Spellcasting] Found hitParticle." : "[Spellcasting] No ParticleSystem found!");
        }

        // --- IK Controller ---
        if (ikController == null)
        {
            ikController = GetComponent<TwoHandIKController>();
            if (ikController == null)
                ikController = FindObjectOfType<TwoHandIKController>();

            Debug.Log(ikController ? "[Spellcasting] Linked IK Controller." : "[Spellcasting] IK Controller not found!");
        }

        // --- Arrow Spawn Point ---
        if (arrowSpawnPoint == null)
        {
            // Try to find a transform named "ArrowSpawnPoint" or use player's head
            Transform[] children = GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                if (child.name.Contains("Head") || child.name.Contains("Spine"))
                {
                    arrowSpawnPoint = child;
                    Debug.Log($"[Spellcasting] Using {child.name} as arrow spawn point.");
                    break;
                }
            }

            if (arrowSpawnPoint == null)
            {
                // Fallback: create a spawn point above player
                GameObject spawnObj = new GameObject("ArrowSpawnPoint");
                spawnObj.transform.SetParent(transform);
                spawnObj.transform.localPosition = new Vector3(0f, 2f, 0f); // 2 units above player
                arrowSpawnPoint = spawnObj.transform;
                Debug.Log("[Spellcasting] Created ArrowSpawnPoint above player.");
            }
        }
    }

    private void InitializeRacketShader()
    {
        if (racketShader != null)
        {
            racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
            racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
        }
        else
        {
            Debug.LogWarning("[Spellcasting] Racket shader not assigned or found at startup.");
        }
    }

    // ================================================================
    // UPDATE LOOP
    // ================================================================

    private void Update()
    {
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

    // ================================================================
    // BALL LINKING
    // ================================================================

    private void TryFindAndLinkBall()
    {
        if (currentBall != null)
        {
            if (currentBall.activeInHierarchy)
                return;
            currentBall = null;
        }

        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (ballObj == null)
        {
            Debug.LogWarning("[Spellcasting] No Ball found with tag 'Ball'.");
            return;
        }

        currentBall = ballObj;
        parentObject = ballObj;

        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
        {
            baseEffectObject = visualChild.gameObject;
            Debug.Log("[Spellcasting] Linked Ball visuals: " + baseEffectObject.name);
        }
        else
        {
            Debug.LogWarning("[Spellcasting] Could not find 'sm_Ball' child under Ball prefab.");
        }

        Debug.Log("[Spellcasting] Connected to active Ball GameObject.");
    }

    // ================================================================
    // INPUT HANDLING
    // ================================================================

    private bool CheckSpellInput(out string direction)
    {
        direction = "";

        KeyCode leftKey = KeyCode.LeftArrow;
        KeyCode rightKey = KeyCode.RightArrow;
        KeyCode upKey = KeyCode.UpArrow;
        KeyCode downKey = KeyCode.DownArrow;

        if (OptionsManager.Instance != null && OptionsManager.Instance.leftHandedMode)
        {
            leftKey = KeyCode.A;
            rightKey = KeyCode.D;
            upKey = KeyCode.W;
            downKey = KeyCode.S;
        }

        if (Input.GetKeyDown(leftKey)) direction = "a";
        if (Input.GetKeyDown(rightKey)) direction = "A";
        if (Input.GetKeyDown(upKey)) direction = "B";
        if (Input.GetKeyDown(downKey)) direction = "b";

        return !string.IsNullOrEmpty(direction);
    }

    private void RegisterInput(string direction)
    {
        if (audioSource == null)
        {
            Debug.LogWarning("[Spellcasting] AudioSource missing, cannot play SpellInputClick.");
        }
        else if (SpellInputClick != null)
        {
            audioSource.PlayOneShot(SpellInputClick);
        }

        inputSpellAddress += direction;
        lastInputTime = Time.time;
        UpdateSpellBook();

        // VISUAL FEEDBACK
        TriggerIKNudge(direction);
        SpawnArrowParticle(direction);
    }

    // ================================================================
    // SPELL LOGIC
    // ================================================================

    private void CheckSpell()
    {
        if (!spellBook.ContainsKey(inputSpellAddress))
        {
            Debug.Log("[Spellcasting] Spell not found: " + inputSpellAddress);
            return;
        }

        string spellName = spellBook[inputSpellAddress];

        if (isCasting)
        {
            Debug.Log("[Spellcasting] Spell blocked, another is active.");
            return;
        }

        isCasting = true;
        currentActiveSpell = spellName;

        if (audioSource && spellRegisterSound)
            audioSource.PlayOneShot(spellRegisterSound);

        if (SpellEffects == null)
        {
            Debug.LogError("[Spellcasting] Missing SpellEffects reference!");
            return;
        }

        SpellEffects.spellName = spellName;
        SpellEffects.plrHitSpell = boolBook.ContainsKey(spellName) && boolBook[spellName];
        if (!SpellEffects.plrHitSpell)
            SpellEffects.castSpell();

        if (UIManager.Instance != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(inputSpellAddress)
                ? spellColors[inputSpellAddress]
                : Color.white;
            UIManager.Instance.UpdateSpellStatus(spellName, uiSpellColor);
        }

        float duration = spellDurations.ContainsKey(inputSpellAddress)
            ? spellDurations[inputSpellAddress]
            : spellDuration;

        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, inputSpellAddress));
        inputSpellAddress = "";
        UpdateSpellBook();
    }

    // ================================================================
    // RESET
    // ================================================================

    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect, string spellAddress)
    {
        yield return new WaitForSeconds(delay);

        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
            currentVisualInstance = null;
        }

        if (baseEffect != null)
            baseEffect.SetActive(true);

        spellParticleColor?.ResetColor();

        if (racketShader != null)
        {
            racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
            racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
        }

        if (!string.IsNullOrEmpty(currentActiveSpell))
        {
            TennisAi?.ClearEffects();
            currentActiveSpell = "";

            if (UIManager.Instance != null)
                UIManager.Instance.UpdateSpellStatus("None", Color.white);
        }

        RemoveSpell(spellAddress);
        isCasting = false;
        Debug.Log("[Spellcasting] Spellcasting unlocked.");
    }

    // ================================================================
    // UTILITY
    // ================================================================

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
                spellAddressText.text = ColorizeSpellAddress(inputSpellAddress);
            }
        }

        foreach (GameObject currentSpell in GameObject.FindGameObjectsWithTag("SpellUI"))
            Destroy(currentSpell);

        foreach (KeyValuePair<string, string> item in spellBook)
        {
            if (item.Key.StartsWith(inputSpellAddress))
            {
                if (spellBookPanel != null && spellTextPrefab != null)
                {
                    SpellTextEntry newEntry = Instantiate(spellTextPrefab, spellBookPanel.transform, false);
                    newEntry.gameObject.tag = "SpellUI";
                    newEntry.SetText(item.Value, item.Key);
                }
                else
                {
                    Debug.LogWarning("[Spellcasting] Missing UI references for SpellBook update.");
                }
            }
        }
    }

    /// <summary>
    /// Refresh the spell book UI when language changes
    /// Called by SpellLocalizationManager
    /// </summary>
    public void RefreshSpellBookUI()
    {
        if (SpellLocalizationManager.Instance == null) return;

        Language currentLang = SpellLocalizationManager.Instance.CurrentLanguage;
        Debug.Log($"[Spellcasting] Refreshing spell book UI for language: {LanguageHelper.GetLanguageName(currentLang)}");

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
        if (!string.IsNullOrEmpty(currentActiveSpell) && UIManager.Instance != null)
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

                UIManager.Instance.UpdateSpellStatus(currentActiveSpell, uiColor);
            }
        }
    }

    private void RemoveSpell(string address)
    {
        if (!spellBook.ContainsKey(address))
            return;

        string spellName = spellBook[address];
        spellBook.Remove(address);
        debuffBook.Remove(address);
        spellVisuals.Remove(address);
        spellColors.Remove(address);
        spellAudio.Remove(address);
        wizardAudio.Remove(address);
        boolBook.Remove(spellName);

        Debug.Log($"[Spellcasting] Removed spell '{spellName}' ({address}) from spell book.");
        UpdateSpellBook();
    }


public void CastSpellNormal(string spellName)
    {
        bool found = false;
        string spellAddress = "";

        foreach (var spell in spellBook)
        {
            if (spell.Value == spellName)
            {
                spellAddress = spell.Key;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"Spell '{spellName}' not found in spell book.");
            return;
        }

        // Disable base effect
        if (baseEffectObject != null)
            baseEffectObject.SetActive(false);

        // Apply buff/debuff
        float value = debuffBook[spellAddress];

        Debug.Log($"{spellName} cast!");
        if (UIManager.Instance != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(spellAddress)
                ? spellColors[spellAddress]
                : Color.white;
            UIManager.Instance.UpdateSpellStatus(spellName, uiSpellColor);
        }

        Color spellColor = spellColors[spellAddress];
        Color spellColor2 = spellColors2[spellAddress];

        // Change racket color
        racketShader.SetColor("_Racket_Color_Top", spellColor);
        racketShader.SetColor("_Racket_Color_Bottom", spellColor2);

        spellFloorImage.ShowSpell(spellName, spellColor);
        spellParticleColor.SetSpellColor(spellColor);

        if (wizardAudio.TryGetValue(spellAddress, out AudioClip wizclip) && wizclip != null)
        {
            AudioClip spellClip = null;
            spellAudio.TryGetValue(spellAddress, out spellClip);
            StartCoroutine(PlaySpellSequence(wizclip, spellClip, 0.35f)); // waits 1 second
        }
        else if (spellAudio.TryGetValue(spellAddress, out AudioClip spellClipOnly) && spellClipOnly != null)
        {
            // If there’s no wizard clip, play the spell sound immediately
            audioSource?.PlayOneShot(spellClipOnly);
        }


        // Swap visuals
        if (spellVisuals.ContainsKey(spellAddress) && parentObject != null)
            SwapVisual(spellVisuals[spellAddress], parentObject.transform, baseEffectObject);
        SpellEffects.spellHit = true;

        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, spellAddress));
    }



    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        baseEffect.SetActive(false);

        currentVisualInstance = Instantiate(newPrefab, parentTransform);
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;
    }

    public void AddSpell(string address, string name, float value, GameObject visualPrefab, bool onHitBool, Color SpellColor1, Color SpellColor2, AudioClip spellCastAudio, AudioClip wizardSpellSound, float duration)
    {
        if (!spellBook.ContainsKey(address))
        {
            spellBook[address] = name;
            debuffBook[address] = value;

            if (visualPrefab != null)
                spellVisuals[address] = visualPrefab;

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
        // Step 1: play the wizard voice line
        if (wizardClip != null)
            audioSource.PlayOneShot(wizardClip);

        // Step 2: wait for the delay (e.g. 1 second)
        yield return new WaitForSeconds(delay);

        // Step 3: play the spell sound
        if (spellClip != null)
            audioSource.PlayOneShot(spellClip);
    }

    /// <summary>
    /// Triggers a small nudge in the IK controller based on input direction
    /// </summary>
    private void TriggerIKNudge(string direction)
    {
        if (ikController == null)
        {
            Debug.LogWarning("[Spellcasting] IK Controller not assigned!");
            return;
        }

        Vector3 nudgeOffset = Vector3.zero;

        switch (direction)
        {
            case "a": // Left
                nudgeOffset = -transform.right * nudgeDistance;
                break;
            case "A": // Right
                nudgeOffset = transform.right * nudgeDistance;
                break;
            case "B": // Up
                nudgeOffset = Vector3.up * nudgeDistance;
                break;
            case "b": // Down
                nudgeOffset = -Vector3.up * nudgeDistance * 0.5f; // Smaller downward nudge
                break;
        }

        StartCoroutine(IKNudgeCoroutine(nudgeOffset));
    }

    /// <summary>
    /// Animates a small nudge in the IK controller
    /// </summary>
    private IEnumerator IKNudgeCoroutine(Vector3 offset)
    {
        if (ikController == null || ikController.twoHandController == null)
            yield break;

        Transform handTarget = ikController.twoHandController;
        Vector3 startPos = handTarget.localPosition;
        Vector3 nudgedPos = startPos + offset;

        float elapsed = 0f;
        float halfDuration = nudgeDuration / 2f;

        // Move to nudged position
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            handTarget.localPosition = Vector3.Lerp(startPos, nudgedPos, t);
            yield return null;
        }

        elapsed = 0f;

        // Return to original position
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            handTarget.localPosition = Vector3.Lerp(nudgedPos, startPos, t);
            yield return null;
        }

        handTarget.localPosition = startPos;
    }

    /// <summary>
    /// Spawns a floating arrow particle that fades out
    /// </summary>
    private void SpawnArrowParticle(string direction)
    {
        if (arrowKeyPrefab == null)
        {
            Debug.LogWarning("[Spellcasting] Arrow key prefab not assigned!");
            return;
        }

        if (arrowSpawnPoint == null)
        {
            Debug.LogWarning("[Spellcasting] Arrow spawn point not assigned!");
            return;
        }

        // Get arrow symbol based on direction
        string arrowSymbol = direction;

        // Random spawn position around spawn point
        Vector2 randomCircle = Random.insideUnitCircle * arrowSpawnRadius;
        Vector3 spawnPos = arrowSpawnPoint.position + new Vector3(randomCircle.x, randomCircle.y, 0f);

        // Random rotation angle
        float randomAngle = Random.Range(randomAngleRange.x, randomAngleRange.y);
        Quaternion spawnRot = Quaternion.Euler(0f, 0f, randomAngle);

        // Instantiate arrow
        GameObject arrowInstance = Instantiate(arrowKeyPrefab, spawnPos, spawnRot);

        // Set the text
        TextMeshProUGUI arrowText = arrowInstance.GetComponentInChildren<TextMeshProUGUI>();
        if (arrowText != null)
        {
            arrowText.text = arrowSymbol;

            if (arrowColors.TryGetValue(direction, out Color arrowColor))
            {
                arrowText.color = arrowColor;
            }
        }
        else
        {
            Debug.LogWarning("[Spellcasting] Arrow prefab missing TextMeshProUGUI component!");
        }

        // Animate it
        StartCoroutine(AnimateArrowParticle(arrowInstance, arrowText));
    }

    private string GetArrowSymbol(string direction)
    {
        // Font glyphs already match our input directions
        return direction;
    }

    /// <summary>
    /// Animates the arrow particle: moves upward and fades out
    /// </summary>
    private IEnumerator AnimateArrowParticle(GameObject arrow, TextMeshProUGUI arrowText)
    {
        if (arrow == null)
            yield break;

        float elapsed = 0f;
        Vector3 startPos = arrow.transform.position;
        Vector3 moveDirection = Vector3.up * arrowMoveSpeed;

        CanvasGroup canvasGroup = arrow.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = arrow.AddComponent<CanvasGroup>();
        }

        while (elapsed < arrowLifetime)
        {
            elapsed += Time.deltaTime;

            // Move upward
            arrow.transform.position = startPos + moveDirection * elapsed;

            // Fade out after arrowFadeStart
            if (elapsed > arrowFadeStart)
            {
                float fadeProgress = (elapsed - arrowFadeStart) / (arrowLifetime - arrowFadeStart);
                canvasGroup.alpha = 1f - fadeProgress;

                if (arrowText != null)
                {
                    Color textColor = arrowText.color;
                    textColor.a = 1f - fadeProgress;
                    arrowText.color = textColor;
                }
            }

            yield return null;
        }

        Destroy(arrow);
    }

    private void OnDestroy()
    {
        // Unsubscribe from language changes
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(Language newLanguage)
    {
        Debug.Log($"[Spellcasting] Language changed to: {LanguageHelper.GetLanguageName(newLanguage)}");
        RefreshSpellBookUI();
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
}
