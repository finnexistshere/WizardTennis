using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class NetworkedSpellcasting : NetworkBehaviour, ISpellcasting
{
    [Header("Racket Shader Reference")]
    public Material racketShader;

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

    [Header("Other References")]
    public TennisAI TennisAi;
    public SpellFloorImage spellFloorImage;
    public SpellEffects spellEffects;

    [Header("Settings")]
    public bool leftHandedMode = false;

    private string inputSpellAddress = "";
    private string currentActiveSpell = "";
    private float lastInputTime;
    private bool isCasting = false;
    private GameObject currentBall;
    private float ballCheckInterval = 0.5f;
    private float nextBallCheckTime = 0f;

    public override void OnNetworkSpawn()
    {
        if (NetworkedSpellcastingRelay.Instance != null)
            NetworkedSpellcastingRelay.Instance.ApplyTo(this);
    }

    private void Update()
    {
        if (!IsOwner) return; // Only the local owner handles input

        if (Time.time >= nextBallCheckTime)
        {
            nextBallCheckTime = Time.time + ballCheckInterval;
            TryFindAndLinkBall();
        }

        if (isCasting) return;

        UpdateSpellBook();

        if (!string.IsNullOrEmpty(inputSpellAddress) && Time.time - lastInputTime >= inputTimeout)
            inputSpellAddress = "";

        if (CheckSpellInput(out string direction))
            RegisterInput(direction);

        if (!string.IsNullOrEmpty(inputSpellAddress) && spellBook.ContainsKey(inputSpellAddress))
            CheckSpell();
    }

    private void TryFindAndLinkBall()
    {
        if (currentBall != null && currentBall.activeInHierarchy) return;

        GameObject ballObj = GameObject.FindWithTag("Ball");
        if (!ballObj) return;

        currentBall = ballObj;
        parentObject = ballObj;

        Transform visualChild = ballObj.transform.Find("sm_Ball");
        if (visualChild != null)
            baseEffectObject = visualChild.gameObject;
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
        audioSource.PlayOneShot(SpellInputClick);
        inputSpellAddress += direction;
        lastInputTime = Time.time;
        UpdateSpellBook();
    }

    private void CheckSpell()
    {
        string spellName = spellBook[inputSpellAddress];
        if (isCasting) return;

        isCasting = true;
        currentActiveSpell = spellName;

        audioSource.PlayOneShot(spellRegisterSound);

        spellEffects.spellName = spellName;

        if (boolBook[spellName])
            spellEffects.plrHitSpell = true;
        else
        {
            spellEffects.plrHitSpell = false;
            spellEffects.castSpell();
        }

        if (UIManager.Instance != null)
        {
            Color uiSpellColor = spellColors.ContainsKey(inputSpellAddress) ? spellColors[inputSpellAddress] : Color.white;
            UIManager.Instance.UpdateSpellStatus(spellName, uiSpellColor);
        }

        float duration = spellDurations.ContainsKey(inputSpellAddress) ? spellDurations[inputSpellAddress] : spellDuration;
        StartCoroutine(ResetVisualAfterDelay(duration, baseEffectObject, inputSpellAddress));

        inputSpellAddress = "";
    }

    public void CastSpellNormal(string spellName)
    {
        if (!IsOwner) return;

        if (!spellBook.ContainsValue(spellName)) return;

        spellEffects.spellHit = true;

        // Play audio/visuals
        StartCoroutine(SpellVisualAndAudio(spellName));
    }

    private IEnumerator SpellVisualAndAudio(string spellName)
    {
        if (spellVisuals.ContainsKey(spellName))
        {
            if (baseEffectObject != null)
                baseEffectObject.SetActive(false);

            GameObject newVisual = Instantiate(spellVisuals[spellName], parentObject.transform);
            newVisual.transform.localPosition = Vector3.zero;
            newVisual.transform.localRotation = Quaternion.identity;
            newVisual.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(spellDurations[spellName]);
            Destroy(newVisual);
            if (baseEffectObject != null)
                baseEffectObject.SetActive(true);
        }

        isCasting = false;
    }

    private IEnumerator ResetVisualAfterDelay(float delay, GameObject baseEffect, string spellAddress)
    {
        yield return new WaitForSeconds(delay);
        if (baseEffect != null)
            baseEffect.SetActive(true);
        isCasting = false;
        currentActiveSpell = "";
        UpdateSpellBook();
    }

    private void UpdateSpellBook()
    {
        if (spellAddressText != null)
            spellAddressText.text = inputSpellAddress;
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

}
