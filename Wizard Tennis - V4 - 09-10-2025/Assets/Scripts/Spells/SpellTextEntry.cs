using UnityEngine;
using TMPro;

public class SpellTextEntry : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI spellNameText;
    public TextMeshProUGUI spellAddressText;

    // Cached references (set once, reused)
    private static ISpellcasting cachedSpellcasting;
    private static bool hasCached = false;

    public void SetText(string spellName, string spellAddress)
    {
        // Update spell name
        if (spellNameText != null)
        {
            spellNameText.text = spellName;
        }

        // Update spell address with colors
        if (spellAddressText != null)
        {
            // Get the spellcasting system (cached for performance)
            ISpellcasting spellcasting = GetSpellcastingSystem();

            if (spellcasting != null)
            {
                // Try to get colorized address
                string colorizedAddress = GetColorizedAddressFromSpellcasting(spellcasting, spellAddress);
                spellAddressText.text = colorizedAddress;
            }
            else
            {
                // Fallback: plain text
                spellAddressText.text = spellAddress;
            }
        }
    }

    /// <summary>
    /// Gets the active spellcasting system (NetworkedSpellcasting or Spellcasting)
    /// Caches the result for performance
    /// </summary>
    private ISpellcasting GetSpellcastingSystem()
    {
        // Return cached reference if available
        if (hasCached && cachedSpellcasting != null)
        {
            return cachedSpellcasting;
        }

        // Try NetworkedSpellcasting first (multiplayer)
        NetworkedSpellcasting networkedSC = FindObjectOfType<NetworkedSpellcasting>();
        if (networkedSC != null)
        {
            cachedSpellcasting = networkedSC;
            hasCached = true;
            Debug.Log("[SpellTextEntry] Found NetworkedSpellcasting system");
            return cachedSpellcasting;
        }

        // Fallback: Try regular Spellcasting (single player)
        Spellcasting regularSC = FindObjectOfType<Spellcasting>();
        if (regularSC != null)
        {
            cachedSpellcasting = regularSC;
            hasCached = true;
            Debug.Log("[SpellTextEntry] Found Spellcasting system");
            return cachedSpellcasting;
        }

        Debug.LogWarning("[SpellTextEntry] No spellcasting system found!");
        return null;
    }

    /// <summary>
    /// Gets colorized address from the spellcasting system
    /// </summary>
    private string GetColorizedAddressFromSpellcasting(ISpellcasting spellcasting, string address)
    {
        // Try NetworkedSpellcasting first
        if (spellcasting is NetworkedSpellcasting networkedSC)
        {
            return networkedSC.GetColorizedAddress(address);
        }

        // Try regular Spellcasting
        if (spellcasting is Spellcasting regularSC)
        {
            return regularSC.GetColorizedAddress(address);
        }

        // Fallback: return plain address
        return address;
    }

    /// <summary>
    /// Reset cache when scene changes or object is destroyed
    /// </summary>
    private void OnDestroy()
    {
        // Clear cache when this object is destroyed
        // (next SpellTextEntry will re-cache)
        cachedSpellcasting = null;
        hasCached = false;
    }

    /// <summary>
    /// Force refresh the spellcasting reference
    /// Call this if the spellcasting system changes mid-game
    /// </summary>
    public static void ClearCache()
    {
        cachedSpellcasting = null;
        hasCached = false;
        Debug.Log("[SpellTextEntry] Cache cleared - will re-find spellcasting system");
    }
}