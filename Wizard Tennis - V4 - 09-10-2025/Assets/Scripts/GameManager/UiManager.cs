using TMPro;
using UnityEngine;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [SerializeField] private TextMeshProUGUI spellStatusText;
    [SerializeField] private TextMeshProUGUI rallyCountText;
    [SerializeField] private TextMeshProUGUI greenPointsText;
    [SerializeField] private TextMeshProUGUI greenPointsParent;

    private Coroutine rallyPopRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateSpellStatus("None");
    }

    public void UpdateSpellStatus(string spellName, Color? spellColor = null)
    {
        if (spellStatusText == null) return;

        if (string.IsNullOrEmpty(spellName) || spellName == "None")
        {
            spellStatusText.text = "None";
            spellStatusText.color = Color.white; // Reset to neutral color
        }
        else
        {
            spellStatusText.text = $"{spellName}";
            if (spellColor.HasValue)
                spellStatusText.color = spellColor.Value;
        }
    }

    public void UpdateRallyCount(int rallyCount)
    {
        if (rallyCountText == null) return;

        rallyCountText.text = $"x{rallyCount}";
        rallyCountText.color = GetColorForValue(rallyCount);

        // Cancel any ongoing animation so multiple updates don't overlap
        if (rallyPopRoutine != null)
            StopCoroutine(rallyPopRoutine);

        rallyPopRoutine = StartCoroutine(PopText(rallyCountText, 1.25f, 0.15f));
    }

    public void UpdateGreenPoints(int points)
    {
        if (greenPointsText == null) return;

        // Activate the text object if it's inactive
        if (!greenPointsText.gameObject.activeSelf)
            greenPointsText.gameObject.SetActive(true);

        if (!greenPointsParent.gameObject.activeSelf)
            greenPointsParent.gameObject.SetActive(true);

        greenPointsText.text = $"{points}";
        greenPointsText.color = GetColorForValue(points);

        // Restart the pop animation
        if (rallyPopRoutine != null)
            StopCoroutine(rallyPopRoutine);

        rallyPopRoutine = StartCoroutine(PopText(greenPointsText, 1.25f, 0.15f));
    }

    private IEnumerator PopText(TMP_Text text, float popScale, float duration)
    {
        if (text == null) yield break;

        Transform t = text.transform;
        Vector3 originalScale = t.localScale;
        Vector3 targetScale = originalScale * popScale;
        float halfDuration = duration / 2f;
        float timer = 0f;

        // Scale up
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / halfDuration;
            t.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            yield return null;
        }

        timer = 0f;

        // Scale back down
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / halfDuration;
            t.localScale = Vector3.Lerp(targetScale, originalScale, progress);
            yield return null;
        }

        t.localScale = originalScale;
    }

    private Color GetColorForValue(int value)
    {
        // Define the gradient: white -> yellow -> crimson
        Color startColor = Color.white;
        Color midColor = Color.yellow;
        Color endColor = new Color(0.7f, 0f, 0f); // deep crimson
        Color endColor2 = Color.magenta;

        float t;
        if (value < 10)
        {
            // 0–10: white -> yellow
            t = Mathf.InverseLerp(0, 20, value);
            return Color.Lerp(startColor, midColor, t);
        }
        else if (value < 20)
        {
            // 10–30: yellow -> crimson
            t = Mathf.InverseLerp(20, 50, value);
            return Color.Lerp(midColor, endColor, t);
        }
        else
        {
            // 50 -> 80 Purple
            t = Mathf.InverseLerp(50, 80, value);
            return Color.Lerp(endColor, endColor2, t);
        }
    }
}
