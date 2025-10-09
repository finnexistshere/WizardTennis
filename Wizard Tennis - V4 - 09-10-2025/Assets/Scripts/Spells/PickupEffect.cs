using UnityEngine;

public class PickupEffect : MonoBehaviour
{
    public enum EffectType { Buff, Debuff }

    [Header("Effect Settings")]
    [SerializeField] private EffectType type;
    [SerializeField] private float value;
    [SerializeField] private string spellName;
    [SerializeField] private string spellAddress;
    [SerializeField] private bool onHitBool;

    [Header("Visual Prefab")]
    [SerializeField] private GameObject spellVisualPrefab; // full ball visual prefab

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;
    public string SpellAddress => spellAddress;
    public bool OnHitBool => onHitBool;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Spellcasting spellcasting = other.GetComponent<Spellcasting>();
        if (spellcasting != null)
        {
            spellcasting.AddSpell(spellAddress, spellName, value, spellVisualPrefab, onHitBool);
        }

        Destroy(gameObject);
    }
}


// This script is to hold the Values of a Name and a debuff value
// We can honestly just clone this script to hold weird shit down the line, but it'd have to involve upgrades to GameManager.cs and MainCharacterMovement.cs because they call values from this

