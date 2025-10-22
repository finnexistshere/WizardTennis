using UnityEngine;

public class PickupEffect : MonoBehaviour
{
    public enum EffectType { Buff, Debuff }

    [SerializeField] public EffectType type;
    [SerializeField] public float value;
    [SerializeField] private string spellName;
    [SerializeField] private string spellAddress;
    [SerializeField] private bool onHitBool;
    [SerializeField] public Color FloorVisualColor;
    [SerializeField] public AudioClip spellCastAudio;

    [Header("Visual Prefab")]
    [SerializeField] private GameObject spellVisualPrefab; // full ball visual prefab

    [SerializeField] private AudioClip audioClip;

    private AudioSource audioSource;

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;
    public string SpellAddress => spellAddress;

    public bool OnHitBool => onHitBool;

    private void Awake()
    {
        audioSource = GameObject.FindGameObjectWithTag("Audio Source").GetComponent<AudioSource>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Spellcasting spellcasting = other.GetComponent<Spellcasting>();

            spellcasting.AddSpell(spellAddress, spellName, value, spellVisualPrefab, onHitBool, FloorVisualColor, spellCastAudio);
            if (spellcasting != null)
            {
                if (!spellcasting.spellBook.ContainsKey(SpellAddress))
                {
                    // Match Spellcasting.AddSpell() structure
                    spellcasting.spellBook[SpellAddress] = SpellName;
                    spellcasting.debuffBook[SpellAddress] = value;  // fixed line!

                    // Optionally assign a prefab and bool if you have those
                    spellcasting.boolBook[SpellName] = false; // default to false or set dynamically

                    if (audioClip != null)
                        audioSource.PlayOneShot(audioClip);
                }
            }

            Destroy(gameObject);
        }
    }

    // This script is to hold the Values of a Name and a debuff value
    // We can honestly just clone this script to hold weird shit down the line, but it'd have to involve upgrades to GameManager.cs and MainCharacterMovement.cs because they call values from this
}

