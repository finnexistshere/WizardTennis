using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [SerializeField] private TextMeshProUGUI spellStatusText;
    [SerializeField] private TextMeshProUGUI rallyCountText;
    [SerializeField] private TextMeshProUGUI greenPointsText;

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

    public void UpdateRallyCount(int rallyCount)
    {
        if (rallyCountText == null) return;

        rallyCountText.text = $"Rally Count: {rallyCount}";
    }

    public void UpdateGreenPoints(int points)
    {
        if (greenPointsText == null) return;

        greenPointsText.text = $"Green Points: {points}";
    }
}
