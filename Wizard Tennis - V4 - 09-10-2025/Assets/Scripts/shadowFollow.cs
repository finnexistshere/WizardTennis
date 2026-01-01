using System.Collections.Generic;
using UnityEngine;

public class shadowFollow : MonoBehaviour
{
    [Header("Follow Mode")]
    [Tooltip("Determines what this shadow should follow")]
    public FollowMode followMode = FollowMode.Ball;

    [Header("Manual Assignment")]
    [Tooltip("Manually assign a target (optional). If set, this overrides automatic assignment.")]
    public GameObject manualTarget;

    [Tooltip("The object this shadow is currently following.")]
    public GameObject follow;

    private SpriteRenderer spriteRenderer;
    private float groundY = 0.941f;

    // Height-based scale control
    private const float minScale = 1.0f;
    private const float maxScale = 3.0f;
    private const float maxHeight = 5f;

    private float nextSearchTime = 0f;
    private const float searchInterval = 1.0f;

    // Global registry of claimed players
    private static HashSet<GameObject> claimedPlayers = new HashSet<GameObject>();

    private bool isAssigned = false;
    private bool isChildOfPickup = false;

    public enum FollowMode
    {
        Ball,           // Follow the tennis ball
        Player,         // Follow a player (one shadow per player)
        SpellPickup     // Follow the parent spell pickup
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        // Check if this shadow is a child of a spell pickup
        CheckIfChildOfPickup();

        // Try manual assignment first
        if (manualTarget != null)
        {
            follow = manualTarget;
            isAssigned = true;
            Debug.Log($"[shadowFollow] Manually assigned to '{manualTarget.name}'");
        }
        else
        {
            TryFindFollowTarget();
        }
    }

    void Update()
    {
        // If we're a child of a pickup and it's assigned, we don't need to search
        if (isChildOfPickup && follow != null)
        {
            if (spriteRenderer != null && !spriteRenderer.enabled)
                spriteRenderer.enabled = true;

            FollowTarget();
            return;
        }

        // Normal behavior for non-pickup shadows
        if (!isAssigned || follow == null)
        {
            if (Time.time >= nextSearchTime)
            {
                nextSearchTime = Time.time + searchInterval;
                TryFindFollowTarget();
            }
            return;
        }

        if (spriteRenderer != null && !spriteRenderer.enabled)
            spriteRenderer.enabled = true;

        FollowTarget();
    }

    private void FollowTarget()
    {
        if (follow == null)
            return;

        transform.position = new Vector3(
            follow.transform.position.x,
            groundY,
            follow.transform.position.z
        );
        transform.rotation = Quaternion.Euler(-90, 0, 0);

        float height = follow.transform.position.y;
        float t = Mathf.InverseLerp(0, maxHeight, height);
        float scale = Mathf.Lerp(maxScale, minScale, t);
        transform.localScale = Vector3.one * scale;
    }

    private void CheckIfChildOfPickup()
    {
        // Check if this shadow is a child of a spell pickup
        Transform parent = transform.parent;

        if (parent != null)
        {
            // Check for PickupEffect component (most reliable)
            if (parent.GetComponent<PickupEffect>() != null)
            {
                isChildOfPickup = true;
                followMode = FollowMode.SpellPickup;
                follow = parent.gameObject;
                isAssigned = true;

                Debug.Log($"[shadowFollow] Detected as child of PickupEffect '{parent.name}' - following parent");
                return;
            }

            // Fallback checks for other pickup types
            if (parent.CompareTag("SpellPickup") ||
                parent.name.ToLower().Contains("pickup") ||
                parent.name.ToLower().Contains("spell"))
            {
                isChildOfPickup = true;
                followMode = FollowMode.SpellPickup;
                follow = parent.gameObject;
                isAssigned = true;

                Debug.Log($"[shadowFollow] Detected as child of spell pickup '{parent.name}' - following parent");
            }
        }
    }

    private void TryFindFollowTarget()
    {
        if (isAssigned)
            return;

        // If manual target is set, use it
        if (manualTarget != null)
        {
            follow = manualTarget;
            isAssigned = true;
            Debug.Log($"[shadowFollow] Assigned to manual target '{manualTarget.name}'");
            return;
        }

        // Check mode
        switch (followMode)
        {
            case FollowMode.Ball:
                AssignToBall();
                break;

            case FollowMode.Player:
                AssignToPlayer();
                break;

            case FollowMode.SpellPickup:
                AssignToPickup();
                break;
        }
    }

