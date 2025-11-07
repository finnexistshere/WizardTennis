using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PickupEffect : MonoBehaviour
{
    public enum EffectType { Buff, Debuff }

    [SerializeField] public EffectType type;
    [SerializeField] public float value;
    [SerializeField] private string spellName;
    [SerializeField] private string spellAddress;
    [SerializeField] private bool onHitBool;
    [SerializeField] public Color Color1;
    [SerializeField] public Color Color2;
    [SerializeField] public AudioClip spellCastAudio;
    [SerializeField] public AudioClip wizardSpellSound;
    [SerializeField] private float duration = 5f;

    [Header("Spawn Settings")]
    [Range(0f, 1f)] public float spawnWeight = 0.2f;

    [Header("Visual Prefab")]
    [SerializeField] private GameObject spellVisualPrefab; // full ball visual prefab

    [SerializeField] private AudioClip audioClip;

    private AudioSource audioSource;

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;
    public string SpellAddress => spellAddress;

    public bool OnHitBool => onHitBool;

    [Header("Fade Settings")]
    [SerializeField] private float fadeInDuration = 3f;
    [SerializeField] private bool destroyAfterFadeIn = false;

    private List<Material> materials = new List<Material>();
    private List<ParticleSystem> particleSystems = new List<ParticleSystem>();
    private bool isFading = false;

    private void Awake()
    {
        audioSource = GameObject.FindGameObjectWithTag("Audio Source").GetComponent<AudioSource>();
        Debug.Log($"Assigned audiosource to {audioSource.name}");

        // Collect all renderers and materials from children
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            // Clone the material to avoid affecting shared assets
            materials.Add(renderer.material);
        }

        // Collect all particle systems from children
        particleSystems.AddRange(GetComponentsInChildren<ParticleSystem>(true));

        // Start invisible
        SetAlpha(0f);
        SetParticleAlpha(0f);
    }

    private void OnEnable()
    {
        // Begin fade-in when enabled
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

        // Ensure fully visible
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

            // Configure for transparent rendering if needed
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
                // Fade the whole gradient if used
                Gradient grad = main.startColor.gradient;
                GradientColorKey[] colors = grad.colorKeys;
                GradientAlphaKey[] alphas = grad.alphaKeys;
                for (int i = 0; i < alphas.Length; i++)
                    alphas[i].alpha = alpha;
                Gradient fadedGrad = new Gradient();
                fadedGrad.SetKeys(colors, alphas);
                main.startColor = fadedGrad;
            }

            // If system is stopped, restart it so it shows
            if (!ps.isPlaying)
                ps.Play();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Spellcasting spellcasting = other.GetComponent<Spellcasting>();

            spellcasting.AddSpell(spellAddress, spellName, value, spellVisualPrefab, onHitBool, Color1, Color2, spellCastAudio, wizardSpellSound, duration);
            if (spellcasting != null)
            {
                if (audioClip != null)
                    audioSource.PlayOneShot(audioClip);

                if (!spellcasting.spellBook.ContainsKey(SpellAddress))
                {
                    // Match Spellcasting.AddSpell() structure
                    spellcasting.spellBook[SpellAddress] = SpellName;
                    spellcasting.debuffBook[SpellAddress] = value;  // fixed line!

                    // Optionally assign a prefab and bool if you have those
                    spellcasting.boolBook[SpellName] = false; // default to false or set dynamically
                }
            }

            Destroy(gameObject);
        }
    }

    // This script is to hold the Values of a Name and a debuff value
    // We can honestly just clone this script to hold weird shit down the line, but it'd have to involve upgrades to GameManager.cs and MainCharacterMovement.cs because they call values from this
}

