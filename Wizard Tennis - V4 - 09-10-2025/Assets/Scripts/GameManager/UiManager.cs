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
    private Coroutine greenPointsPopRoutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        TryFindUIElements();

        UpdateSpellStatus("None");
    }

    private void TryFindUIElements()
    {
        if (spellStatusText == null)
            spellStatusText = GameObject.Find("SpellStatusText")?.GetComponent<TextMeshProUGUI>();

        if (rallyCountText == null)
            rallyCountText = GameObject.Find("RallyCountText")?.GetComponent<TextMeshProUGUI>();

        if (greenPointsText == null)
            greenPointsText = GameObject.Find("GreenPointsNumbers")?.GetComponent<TextMeshProUGUI>();

        if (greenPointsParent == null)
            greenPointsParent = GameObject.Find("GreenPointsText")?.GetComponent<TextMeshProUGUI>();

        Debug.Log($"[UIManager] UI elements assigned:" +
                  $"\n - Spell Status: {(spellStatusText ? spellStatusText.name : "Not Found")}" +
                  $"\n - Rally Count: {(rallyCountText ? rallyCountText.name : "Not Found")}" +
                  $"\n - Green Points: {(greenPointsText ? greenPointsText.name : "Not Found")}" +
                  $"\n - Green Points Parent: {(greenPointsParent ? greenPointsParent.name : "Not Found")}");
    }

    public void UpdateSpellStatus(string spellName, Color? spellColor = null)
    {
        if (spellStatusText == null)
        {
            Debug.LogWarning("[UIManager] spellStatusText is null!");
            return;
        }

        if (string.IsNullOrEmpty(spellName) || spellName == "None")
        {
            spellStatusText.text = "None";
            spellStatusText.color = Color.white;
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
        if (rallyCountText == null)
        {
            Debug.LogWarning("[UIManager] rallyCountText is null!");
            return;
        }

        rallyCountText.text = $"x{rallyCount}";
        rallyCountText.color = GetColorForValue(rallyCount);

        // Stop existing routine and ensure scale is reset before starting new pop
        if (rallyPopRoutine != null)
        {
            StopCoroutine(rallyPopRoutine);
            rallyCountText.transform.localScale = Vector3.one; // Reset to normal size
        }

        rallyPopRoutine = StartCoroutine(PopText(rallyCountText, 1.25f, 0.15f));
    }

    public void UpdateGreenPoints(int points)
    {
        if (greenPointsText == null)
        {
            Debug.LogWarning("[UIManager] greenPointsText is null!");
            return;
        }

        if (!greenPointsText.gameObject.activeSelf)
            greenPointsText.gameObject.SetActive(true);

        if (!greenPointsParent.gameObject.activeSelf)
            greenPointsParent.gameObject.SetActive(true);

        greenPointsText.text = $"{points}";
        greenPointsText.color = GetColorForValue(points);

        // Stop existing routine and ensure scale is reset before starting new pop
        if (greenPointsPopRoutine != null)
        {
            StopCoroutine(greenPointsPopRoutine);
            greenPointsText.transform.localScale = Vector3.one; // Reset to normal size
        }

        greenPointsPopRoutine = StartCoroutine(PopText(greenPointsText, 1.25f, 0.15f));
    }

    private IEnumerator PopText(TMP_Text text, float popScale, float duration)
    {
        if (text == null) yield break;

        Transform t = text.transform;
        Vector3 originalScale = Vector3.one; // Use consistent base scale
        Vector3 targetScale = originalScale * popScale;
        float halfDuration = duration / 2f;
        float timer = 0f;

        // Scale up
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            t.localScale = Vector3.Lerp(originalScale, targetScale, timer / halfDuration);
            yield return null;
        }

        timer = 0f;

        // Scale down
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            t.localScale = Vector3.Lerp(targetScale, originalScale, timer / halfDuration);
            yield return null;
        }

        // Ensure final scale is exactly the original
        t.localScale = originalScale;
    }

    private Color GetColorForValue(int value)
    {
        Color startColor = Color.white;
        Color midColor = Color.yellow;
        Color endColor = new Color(0.7f, 0f, 0f); // Dark red
        Color endColor2 = Color.magenta;

        if (value <= 10)
        {
            // 0-10: White to Yellow
            float t = Mathf.InverseLerp(0, 10, value);
            return Color.Lerp(startColor, midColor, t);
        }
        else if (value <= 20)
        {
            // 10-20: Yellow to Dark Red
            float t = Mathf.InverseLerp(10, 20, value);
            return Color.Lerp(midColor, endColor, t);
        }
        else
        {
            // 20+: Dark Red to Magenta
            float t = Mathf.InverseLerp(20, 50, value);
            t = Mathf.Clamp01(t); // Cap at 1.0 for values above 50
            return Color.Lerp(endColor, endColor2, t);
        }
    }
}