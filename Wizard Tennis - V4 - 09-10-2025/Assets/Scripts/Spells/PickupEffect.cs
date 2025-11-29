using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class PickupEffect : MonoBehaviour
{
    public enum EffectType { Buff, Debuff }

    [Header("Effect Settings")]
    [SerializeField] private EffectType type;
    [SerializeField] private float value;
    [SerializeField] private string spellName;
    [SerializeField] private string spellAddress;
    [SerializeField] private bool onHitBool;
    [SerializeField] private Color Color1;
    [SerializeField] private Color Color2;
    [SerializeField] private AudioClip spellCastAudio;
    [SerializeField] private AudioClip wizardSpellSound;
    [SerializeField] private float duration = 5f;

    [Header("Spawn Settings")]
    [Range(0f, 1f)] public float spawnWeight = 0.2f;

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

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;
    public string SpellAddress => spellAddress;
    public bool OnHitBool => onHitBool;

    private void Awake()
    {
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
            materials.Add(renderer.material); // Clone material automatically
        }

        // Collect all particle systems
        particleSystems.AddRange(GetComponentsInChildren<ParticleSystem>(true));

        // Start invisible
        SetAlpha(0f);
        SetParticleAlpha(0f);
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

            // Configure transparent rendering
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

        ISpellcasting spellcastingRef = null;

        // Check for NetworkedSpellcasting first (owner only)
        NetworkedSpellcasting netSpell = other.GetComponent<NetworkedSpellcasting>();
        if (netSpell != null && netSpell.IsOwner)
        {
            spellcastingRef = netSpell;
        }

        // Fallback to normal Spellcasting
        if (spellcastingRef == null)
        {
            Spellcasting singleSpell = other.GetComponent<Spellcasting>();
            if (singleSpell != null)
                spellcastingRef = (ISpellcasting)singleSpell;
        }

        if (spellcastingRef == null)
        {
            Debug.LogWarning("[PickupEffect] No Spellcasting component found on player!");
            return;
        }

        // Add spell safely
        if (spellcastingRef is Spellcasting sc)
        {
            sc.AddSpell(spellAddress, spellName, value, spellVisualPrefab, onHitBool, Color1, Color2, spellCastAudio, wizardSpellSound, duration);
        }
        else if (spellcastingRef is NetworkedSpellcasting nsc)
        {
            nsc.AddSpell(spellAddress, spellName, value, spellVisualPrefab, onHitBool, Color1, Color2, spellCastAudio, wizardSpellSound, duration);
        }

        // Play pickup sound
        if (audioSource != null && pickupAudio != null)
            audioSource.PlayOneShot(pickupAudio);

        // NETCODE SAFE DESTROY
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                // Server/host only
                netObj.Despawn();
            }
            // Otherwise, client does nothing — despawn will propagate from server
        }
        else
        {
            // Singleplayer fallback
            Destroy(gameObject);
        }
    }
}
