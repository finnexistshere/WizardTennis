using UnityEngine;

public class PickupEffect : MonoBehaviour
{
    public enum EffectType { Buff, Debuff }

    [SerializeField] public EffectType type;
    [SerializeField] public float value;
    [SerializeField] private string spellName;
    [SerializeField] private string spellAddress;

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;
    public string SpellAddress => spellAddress;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Spellcasting spellcasting = other.GetComponent<Spellcasting>();
            if (spellcasting != null)
            {
                if (!spellcasting.spellBook.ContainsKey(SpellAddress))
                {
                    spellcasting.spellBook.Add(SpellAddress, SpellName);
                    spellcasting.debuffBook.Add(SpellName, value);
                }
            }

            Destroy(gameObject);
        }
    }
    // This script is to hold the Values of a Name and a debuff value
    // We can honestly just clone this script to hold weird shit down the line, but it'd have to involve upgrades to GameManager.cs and MainCharacterMovement.cs because they call values from this
}

