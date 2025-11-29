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
        if (!IsOwner) return;

        uiManager = GetComponent<NetworkedUIManager>();
        if (uiManager == null)
            Debug.LogWarning($"[NetworkedSpellcasting] Player {OwnerClientId} missing NetworkedUIManager!");
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
            SpellEffects = FindObjectOfType<SpellEffects>();

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
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.E))
            TryFindAndLinkBall();

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
        if (ballObj == null) return;

        currentBall = ballObj;
        parentObject = ballObj;

        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
            baseEffectObject = visualChild.gameObject;
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

        // Request server to cast spell
        CastSpellServerRpc(inputSpellAddress, spellName, opponentNetId);

        float duration = spellDurations.ContainsKey(inputSpellAddress) ? spellDurations[inputSpellAddress] : spellDuration;

        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, inputSpellAddress));
        inputSpellAddress = "";
        UpdateSpellBook();
    }

    [ServerRpc]
    private void CastSpellServerRpc(string spellAddress, string spellName, ulong opponentNetId)
    {
        // Server validates and broadcasts to all clients
        CastSpellNetworkedClientRpc(spellAddress, spellName, OwnerClientId, opponentNetId);
    }

    [ClientRpc]
    private void CastSpellNetworkedClientRpc(string spellAddress, string spellName, ulong casterClientId, ulong opponentNetId)
    {
        if (!spellBook.ContainsKey(spellAddress)) return;

        // Find the actual caster and opponent by NetworkObjectId
        GameObject caster = null;
        GameObject opponent = null;

        foreach (var sc in FindObjectsOfType<NetworkedSpellcasting>())
        {
            if (sc.OwnerClientId == casterClientId)
                caster = sc.gameObject;
            else if (sc.GetComponent<NetworkObject>().NetworkObjectId == opponentNetId)
                opponent = sc.gameObject;
        }

        if (caster == null)
        {
            Debug.LogError($"[NetworkedSpellcasting] Could not find caster with ClientId {casterClientId}");
            return;
        }

        // Update UI (only for the caster)
        if (IsOwner && casterClientId == OwnerClientId)
        {
            if (uiManager != null)
            {
                Color uiSpellColor = spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white;
                uiManager.UpdateSpellStatus(spellName, uiSpellColor);
            }
        }

        // Swap visuals (only on caster's instance)
        if (casterClientId == OwnerClientId && spellVisuals.ContainsKey(spellAddress) && parentObject != null)
            SwapVisual(spellVisuals[spellAddress], parentObject.transform, baseEffectObject);

        // Racket shader (only for caster)
        if (casterClientId == OwnerClientId && racketShader != null && spellColors.ContainsKey(spellAddress) && spellColors2.ContainsKey(spellAddress))
        {
            racketShader.SetColor("_Racket_Color_Top", spellColors[spellAddress]);
            racketShader.SetColor("_Racket_Color_Bottom", spellColors2[spellAddress]);
        }

        // Floor image (visible to all)
        if (spellFloorImage != null)
        {
            try { spellFloorImage.ShowSpell(spellName, spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white); } catch { }
        }

        // Particle system (visible to all)
        if (spellParticleColor != null)
        {
            try { spellParticleColor.SetSpellColor(spellColors.ContainsKey(spellAddress) ? spellColors[spellAddress] : Color.white); } catch { }
        }

        // Play audio (only on caster)
        if (casterClientId == OwnerClientId)
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

        // Spell Effects (ALL CLIENTS execute spell effects)
        if (SpellEffects != null)
        {
            SpellEffects.spellName = spellName;
            SpellEffects.plrHitSpell = boolBook.ContainsKey(spellName) && boolBook[spellName];

            if (!SpellEffects.plrHitSpell)
            {
                try
                {
                    // Set context for ALL clients so spell effects work
                    SpellEffects.SetContext(caster, opponent, TennisAi);
                    SpellEffects.castSpell();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[NetworkedSpellcasting] Error casting spell: {e.Message}");
                }
            }
        }
    }

    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect, string spellAddress)
    {
        yield return new WaitForSeconds(delay);

        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
            currentVisualInstance = null;
        }

        baseEffect?.SetActive(true);

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

    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (newPrefab == null) return;

        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        baseEffect?.SetActive(false);

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
        if (IsServer)
        {
            CastSpellNetworkedClientRpc(spellAddress, spellName, OwnerClientId, opponentNetId);
        }
        else
        {
            CastSpellServerRpc(spellAddress, spellName, opponentNetId);
        }

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
    }
}