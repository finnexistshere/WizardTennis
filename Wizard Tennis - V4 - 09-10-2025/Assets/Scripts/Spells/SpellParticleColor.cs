using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SpellParticleColor : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem targetParticleSystem;

    private ParticleSystem.MainModule mainModule;
    private Color baseColor; // cached original color

    void Awake()
    {
        if (targetParticleSystem == null)
            targetParticleSystem = GetComponent<ParticleSystem>();

        mainModule = targetParticleSystem.main;

        // Cache the starting color
        baseColor = mainModule.startColor.color;
    }

    /// <summary>
    /// Update the particle system color to match the spell.
    /// </summary>
    public void SetSpellColor(Color spellColor)
    {
        mainModule.startColor = spellColor;
    }

    /// <summary>
    /// Reset the particle system color to its original value.
    /// </summary>
    public void ResetColor()
    {
        mainModule.startColor = baseColor;
    }
}
