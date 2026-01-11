using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SpellParticleColor : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem targetParticleSystem;

    private Color baseColor;

    void Awake()
    {
        if (targetParticleSystem == null)
            targetParticleSystem = GetComponent<ParticleSystem>();

        // Cache original color
        var main = targetParticleSystem.main;
        baseColor = main.startColor.color;
    }

    /// <summary>
    /// Update the particle system color to match the spell.
    /// </summary>
    public void SetSpellColor(Color spellColor)
    {
        if (targetParticleSystem == null)
        {
            Debug.LogError("SpellParticleColor: No ParticleSystem assigned!");
            return;
        }

        var main = targetParticleSystem.main;   // always fetch fresh
        main.startColor = spellColor;
    }

    /// <summary>
    /// Reset the particle system color to its original value.
    /// </summary>
    public void ResetColor()
    {
        if (targetParticleSystem == null) return;

        var main = targetParticleSystem.main;   // fresh again
        main.startColor = baseColor;
    }
}
