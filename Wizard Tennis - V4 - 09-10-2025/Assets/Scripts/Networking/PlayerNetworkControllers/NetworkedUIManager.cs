using TMPro;
using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkedUIManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI spellStatusText;
    [SerializeField] private TextMeshProUGUI rallyCountText;
    [SerializeField] private TextMeshProUGUI greenPointsText;
    [SerializeField] private TextMeshProUGUI greenPointsParent;

    [Header("UI Object Names (for auto-finding)")]
    [SerializeField] private string spellStatusName = "SpellStatusText";
    [SerializeField] private string rallyCountName = "RallyCountText";
    [SerializeField] private string greenPointsNumbersName = "GreenPointsNumbers";
    [SerializeField] private string greenPointsParentName = "GreenPointsText";

    private Coroutine rallyPopRoutine;
    private Coroutine greenPointsPopRoutine;

    public override void OnNetworkSpawn()
    {
        // Only the owner of this player manages their UI
        if (!IsOwner) return;

        TryFindUIElements();
        UpdateSpellStatus("None");

        Debug.Log($"[NetworkedUIManager] Player {OwnerClientId} UI initialized.");
    }

    private void TryFindUIElements()
    {
        // Searches ALL objects, even disabled, across entire scene
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();

        if (spellStatusText == null)
            spellStatusText = FindUIByName(allTexts, spellStatusName);

        if (rallyCountText == null)
            rallyCountText = FindUIByName(allTexts, rallyCountName);

        if (greenPointsText == null)
            greenPointsText = FindUIByName(allTexts, greenPointsNumbersName);

        if (greenPointsParent == null)
            greenPointsParent = FindUIByName(allTexts, greenPointsParentName);

        Debug.Log($"[NetworkedUIManager] Player {OwnerClientId} UI elements assigned:" +
                  $"\n - Spell Status: {(spellStatusText ? spellStatusText.name : "Not Found")}" +
                  $"\n - Rally Count: {(rallyCountText ? rallyCountText.name : "Not Found")}" +
                  $"\n - Green Points: {(greenPointsText ? greenPointsText.name : "Not Found")}" +
                  $"\n - Green Points Parent: {(greenPointsParent ? greenPointsParent.name : "Not Found")}");
    }

    // Helper method
    private TextMeshProUGUI FindUIByName(TextMeshProUGUI[] list, string targetName)
    {
        foreach (var t in list)
        {
            if (t != null && t.name == targetName)
                return t;
        }
        return null;
    }


    public void UpdateSpellStatus(string spellName, Color? spellColor = null)
    {
        if (!IsOwner) return; // Only update your own UI

        if (spellStatusText == null)
        {
            Debug.LogWarning($"[NetworkedUIManager] Player {OwnerClientId} spellStatusText is null!");
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
        if (!IsOwner) return; // Only update your own UI

        if (rallyCountText == null)
        {
            Debug.LogWarning($"[NetworkedUIManager] Player {OwnerClientId} rallyCountText is null!");
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
        if (!IsOwner) return; // Only update your own UI

        if (greenPointsText == null)
        {
            Debug.LogWarning($"[NetworkedUIManager] Player {OwnerClientId} greenPointsText is null!");
            return;
        }

        if (!greenPointsText.gameObject.activeSelf)
            greenPointsText.gameObject.SetActive(true);

        if (greenPointsParent != null && !greenPointsParent.gameObject.activeSelf)
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

    // Helper method for other scripts to get this player's UI manager
    public static NetworkedUIManager GetLocalPlayerUI()
    {
        var allManagers = FindObjectsOfType<NetworkedUIManager>();
        foreach (var manager in allManagers)
        {
            if (manager.IsOwner)
                return manager;
        }
        return null;
    }
}