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
        if (spellStatusText == null)
            spellStatusText = GameObject.Find(spellStatusName)?.GetComponent<TextMeshProUGUI>();

        if (rallyCountText == null)
            rallyCountText = GameObject.Find(rallyCountName)?.GetComponent<TextMeshProUGUI>();

        if (greenPointsText == null)
            greenPointsText = GameObject.Find(greenPointsNumbersName)?.GetComponent<TextMeshProUGUI>();

        if (greenPointsParent == null)
            greenPointsParent = GameObject.Find(greenPointsParentName)?.GetComponent<TextMeshProUGUI>();

        Debug.Log($"[NetworkedUIManager] Player {OwnerClientId} UI elements assigned:" +
                  $"\n - Spell Status: {(spellStatusText ? spellStatusText.name : "Not Found")}" +
                  $"\n - Rally Count: {(rallyCountText ? rallyCountText.name : "Not Found")}" +
                  $"\n - Green Points: {(greenPointsText ? greenPointsText.name : "Not Found")}" +
                  $"\n - Green Points Parent: {(greenPointsParent ? greenPointsParent.name : "Not Found")}");
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

        if (rallyPopRoutine != null)
            StopCoroutine(rallyPopRoutine);

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

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            t.localScale = Vector3.Lerp(originalScale, targetScale, timer / halfDuration);
            yield return null;
        }

        timer = 0f;

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            t.localScale = Vector3.Lerp(targetScale, originalScale, timer / halfDuration);
            yield return null;
        }

        t.localScale = originalScale;
    }

    private Color GetColorForValue(int value)
    {
        Color startColor = Color.white;
        Color midColor = Color.yellow;
        Color endColor = new Color(0.7f, 0f, 0f);
        Color endColor2 = Color.magenta;

        float t;
        if (value < 10)
        {
            t = Mathf.InverseLerp(0, 20, value);
            return Color.Lerp(startColor, midColor, t);
        }
        else if (value < 20)
        {
            t = Mathf.InverseLerp(20, 50, value);
            return Color.Lerp(midColor, endColor, t);
        }
        else
        {
            t = Mathf.InverseLerp(50, 80, value);
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