using UnityEngine;

public class PickupEffect : MonoBehaviour
{
    public enum EffectType { Buff, Debuff }

    [SerializeField] public EffectType type;
    [SerializeField] public float value;
    [SerializeField] private string spellName;

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            MainCharacterMovement playerMovement = other.GetComponent<MainCharacterMovement>();
            if (playerMovement != null)
            {
                playerMovement.SetDebuff(value, spellName);
            }

            Destroy(gameObject);
        }
    }
    // This script is to hold the Values of a Name and a debuff value
    // We can honestly just clone this script to hold weird shit down the line, but it'd have to involve upgrades to GameManager.cs and MainCharacterMovement.cs because they call values from this
}

