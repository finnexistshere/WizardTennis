using UnityEngine;
using TMPro;

public class NetworkedSpellcastingRelay : MonoBehaviour
{
    [Header("References to feed to NetworkedSpellcasting")]
    public Material racketShader;
    public GameObject spellBookPanel;
    public TextMeshProUGUI spellAddressText;
    public SpellTextEntry spellTextPrefab;
    public AudioSource audioSource;
    public AudioClip spellInputClick;
    public AudioClip spellRegisterSound;
    public SpellParticleColor spellParticleColor;
    public SpellFloorImage spellFloorImage;
    public TennisAI tennisAI;
    public NetworkedSpellEffects spellEffects;

    private static NetworkedSpellcastingRelay instance;
    public static NetworkedSpellcastingRelay Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void ApplyTo(NetworkedSpellcasting spellcaster)
    {
        if (spellcaster == null) return;

        spellcaster.racketShader = racketShader;
        spellcaster.spellBookPanel = spellBookPanel;
        spellcaster.spellAddressText = spellAddressText;
        spellcaster.spellTextPrefab = spellTextPrefab;
        spellcaster.audioSource = audioSource;
        spellcaster.SpellInputClick = spellInputClick;
        spellcaster.spellRegisterSound = spellRegisterSound;
        spellcaster.spellParticleColor = spellParticleColor;
        spellcaster.spellFloorImage = spellFloorImage;
        spellcaster.TennisAi = tennisAI;
        spellcaster.SpellEffects = spellEffects;
    }
}
