using UnityEngine;

public class TennisAI : MonoBehaviour
{
    [Header("Probability Settings")]
    [Range(0f, 1f)] public float baseProbability = 0.5f;

    private float buff = 0f;
    private string currentSpellName = "";


    [Header("Ball Reference")]
    public GameObject ball; // assign the actual tennis ball in the Inspector

    private void Start()
    {

    }

    // Attempts a Hit back based on the given Buff and Debuff values
    public bool AttemptReturn()
    {
        float totalProbability = baseProbability + buff;
        totalProbability = Mathf.Clamp01(totalProbability);

        float roll = Random.value;
        bool success = roll <= totalProbability;

        Debug.Log(success ? "AI returned the ball!" : "AI missed!");

        // Reset one-time modifiers after use
        //ClearEffects();

        return success;
    }

    // Applies the buff, this is to be called from other scripts
    public void ApplyBuff(float amount, string spell)
    {
        buff = amount;
        currentSpellName = spell;
        UpdateSpellUI();
    }

    // Call this once the buff/debuff has been consumed
    public void ClearEffects()
    {
        buff = 0f;
        currentSpellName = "";
        UpdateSpellUI();
    }

    // I'm not sure if this is actually doing anything but I don't wanna risk breaking it
    private void UpdateSpellUI()
    {
        // Keep this empty for compatability's sake
    }
}
