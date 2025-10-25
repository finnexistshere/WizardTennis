using UnityEngine;
using TMPro;

public class SpellTextEntry : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI addressText;

    /// <summary>
    /// Sets both the spell name and address text.
    /// </summary>
    public void SetText(string spellName, string spellAddress)
    {
        if (nameText != null)
            nameText.text = spellName;
        else
            Debug.LogWarning("[SpellTextEntry] nameText is not assigned!");

        if (addressText != null)
            addressText.text = spellAddress;
        else
            Debug.LogWarning("[SpellTextEntry] addressText is not assigned!");
    }
}
