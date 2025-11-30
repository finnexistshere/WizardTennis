using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(SpriteRenderer))]
public class SpellFloorImage : MonoBehaviour
{
    [Header("References")]
    public GameObject follow;                 // Object to follow
    public Spellcasting spellcasting;         // Reference to Spellcasting (singleplayer)
    public NetworkedSpellcasting networkedSpellcasting; // Reference to NetworkedSpellcasting (multiplayer)

    [Header("Visual Settings")]
    public float baseHeight = 0.941f;
    public float appearDuration = 0.4f;
    public float visibleDuration = 1.0f;
    public float fadeOutTime = 0.4f;
    public float spinSpeed = 180f;            // degrees per second
    public float maxScaleMultiplier = 1.4f;   // Final size when expanding

    private SpriteRenderer spriteRenderer;
    private Coroutine effectRoutine;
    private bool isVisible;
    private float spinAngle;                  // track spin independently
    private ulong assignedClientId = ulong.MaxValue; // Track which client this belongs to
    private bool isInitialized = false;

    // Tracking for assigned spell images in the Networking Scene
    private static readonly List<SpellFloorImage> allImages = new List<SpellFloorImage>();

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.zero;
    }

    void Start()
    {
        spriteRenderer.enabled = false;

        // Auto-assign to local player
        StartCoroutine(AutoAssignToPlayer());
    }

    private IEnumerator AutoAssignToPlayer()
    {
        // Wait until the network is active
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening
        );

        // MULTIPLAYER --> wait for both players to exist
        if (NetworkManager.Singleton.IsListening)
        {
            yield return StartCoroutine(WaitForBothNetworkedPlayers());

            AssignOnceBothPlayersExist();
            yield break;
        }

        // SINGLEPLAYER fallback
        Spellcasting sp = FindObjectOfType<Spellcasting>();
        if (sp != null)
            AssignToSinglePlayer(sp);
    }

    private IEnumerator WaitForBothNetworkedPlayers()
    {
        while (true)
        {
            NetworkedSpellcasting[] players = FindObjectsOfType<NetworkedSpellcasting>();

            if (players.Length >= 2)
                break;

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void AssignOnceBothPlayersExist()
    {
        NetworkedSpellcasting[] players = FindObjectsOfType<NetworkedSpellcasting>();

        // Identify the LOCAL player
        NetworkedSpellcasting localPlayer = null;
        foreach (var p in players)
            if (p.IsOwner)
                localPlayer = p;

        if (localPlayer == null)
        {
            Debug.LogWarning("[SpellFloorImage] No local player found even though 2 players exist?");
            return;
        }

        // Check if this client already has an assigned image
        foreach (var img in allImages)
        {
            if (img.networkedSpellcasting == localPlayer)
            {
                // Already assigned
                return;
            }
        }

        // Assign the first unassigned image to the local player
        if (!IsAssigned())
        {
            AssignToNetworkedPlayer(localPlayer);
            Debug.Log("[SpellFloorImage] Assigned AFTER both players existed.");
        }
    }

    /// <summary>
    /// Manually assign this floor image to a specific networked player
    /// </summary>
    public void AssignToNetworkedPlayer(NetworkedSpellcasting player)
    {
        if (player == null)
        {
            Debug.LogError("[SpellFloorImage] Cannot assign to null NetworkedSpellcasting!");
            return;
        }

        networkedSpellcasting = player;
        spellcasting = null; // Clear singleplayer reference

        // Get the client ID
        assignedClientId = player.OwnerClientId;

        // Set follow target to the player object (not the ball)
        follow = player.gameObject;

        isInitialized = true;

        Debug.Log($"[SpellFloorImage] Assigned to networked player {assignedClientId}");
    }

    /// <summary>
    /// Manually assign this floor image to a singleplayer player
    /// </summary>
    public void AssignToSinglePlayer(Spellcasting player)
    {
        if (player == null)
        {
            Debug.LogError("[SpellFloorImage] Cannot assign to null Spellcasting!");
            return;
        }

        spellcasting = player;
        networkedSpellcasting = null; // Clear multiplayer reference
        assignedClientId = ulong.MaxValue; // Not networked

        // Set follow target to the player object (not the ball)
        follow = player.gameObject;

        isInitialized = true;

        Debug.Log("[SpellFloorImage] Assigned to singleplayer player");
    }

    /// <summary>
    /// Check if this floor image belongs to the local player
    /// </summary>
    private bool IsLocalPlayer()
    {
        // Singleplayer mode - always show
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        // Multiplayer mode - check if assigned to local client
        if (networkedSpellcasting != null)
            return networkedSpellcasting.IsOwner;

        return false;
    }

    void Update()
    {
        // Only update if initialized and belongs to local player
        if (!isInitialized || !IsLocalPlayer())
            return;

        // Update follow target dynamically (in case ball changes)
        UpdateFollowTarget();

        if (follow != null)
        {
            transform.position = new Vector3(follow.transform.position.x, baseHeight, follow.transform.position.z);
        }

        // Spin only while visible
        if (isVisible)
        {
            spinAngle += spinSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(-90, spinAngle, 0);
        }
        else
        {
            // Keep facing downward when invisible
            transform.rotation = Quaternion.Euler(-90, 0, 0);
        }
    }

    /// <summary>
    /// Update the follow target to track the ball correctly
    /// </summary>
    private void UpdateFollowTarget()
    {
        // The floor image should follow the player, not the ball
        // This method is kept for backwards compatibility but doesn't need to do anything
        // since we set follow = player.gameObject in the assignment methods

        // If for some reason follow is null, try to reassign
        if (follow == null)
        {
            if (networkedSpellcasting != null)
                follow = networkedSpellcasting.gameObject;
            else if (spellcasting != null)
                follow = spellcasting.gameObject;
        }
    }

    /// <summary>
    /// Show spell effect - only if this belongs to the local player
    /// </summary>
    public void ShowSpell(string spellName, Color spellColor)
    {
        // Only show for local player
        if (!IsLocalPlayer())
            return;

        if (spriteRenderer == null)
        {
            Debug.LogError("[SpellFloorImage] SpriteRenderer is missing!");
            return;
        }

        spriteRenderer.color = spellColor;
        spriteRenderer.enabled = true;

        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
            effectRoutine = null;
        }

        effectRoutine = StartCoroutine(PlayEffect());
    }

    private IEnumerator PlayEffect()
    {
        if (spriteRenderer == null)
            yield break;

        isVisible = true;
        spriteRenderer.enabled = true;
        spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);

        float timer = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * maxScaleMultiplier;

        // Expand animation
        while (timer < appearDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / appearDuration);
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        transform.localScale = endScale;
        yield return new WaitForSeconds(visibleDuration);

        // Fade out
        timer = 0f;
        Color originalColor = spriteRenderer.color;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeOutTime);
            spriteRenderer.color = Color.Lerp(
                originalColor,
                new Color(originalColor.r, originalColor.g, originalColor.b, 0f),
                t
            );
            yield return null;
        }

        // Reset
        spriteRenderer.enabled = false;
        spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        transform.localScale = Vector3.zero;
        isVisible = false;
        effectRoutine = null;
        spinAngle = 0f; // reset rotation angle
    }

    /// <summary>
    /// Force hide the effect
    /// </summary>
    public void Hide()
    {
        if (effectRoutine != null)
        {
            StopCoroutine(effectRoutine);
            effectRoutine = null;
        }

        spriteRenderer.enabled = false;
        transform.localScale = Vector3.zero;
        isVisible = false;
        spinAngle = 0f;
    }

    /// <summary>
    /// Check if this floor image is properly assigned
    /// </summary>
    public bool IsAssigned()
    {
        return isInitialized && (spellcasting != null || networkedSpellcasting != null);
    }
}