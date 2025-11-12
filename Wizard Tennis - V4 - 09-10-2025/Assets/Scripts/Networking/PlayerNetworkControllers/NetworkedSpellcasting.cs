using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class NetworkedSpellcasting : NetworkBehaviour, ISpellcasting
{
    [Header("Racket Shader Reference")]
    [SerializeField] public Material racketShader;

    [Header("Spell Dictionaries")]
    public Dictionary<string, string> spellBook { get; private set; } = new Dictionary<string, string>();
    public Dictionary<string, float> debuffBook = new Dictionary<string, float>();
    public Dictionary<string, GameObject> spellVisuals = new Dictionary<string, GameObject>();
    public Dictionary<string, bool> boolBook = new Dictionary<string, bool>();
    public Dictionary<string, Color> spellColors = new Dictionary<string, Color>();
    public Dictionary<string, Color> spellColors2 = new Dictionary<string, Color>();
    public Dictionary<string, AudioClip> spellAudio = new Dictionary<string, AudioClip>();
    public Dictionary<string, AudioClip> wizardAudio = new Dictionary<string, AudioClip>();
    public Dictionary<string, float> spellDurations = new Dictionary<string, float>();

    [Header("UI References")]
    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public SpellTextEntry spellTextPrefab;

    [Header("Ball Visual")]
    public GameObject parentObject;
    public GameObject baseEffectObject;
    private GameObject currentVisualInstance;

    [Header("Spell Settings")]
    public float spellDuration = 5f;
    public float inputTimeout = 2f;

    [Header("Particle Systems")]
    public ParticleSystem hitParticle;
    public SpellParticleColor spellParticleColor;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip SpellInputClick;
    public AudioClip spellRegisterSound;

    [Header("References")]
    public TennisAI TennisAi;
    public SpellFloorImage spellFloorImage;
    public SpellEffects SpellEffects;

    private string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false;

    public bool leftHandedMode = false;

    private GameObject currentBall;
    private float ballCheckInterval = 0.5f;
    private float nextBallCheckTime = 0f;

    public override void OnNetworkSpawn()
    {
        Debug.Log("[NetworkedSpellcasting] OnNetworkSpawn called.");

        if (NetworkedSpellcastingRelay.Instance != null)
            NetworkedSpellcastingRelay.Instance.ApplyTo(this);

        // Safe defaults for visuals
        if (racketShader != null)
        {
            racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
            racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));
        }

        // Allow only owner to handle input + spell logic
        if (!IsOwner)
            enabled = false;

        TryFindAndLinkBall();
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Regular spell logic
        if (Time.time >= nextBallCheckTime)
        {
            nextBallCheckTime = Time.time + ballCheckInterval;
            TryFindAndLinkBall();
        }

        if (isCasting)
            return;

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
            Debug.Log("[NetworkedSpellcasting] No Ball found with tag 'Ball'.");
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
        else
        {
            Debug.LogWarning("[NetworkedSpellcasting] Could not find child named 'sm_Ball' under Ball prefab.");
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
        audioSource?.PlayOneShot(SpellInputClick);
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
            Debug.Log("[NetworkedSpellcasting] Spell blocked: another spell is still active.");
            return;
        }

        isCasting = true;
        currentActiveSpell = spellName;

        audioSource?.PlayOneShot(spellRegisterSound);
        SpellEffects.spellName = spellName;

        bool isHitSpell = boolBook.ContainsKey(spellName) && boolBook[spellName];
        SpellEffects.plrHitSpell = isHitSpell;
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

    public void CastSpellNormal(string spellName)
    {
        if (!IsOwner) return;

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
            Debug.LogWarning($"[NetworkedSpellcasting] Spell '{spellName}' not found in spell book.");
            return;
        }

        if (baseEffectObject != null)
            baseEffectObject.SetActive(false);

        float value = debuffBook[spellAddress];
        Debug.Log($"[NetworkedSpellcasting] {spellName} cast!");

        if (UIManager.Instance != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(spellAddress)
                ? spellColors[spellAddress]
                : Color.white;
            UIManager.Instance.UpdateSpellStatus(spellName, uiSpellColor);
        }

        Color spellColor = spellColors[spellAddress];
        Color spellColor2 = spellColors2[spellAddress];

        racketShader.SetColor("_Racket_Color_Top", spellColor);
        racketShader.SetColor("_Racket_Color_Bottom", spellColor2);

        spellFloorImage.ShowSpell(spellName, spellColor);
        spellParticleColor.SetSpellColor(spellColor);

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

        SpellEffects.spellHit = true;

        float duration = spellDurations.ContainsKey(spellAddress) ? spellDurations[spellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, spellAddress));
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

        spellParticleColor.ResetColor();

        racketShader.SetColor("_Racket_Color_Top", new Color32(171, 171, 171, 255));
        racketShader.SetColor("_Racket_Color_Bottom", new Color32(99, 99, 99, 255));

        if (!string.IsNullOrEmpty(currentActiveSpell))
        {
            TennisAi.ClearEffects();
            currentActiveSpell = "";
            UIManager.Instance?.UpdateSpellStatus("None", Color.white);
        }

        RemoveSpell(spellAddress);
        isCasting = false;
        Debug.Log("[NetworkedSpellcasting] Spellcasting unlocked.");
    }

    private void SwapVisual(GameObject newPrefab, Transform parentTransform, GameObject baseEffect)
    {
        if (currentVisualInstance != null)
            Destroy(currentVisualInstance);

        if (baseEffect != null)
            baseEffect.SetActive(false);

        currentVisualInstance = Instantiate(newPrefab, parentTransform);
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = Vector3.one;
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
                SpellTextEntry newEntry = Instantiate(spellTextPrefab, spellBookPanel.transform, false);
                newEntry.gameObject.tag = "SpellUI";
                newEntry.SetText(item.Value, item.Key);
            }
        }
    }

    private void RemoveSpell(string address)
    {
        if (spellBook.ContainsKey(address))
        {
            string spellName = spellBook[address];
            spellBook.Remove(address);
            debuffBook.Remove(address);
            spellVisuals.Remove(address);
            spellColors.Remove(address);
            spellAudio.Remove(address);
            wizardAudio.Remove(address);
            boolBook.Remove(spellName);
            Debug.Log($"[NetworkedSpellcasting] Removed spell '{spellName}' ({address}).");
        }

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
        if (wizardClip != null)
            audioSource?.PlayOneShot(wizardClip);

        yield return new WaitForSeconds(delay);

        if (spellClip != null)
            audioSource?.PlayOneShot(spellClip);
    }
}
