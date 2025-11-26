using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

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
    public SpellEffects SpellEffects;

    // --- Internal State ---
    private string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false;
    private GameObject currentBall;

    [Header("Settings")]
    public bool leftHandedMode = false;

    private NetworkedUIManager uiManager;

    private void Awake()
    {
        AutoSetupReferences();
        InitializeRacketShader();
    }

    public override void OnNetworkSpawn()
    {
        // Only the owner manages their own spellcasting
        if (!IsOwner) return;

        uiManager = GetComponent<NetworkedUIManager>();
        if (uiManager == null)
            Debug.LogWarning($"[NetworkedSpellcasting] Player {OwnerClientId} missing NetworkedUIManager!");

        Debug.Log($"[NetworkedSpellcasting] Player {OwnerClientId} spellcasting initialized.");
    }

    private void AutoSetupReferences()
    {
        // --- Racket Shader ---
        if (racketShader == null)
        {
            Debug.LogWarning("[NetworkedSpellcasting] Racket shader missing!");
        }

        // --- Audio ---
        if (audioSource == null)
        {
            var audioGO = GameObject.Find("audiosource");
            if (audioGO != null)
                audioSource = audioGO.GetComponent<AudioSource>();
        }

        // --- Spell Effects ---
        if (SpellEffects == null)
        {
            SpellEffects = FindObjectOfType<SpellEffects>();
        }

        // --- Spell Floor Image ---
        if (spellFloorImage == null)
        {
            spellFloorImage = FindObjectOfType<SpellFloorImage>();
        }

        // --- Spell Particle Color ---
        if (spellParticleColor == null)
        {
            spellParticleColor = FindObjectOfType<SpellParticleColor>();
        }

        // --- TennisAI ---
        if (TennisAi == null)
        {
            TennisAi = FindObjectOfType<TennisAI>();
        }
    }

    private void InitializeRacketShader()
    {
        if (racketShader != null)
        {
            racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
            racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
        }
    }

    private void Update()
    {
        // Only owner can cast spells
        if (!IsOwner) return;

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
        if (currentBall != null && currentBall.activeInHierarchy)
            return;

        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (ballObj == null)
        {
            Debug.LogWarning("[NetworkedSpellcasting] No Ball found with tag 'Ball'.");
            return;
        }

        currentBall = ballObj;
        parentObject = ballObj;

        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
        {
            baseEffectObject = visualChild.gameObject;
            Debug.Log("[NetworkedSpellcasting] Linked Ball visuals: " + baseEffectObject.name);
        }

        Debug.Log("[NetworkedSpellcasting] Connected to active Ball GameObject.");
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
        if (audioSource != null && SpellInputClick != null)
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
            return;

        string spellName = spellBook[inputSpellAddress];

        if (isCasting)
        {
            Debug.Log("[NetworkedSpellcasting] Spell blocked, another is active.");
            return;
        }

        isCasting = true;
        currentActiveSpell = spellName;

        if (audioSource && spellRegisterSound)
            audioSource.PlayOneShot(spellRegisterSound);

        if (SpellEffects != null)
        {
            GameObject opponent = FindOpponent();
            try
            {
                SpellEffects.SetContext(this.gameObject, opponent, TennisAi);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkedSpellcasting] SpellEffects.SetContext threw: {e.Message}");
            }

            SpellEffects.spellName = spellName;
            SpellEffects.plrHitSpell = boolBook.ContainsKey(spellName) && boolBook[spellName];

            if (!SpellEffects.plrHitSpell)
            {
                try { SpellEffects.castSpell(); }
                catch (System.Exception e) { Debug.LogWarning($"[NetworkedSpellcasting] castSpell threw: {e.Message}"); }
            }
        }

        if (uiManager != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(inputSpellAddress)
                ? spellColors[inputSpellAddress]
                : Color.white;
            uiManager.UpdateSpellStatus(spellName, uiSpellColor);
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

        if (spellParticleColor != null)
        {
            try { spellParticleColor.ResetColor(); }
            catch (System.Exception e) { Debug.LogWarning($"[NetworkedSpellcasting] ResetColor threw: {e.Message}"); }
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

            if (uiManager != null)
                uiManager.UpdateSpellStatus("None", Color.white);
        }

        RemoveSpell(spellAddress);
        isCasting = false;
    }

    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (newPrefab == null) return;

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
        string spellAddress = "";
        foreach (var spell in spellBook)
        {
            if (spell.Value == spellName)
            {
                spellAddress = spell.Key;
                break;
            }
        }

        if (string.IsNullOrEmpty(spellAddress))
        {
            Debug.LogWarning($"Spell '{spellName}' not found in spell book.");
            return;
        }

        if (baseEffectObject != null)
            baseEffectObject.SetActive(false);

        Debug.Log($"{spellName} cast!");

        Color spellColor = spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white;
        Color spellColor2 = spellColors2.ContainsKey(spellAddress) ? spellColors2[spellAddress] : spellColor;

        if (uiManager != null)
        {
            uiManager.UpdateSpellStatus(spellName, spellColor);
        }

        if (racketShader != null)
        {
            racketShader.SetColor("_Racket_Color_Top", spellColor);
            racketShader.SetColor("_Racket_Color_Bottom", spellColor2);
        }

        if (spellFloorImage != null)
        {
            try { spellFloorImage.ShowSpell(spellName, spellColor); } catch { }
        }

        if (spellParticleColor != null)
        {
            try { spellParticleColor.SetSpellColor(spellColor); } catch { }
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
            }
        }
    }

    private void RemoveSpell(string address)
    {
        if (!spellBook.ContainsKey(address)) return;

        string spellName = spellBook[address];
        spellBook.Remove(address);
        debuffBook.Remove(address);
        spellVisuals.Remove(address);
        spellColors.Remove(address);
        spellAudio.Remove(address);
        wizardAudio.Remove(address);
        boolBook.Remove(spellName);

        Debug.Log($"[NetworkedSpellcasting] Removed spell '{spellName}' ({address}).");
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

            Debug.Log($"[NetworkedSpellcasting] Player {OwnerClientId} added spell: {name} ({address})");
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

    public void ResetForNewRound()
    {
        // Called by the Networked Game Manager for any resetting we may need
    }

}