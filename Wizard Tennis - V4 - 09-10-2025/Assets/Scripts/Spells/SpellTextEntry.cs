using UnityEngine;
using TMPro;

public class SpellTextEntry : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI addressText;

    private Spellcasting spellcasting;

    private void Awake()
    {
        // Try to find it, but don't require it
        spellcasting = FindObjectOfType<Spellcasting>();
    }

    /// <summary>
    /// Sets both the spell name and address text.
    /// Uses spellcasting color formatting if available,
    /// otherwise falls back to default text.
    /// </summary>
    public void SetText(string spellName, string spellAddress)
    {
        if (nameText != null)
            nameText.text = spellName;

        if (addressText != null)
        {
            // Use coloured version if possible
            if (spellcasting != null)
                addressText.text = spellcasting.GetColorizedAddress(spellAddress);
            else
                addressText.text = spellAddress; // fallback
        }
    }
}
