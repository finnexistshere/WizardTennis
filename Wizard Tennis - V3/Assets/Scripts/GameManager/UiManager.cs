using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [SerializeField] private TextMeshProUGUI spellStatusText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Set the Default values at Start
        UpdateSpellStatus("None", 0f);
    }

    public void UpdateSpellStatus(string spellName, float debuffValue)
    {
        if (spellStatusText == null) return;

        if (string.IsNullOrEmpty(spellName) || debuffValue == 0f)
        {
            spellStatusText.text = "Current Spell: None";
        }
        else
        {
            spellStatusText.text = $"Spell: {spellName}";
        }
    }
}
