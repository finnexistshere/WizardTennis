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

    private void Awake()
    {
        AutoSetupReferences();
        InitializeRacketShader();
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
                if (mat != null && mat.name.Contains("Racket"))
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
            var audioGO = GameObject.Find("audiosource");
            if (audioGO != null)
                audioSource = audioGO.GetComponent<AudioSource>();

            if (audioSource != null)
                Debug.Log("[Spellcasting] Found and linked AudioSource.");
            else
                Debug.LogWarning("[Spellcasting] AudioSource missing!");
        }

        // --- Spell Effects ---
        if (SpellEffects == null)
        {
            try
            {
                SpellEffects = FindObjectOfType<SpellEffects>();
            }
            catch { SpellEffects = null; }

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
            spellBookPanel = GameObject.Find("SpellBook");
            Debug.Log(spellBookPanel ? "[Spellcasting] Found SpellBookPanel." : "[Spellcasting] SpellBookPanel not found!");
        }

        if (spellAddressText == null)
        {
            // prefer finding by name first (spellAddressText could be multiple TMPs in scene)
            var go = GameObject.Find("SpellAddressText");
            if (go != null) spellAddressText = go.GetComponent<TextMeshProUGUI>();
            else spellAddressText = FindObjectOfType<TextMeshProUGUI>();
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
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
    }

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

        // --- Determine player roles for this spell ---
        GameObject caster = this.gameObject;
        GameObject opponent = FindOpponent();

        // Feed the contextual actors into SpellEffects
        try
        {
            SpellEffects.SetContext(caster, opponent, TennisAi);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Spellcasting] SpellEffects.SetContext threw: {e.Message}");
        }

        // Pass spell metadata
        SpellEffects.spellName = spellName;
        SpellEffects.plrHitSpell = boolBook.ContainsKey(spellName) && boolBook[spellName];

        // Cast spell
        if (!SpellEffects.plrHitSpell)
        {
            try { SpellEffects.castSpell(); }
            catch (System.Exception e) { Debug.LogWarning($"[Spellcasting] SpellEffects.castSpell threw: {e.Message}"); }
        }

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

        // Safe particle color set/reset
        if (spellParticleColor != null)
        {
            try { spellParticleColor.ResetColor(); }
            catch (System.Exception e) { Debug.LogWarning($"[Spellcasting] spellParticleColor.ResetColor threw: {e.Message}"); }
        }

        if (racketShader != null)
        {
            racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
            racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
        }

        if (!string.IsNullOrEmpty(currentActiveSpell))
        {
            try { TennisAi?.ClearEffects(); } catch { }
            currentActiveSpell = "";

            if (UIManager.Instance != null)
                UIManager.Instance.UpdateSpellStatus("None", Color.white);
        }

        RemoveSpell(spellAddress);
        isCasting = false;
        Debug.Log("[Spellcasting] Spellcasting unlocked.");
    }

    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (newPrefab == null)
        {
            Debug.LogWarning("[Spellcasting] SwapVisual called with null prefab.");
            return;
        }

        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        if (baseEffect != null) baseEffect.SetActive(false);

        currentVisualInstance = Instantiate(newPrefab, parentTransform);
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;
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

        if (baseEffectObject != null)
            baseEffectObject.SetActive(false);

        float value = 0f;
        if (debuffBook.ContainsKey(spellAddress)) value = debuffBook[spellAddress];

        Debug.Log($"{spellName} cast!");
        if (UIManager.Instance != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(spellAddress)
                ? spellColors[spellAddress]
                : Color.white;
            UIManager.Instance.UpdateSpellStatus(spellName, uiSpellColor);
        }

        Color spellColor = spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white;
        Color spellColor2 = spellColors2.ContainsKey(spellAddress) ? spellColors2[spellAddress] : spellColor;

        if (racketShader != null)
        {
            try
            {
                racketShader.SetColor("_Racket_Color_Top", spellColor);
                racketShader.SetColor("_Racket_Color_Bottom", spellColor2);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Spellcasting] Failed to set racket shader colors: {e.Message}");
            }
        }

        if (spellFloorImage != null)
        {
            try { spellFloorImage.ShowSpell(spellName, spellColor); } catch { }
        }

        if (spellParticleColor != null)
        {
            try { spellParticleColor.SetSpellColor(spellColor); }
            catch (System.Exception e) { Debug.LogWarning($"[Spellcasting] SetSpellColor threw: {e.Message}"); }
        }

        if (wizardAudio.TryGetValue(spellAddress, out AudioClip wizclip) && wizclip != null)
        {
            spellAudio.TryGetValue(spellAddress, out AudioClip spellClip);
            StartCoroutine(PlaySpellSequence(wizclip, spellClip, 0.35f));
        }
        else if (spellAudio.TryGetValue(spellAddress, out AudioClip spellClipOnly) && spellClipOnly != null)
        {
            audioSource?.PlayOneShot(spellClipOnly);
        }

        if (spellVisuals.ContainsKey(spellAddress) && parentObject != null)
            SwapVisual(spellVisuals[spellAddress], parentObject.transform, baseEffectObject);

        if (SpellEffects != null)
        {
            try { SpellEffects.spellHit = true; } catch { }
        }

        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, spellAddress));
    }

    private void UpdateSpellBook()
    {
        if (spellAddressText != null)
            spellAddressText.text = string.IsNullOrEmpty(inputSpellAddress) ? "" : inputSpellAddress;

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

    private GameObject FindOpponent()
    {
        foreach (var sc in FindObjectsOfType<Spellcasting>())
        {
            if (sc != this)
                return sc.gameObject;
        }
        return null;
    }
}