using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class PickupEffect : NetworkBehaviour
{
    public enum EffectType { Buff, Debuff }

    [Header("Effect Settings")]
    [SerializeField] private EffectType type;
    [SerializeField] private float value;
    [SerializeField] private string spellAddress;
    [SerializeField] private bool onHitBool;
    [SerializeField] private Color Color1;
    [SerializeField] private Color Color2;
    [SerializeField] private AudioClip spellCastAudio;
    [SerializeField] private AudioClip wizardSpellSound;
    [SerializeField] private float duration = 5f;

    [Header("Localization")]
    [Tooltip("Default spell name (fallback if no translation exists)")]
    [SerializeField] private string defaultSpellName = "Unknown Spell";

    [Tooltip("Localized spell names for different languages")]
    [SerializeField] private List<LocalizedSpellName> localizedSpellNames = new List<LocalizedSpellName>();

    [Header("Spawn Settings")]
    [Range(0f, 1f)] public float spawnWeight = 0.2f;
    public float SpawnWeight => spawnWeight;

    [Header("Visual Prefab")]
    [SerializeField] private GameObject spellVisualPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupAudio;
    private AudioSource audioSource;

    [Header("Fade Settings")]
    [SerializeField] private float fadeInDuration = 3f;
    [SerializeField] private bool destroyAfterFadeIn = false;

    private List<Material> materials = new List<Material>();
    private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
    private bool isFading = false;
    private bool hasBeenPickedUp = false;

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellAddress => spellAddress;
    public bool OnHitBool => onHitBool;

    // Property to get the spell name in the current language
    public string SpellName
    {
        get
        {
            if (SpellLocalizationManager.Instance != null)
            {
                Language currentLang = SpellLocalizationManager.Instance.CurrentLanguage;
                return GetLocalizedName(currentLang);
            }
            return GetLocalizedName(Language.English);
        }
    }

    private void Awake()
    {
        // Register this spell with the localization manager
        RegisterSpellLocalization();

        // Assign audio source safely
        GameObject audioObj = GameObject.FindGameObjectWithTag("Audio Source");
        if (audioObj != null)
            audioSource = audioObj.GetComponent<AudioSource>();
        else
            Debug.LogWarning("[PickupEffect] No Audio Source found in scene.");

        // Collect all child renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            materials.Add(renderer.material);
        }

        // Collect all particle systems
        particleSystems.AddRange(GetComponentsInChildren<ParticleSystem>(true));

        // Start invisible
        SetAlpha(0f);
        SetParticleAlpha(0f);
    }

    /// <summary>
    /// Register this pickup's spell data with the localization manager
    /// </summary>
    private void RegisterSpellLocalization()
    {
        if (SpellLocalizationManager.Instance == null) return;

        SpellData spellData = new SpellData
        {
            spellAddress = spellAddress,
            defaultName = defaultSpellName,
            localizedNames = new List<LocalizedSpellName>(localizedSpellNames)
        };

        SpellLocalizationManager.Instance.RegisterSpell(spellAddress, spellData);
    }

    /// <summary>
    /// Get the localized name for a specific language
    /// </summary>
    public string GetLocalizedName(Language language)
    {
        foreach (var localized in localizedSpellNames)
        {
            if (localized.language == language)
                return localized.localizedName;
        }

        // Fallback to English
        foreach (var localized in localizedSpellNames)
        {
            if (localized.language == Language.English)
                return localized.localizedName;
        }

        // Final fallback
        return defaultSpellName;
    }

    /// <summary>
    /// Add or update a localized name (useful for runtime setup)
    /// </summary>
    public void SetLocalizedName(Language language, string name)
    {
        for (int i = 0; i < localizedSpellNames.Count; i++)
        {
            if (localizedSpellNames[i].language == language)
            {
                localizedSpellNames[i].localizedName = name;
                RegisterSpellLocalization(); // Update registration
                return;
            }
        }

        localizedSpellNames.Add(new LocalizedSpellName(language, name));
        RegisterSpellLocalization(); // Update registration
    }

    private void OnEnable()
    {
        if (!isFading)
            StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        isFading = true;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);

            SetAlpha(alpha);
            SetParticleAlpha(alpha);
            yield return null;
        }

        SetAlpha(1f);
        SetParticleAlpha(1f);

        isFading = false;

        if (destroyAfterFadeIn)
            Destroy(gameObject, 0.1f);
    }

    private void SetAlpha(float alpha)
    {
        foreach (Material mat in materials)
        {
            if (mat == null || !mat.HasProperty("_Color")) continue;

            Color c = mat.color;
            c.a = alpha;
            mat.color = c;

            if (alpha < 1f)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHABLEND_ON");
            }
        }
    }

    private void SetParticleAlpha(float alpha)
    {
        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps == null) continue;

            var main = ps.main;

            if (main.startColor.mode == ParticleSystemGradientMode.Color)
            {
                Color c = main.startColor.color;
                c.a = alpha;
                main.startColor = c;
            }
            else if (main.startColor.mode == ParticleSystemGradientMode.Gradient)
            {
                Gradient grad = main.startColor.gradient;
                GradientColorKey[] colors = grad.colorKeys;
                GradientAlphaKey[] alphas = grad.alphaKeys;

                for (int i = 0; i < alphas.Length; i++)
                    alphas[i].alpha = alpha;

                Gradient fadedGrad = new Gradient();
                fadedGrad.SetKeys(colors, alphas);
                main.startColor = fadedGrad;
            }

            if (!ps.isPlaying)
                ps.Play();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Check if we're in a networked game
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (hasBeenPickedUp) return;
            HandlePickupSingleplayer(other);
            return;
        }

        // Multiplayer mode
        NetworkedSpellcasting netSpell = other.GetComponent<NetworkedSpellcasting>();
        if (netSpell != null && netSpell.IsOwner)
        {
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            if (playerNetObj != null)
            {
                if (audioSource != null && pickupAudio != null)
                    audioSource.PlayOneShot(pickupAudio);

                RequestPickupServerRpc(playerNetObj.NetworkObjectId);
                // Update On Pickup instead of every frame
                netSpell.UpdateSpellBook();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong playerNetworkId)
    {
        if (hasBeenPickedUp) return;
        hasBeenPickedUp = true;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
        {
            Debug.LogWarning($"[PickupEffect] Could not find NetworkObject with ID {playerNetworkId}");
            return;
        }

        NetworkedSpellcasting netSpell = playerObj.GetComponent<NetworkedSpellcasting>();
        Spellcasting singleSpell = playerObj.GetComponent<Spellcasting>();

        // Create spell data structure for network transmission
        SpellNetworkData spellData = new SpellNetworkData
        {
            spellAddress = spellAddress,
            defaultName = defaultSpellName,
            value = value,
            onHitBool = onHitBool,
            color1 = Color1,
            color2 = Color2,
            duration = duration
        };

        // Create localization data structure
        LocalizationNetworkData localizationData = new LocalizationNetworkData
        {
            languages = new Language[localizedSpellNames.Count],
            names = new string[localizedSpellNames.Count]
        };

        for (int i = 0; i < localizedSpellNames.Count; i++)
        {
            localizationData.languages[i] = localizedSpellNames[i].language;
            localizationData.names[i] = localizedSpellNames[i].localizedName;
        }

        if (netSpell != null)
        {
            AddSpellToPlayerClientRpc(playerNetworkId, spellData, localizationData);
        }
        else if (singleSpell != null)
        {
            // Get current language for single player
            Language currentLang = SpellLocalizationManager.Instance != null
                ? SpellLocalizationManager.Instance.CurrentLanguage
                : Language.English;

            string localizedName = GetLocalizedName(currentLang);
            singleSpell.AddSpell(spellAddress, localizedName, value, spellVisualPrefab,
                                onHitBool, Color1, Color2, spellCastAudio, wizardSpellSound, duration);
        }

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
    }

    [ClientRpc]
    private void AddSpellToPlayerClientRpc(ulong playerNetworkId, SpellNetworkData spellData,
                                          LocalizationNetworkData localizationData)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
            return;

        // Get current language
        Language currentLang = SpellLocalizationManager.Instance != null
            ? SpellLocalizationManager.Instance.CurrentLanguage
            : Language.English;

        // Find the appropriate localized name
        string localizedName = spellData.defaultName;
        for (int i = 0; i < localizationData.languages.Length && i < localizationData.names.Length; i++)
        {
            if (localizationData.languages[i] == currentLang)
            {
                localizedName = localizationData.names[i];
                break;
            }
        }

        // Fallback to English if current language not found
        if (localizedName == spellData.defaultName)
        {
            for (int i = 0; i < localizationData.languages.Length && i < localizationData.names.Length; i++)
            {
                if (localizationData.languages[i] == Language.English)
                {
                    localizedName = localizationData.names[i];
                    break;
                }
            }
        }

        NetworkedSpellcasting netSpell = playerObj.GetComponent<NetworkedSpellcasting>();
        if (netSpell != null)
        {
            netSpell.AddSpell(spellData.spellAddress, localizedName, spellData.value,
                            spellVisualPrefab, spellData.onHitBool, spellData.color1,
                            spellData.color2, spellCastAudio, wizardSpellSound, spellData.duration);

            Debug.Log($"[PickupEffect] Added spell '{localizedName}' to player {playerNetworkId} " +
                     $"(lang: {LanguageHelper.GetLanguageName(currentLang)})");
        }
    }

    private void HandlePickupSingleplayer(Collider other)
    {
        if (hasBeenPickedUp) return;
        hasBeenPickedUp = true;

        Spellcasting sc = other.GetComponent<Spellcasting>();
        if (sc != null)
        {
            // Get current language
            Language currentLang = SpellLocalizationManager.Instance != null
                ? SpellLocalizationManager.Instance.CurrentLanguage
                : Language.English;

            string localizedName = GetLocalizedName(currentLang);

            sc.AddSpell(spellAddress, localizedName, value, spellVisualPrefab,
                       onHitBool, Color1, Color2, spellCastAudio, wizardSpellSound, duration);

            Debug.Log($"[PickupEffect] Added spell '{localizedName}' in singleplayer " +
                     $"(lang: {LanguageHelper.GetLanguageName(currentLang)})");
        }

        if (audioSource != null && pickupAudio != null)
            audioSource.PlayOneShot(pickupAudio);

        Destroy(gameObject);
    }

    // Struct for network data transmission
    [System.Serializable]
    public struct SpellNetworkData : INetworkSerializable
    {
        public string spellAddress;
        public string defaultName;
        public float value;
        public bool onHitBool;
        public Color color1;
        public Color color2;
        public float duration;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref spellAddress);
            serializer.SerializeValue(ref defaultName);
            serializer.SerializeValue(ref value);
            serializer.SerializeValue(ref onHitBool);
            serializer.SerializeValue(ref color1);
            serializer.SerializeValue(ref color2);
            serializer.SerializeValue(ref duration);
        }
    }

    // Struct for localization data transmission
    [System.Serializable]
    public struct LocalizationNetworkData : INetworkSerializable
    {
        public Language[] languages;
        public string[] names;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serialize count
            int count = 0;
            if (serializer.IsWriter)
            {
                count = languages?.Length ?? 0;
            }
            serializer.SerializeValue(ref count);

            // Initialize arrays on read
            if (serializer.IsReader)
            {
                languages = new Language[count];
                names = new string[count];
            }

            // Serialize each element
            for (int i = 0; i < count; i++)
            {
                if (serializer.IsWriter)
                {
                    int langValue = (int)languages[i];
                    serializer.SerializeValue(ref langValue);
                    serializer.SerializeValue(ref names[i]);
                }
                else
                {
                    int langValue = 0;
                    string name = "";
                    serializer.SerializeValue(ref langValue);
                    serializer.SerializeValue(ref name);
                    languages[i] = (Language)langValue;
                    names[i] = name;
                }
            }
        }
    }
}