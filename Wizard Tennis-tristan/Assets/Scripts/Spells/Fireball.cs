using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fireball : PickupBase
{
    [SerializeField] public EffectType type;
    [SerializeField] public float value;
    [SerializeField] private string spellName;

    public bool IsBuff => type == EffectType.Buff;
    public float Amount => value;
    public string SpellName => spellName;
    public enum EffectType { Buff, Debuff }

    public override void ApplyEffect(MainCharacterMovement player)
    {
        player.SetDebuff(Amount, SpellName);
    }
}
