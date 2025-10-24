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

        UpdateSpellStatus("None");
    }

    public void UpdateSpellStatus(string spellName)
    {
        if (spellStatusText == null) return;

        if (string.IsNullOrEmpty(spellName) || spellName == "None")
            spellStatusText.text = "Current Spell: None";
        else
            spellStatusText.text = $"Current Spell: {spellName}";
    }
}
