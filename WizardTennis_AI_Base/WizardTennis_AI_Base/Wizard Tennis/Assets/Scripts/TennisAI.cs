using UnityEngine;

public class TennisAI : MonoBehaviour
{
    [Header("Probability Settings")]
    [Range(0f, 1f)] public float baseProbability = 0.5f;
    private float oneTimeBuff = 0f;
    private float oneTimeDebuff = 0f;

    private float pendingBuff = 0f;
    private float pendingDebuff = 0f;
    private string currentSpellName = "";

    private void Start()
    {

    }

    // Attempts a Hit back based on the given Buff and Debuff values
    public void AttemptReturn()
    {
        float totalProbability = baseProbability + oneTimeBuff - oneTimeDebuff;
        totalProbability = Mathf.Clamp01(totalProbability);

        float roll = Random.value;
        bool success = roll <= totalProbability;

        Debug.Log(success ? "AI returned the ball!" : "AI missed!");

        // Reset one-time modifiers after use
        oneTimeBuff = 0f;
        oneTimeDebuff = 0f;
    }

    // Applies the buff, this is to be called from other scripts
    public void ApplyBuff(float amount, string spell)
    {
        pendingBuff = amount;
        currentSpellName = spell;
        UpdateSpellUI();
    }

    // Applies the debuff, this is to be called from other scripts
    public void ApplyDebuff(float amount, string spell)
    {
        pendingDebuff = amount;
        currentSpellName = spell;
        UpdateSpellUI();
    }

    // Call this once the buff/debuff has been consumed
    public void ClearEffects()
    {
        pendingBuff = 0f;
        pendingDebuff = 0f;
        currentSpellName = "";
        UpdateSpellUI();
    }

    // I'm not sure if this is actually doing anything but I don't wanna risk breaking it
    private void UpdateSpellUI()
    {
        Debug.Log("Current spell: " + currentSpellName);
    }
}