    private void AssignToBall()
    {
        follow = Ball.GetCurrentBall();
        if (follow == null)
            follow = GameObject.FindWithTag("Ball");

        if (follow != null)
        {
            isAssigned = true;
            Debug.Log("[shadowFollow] Assigned to Ball.");
        }
    }

    private void AssignToPlayer()
    {
        // Player assignment (one shadow per player)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            if (player == null)
                continue;

            if (claimedPlayers.Contains(player))
                continue;

            // Claim this player
            claimedPlayers.Add(player);
            follow = player;
            isAssigned = true;
            Debug.Log($"[shadowFollow] Assigned to Player '{player.name}'");
            return;
        }

        Debug.Log("[shadowFollow] No available player without a shadow found.");
    }

    private void AssignToPickup()
    {
        // Check if we have a parent first (most common case)
        if (transform.parent != null)
        {
            // Prioritize PickupEffect component
            if (transform.parent.GetComponent<PickupEffect>() != null)
            {
                follow = transform.parent.gameObject;
                isAssigned = true;
                isChildOfPickup = true;
                Debug.Log($"[shadowFollow] Assigned to PickupEffect parent '{follow.name}'");
                return;
            }

            // Fallback to generic pickup detection
            if (transform.parent.CompareTag("SpellPickup") ||
                transform.parent.name.ToLower().Contains("pickup"))
            {
                follow = transform.parent.gameObject;
                isAssigned = true;
                isChildOfPickup = true;
                Debug.Log($"[shadowFollow] Assigned to spell pickup parent '{follow.name}'");
                return;
            }
        }

        // If no parent, try to find a nearby pickup
        PickupEffect[] pickups = FindObjectsOfType<PickupEffect>();

        if (pickups.Length > 0)
        {
            float closestDistance = float.MaxValue;
            GameObject closestPickup = null;

            foreach (PickupEffect pickup in pickups)
            {
                float distance = Vector3.Distance(transform.position, pickup.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPickup = pickup.gameObject;
                }
            }

            // If we found a close pickup (within 2 units), follow it
            if (closestPickup != null && closestDistance < 2f)
            {
                follow = closestPickup;
                isAssigned = true;
                Debug.Log($"[shadowFollow] Assigned to nearby PickupEffect '{closestPickup.name}' at distance {closestDistance:F2}");
                return;
            }
        }

        Debug.Log("[shadowFollow] No PickupEffect parent or nearby pickup found.");
    }

    /// <summary>
    /// Public method to manually assign a target at runtime
    /// </summary>
    public void SetFollowTarget(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[shadowFollow] Attempted to set null target");
            return;
        }

        // Clean up old claim if we were following a player
        if (followMode == FollowMode.Player && follow != null && claimedPlayers.Contains(follow))
        {
            claimedPlayers.Remove(follow);
        }

        follow = target;
        isAssigned = true;

        Debug.Log($"[shadowFollow] Manually set target to '{target.name}'");
    }

    /// <summary>
    /// Reset the shadow to search for a new target
    /// </summary>
    public void ResetTarget()
    {
        // Clean up old claim
        if (followMode == FollowMode.Player && follow != null && claimedPlayers.Contains(follow))
        {
            claimedPlayers.Remove(follow);
        }

        follow = null;
        isAssigned = false;
        isChildOfPickup = false;

        Debug.Log("[shadowFollow] Target reset - will search for new target");
    }

    private void OnDestroy()
    {
        // Clean up claim if this shadow is destroyed
        if (followMode == FollowMode.Player && isAssigned && follow != null && claimedPlayers.Contains(follow))
        {
            claimedPlayers.Remove(follow);
            Debug.Log($"[shadowFollow] Released shadow from '{follow.name}'");
        }
    }

    // Optional: Visualize the follow target in editor
    private void OnDrawGizmosSelected()
    {
        if (follow != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, follow.transform.position);
            Gizmos.DrawWireSphere(follow.transform.position, 0.5f);
        }
    }
}