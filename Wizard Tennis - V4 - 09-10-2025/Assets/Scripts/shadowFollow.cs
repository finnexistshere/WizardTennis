using System.Collections.Generic;
using UnityEngine;

public class shadowFollow : MonoBehaviour
{
    [Header("Follow Mode")]
    public FollowMode followMode = FollowMode.Ball;

    [Header("Manual Assignment")]
    public GameObject manualTarget;

    [Tooltip("The object this shadow is currently following.")]
    public GameObject follow;

    [Header("Storage")]
    [Tooltip("Where the shadow moves when it has no valid target")]
    public Vector3 storagePosition = new Vector3(0f, -50f, 0f);

    private SpriteRenderer spriteRenderer;
    private float groundY = 0.941f;

    private const float minScale = 1.0f;
    private const float maxScale = 3.0f;
    private const float maxHeight = 5f;

    private float nextSearchTime = 0f;
    private const float searchInterval = 0.5f;

    private static HashSet<GameObject> claimedPlayers = new HashSet<GameObject>();

    private bool isAssigned = false;
    private bool isChildOfPickup = false;

    public enum FollowMode
    {
        Ball,
        Player,
        SpellPickup
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        CheckIfChildOfPickup();

        if (manualTarget != null)
        {
            follow = manualTarget;
            isAssigned = true;
        }
        else
        {
            MoveToStorage();
        }
    }

    void Update()
    {
        // Pickup shadows never search
        if (isChildOfPickup && follow != null)
        {
            EnableVisuals();
            FollowTarget();
            return;
        }

        // Handle lost target (Ball destroyed, player despawned, etc)
        if (follow == null || !follow.activeInHierarchy)
        {
            HandleLostTarget();
        }

        // Periodic search
        if (!isAssigned && Time.time >= nextSearchTime)
        {
            nextSearchTime = Time.time + searchInterval;
            TryFindFollowTarget();
        }

        if (isAssigned && follow != null)
        {
            EnableVisuals();
            FollowTarget();
        }
        else
        {
            MoveToStorage();
        }
    }

    // ===================== CORE BEHAVIOR =====================

    private void FollowTarget()
    {
        transform.position = new Vector3(
            follow.transform.position.x,
            groundY,
            follow.transform.position.z
        );

        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        float height = follow.transform.position.y;
        float t = Mathf.InverseLerp(0, maxHeight, height);
        float scale = Mathf.Lerp(maxScale, minScale, t);
        transform.localScale = Vector3.one * scale;
    }

    private void HandleLostTarget()
    {
        if (followMode == FollowMode.Player && follow != null)
        {
            claimedPlayers.Remove(follow);
        }

        follow = null;
        isAssigned = false;

        DisableVisuals();
    }

    private void MoveToStorage()
    {
        transform.position = storagePosition;
        transform.localScale = Vector3.zero;
        DisableVisuals();
    }

    private void EnableVisuals()
    {
        if (spriteRenderer != null && !spriteRenderer.enabled)
            spriteRenderer.enabled = true;
    }

    private void DisableVisuals()
    {
        if (spriteRenderer != null && spriteRenderer.enabled)
            spriteRenderer.enabled = false;
    }

    // ===================== TARGET FINDING =====================

    private void TryFindFollowTarget()
    {
        if (manualTarget != null)
        {
            follow = manualTarget;
            isAssigned = true;
            return;
        }

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
            EnableVisuals();
            Debug.Log("[shadowFollow] Ball found and assigned.");
        }
    }

    private void AssignToPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            if (player == null || claimedPlayers.Contains(player))
                continue;

            claimedPlayers.Add(player);
            follow = player;
            isAssigned = true;
            EnableVisuals();
            Debug.Log($"[shadowFollow] Assigned to Player '{player.name}'");
            return;
        }
    }

    private void AssignToPickup()
    {
        if (transform.parent != null)
        {
            PickupEffect pickup = transform.parent.GetComponent<PickupEffect>();
            if (pickup != null)
            {
                follow = pickup.gameObject;
                isAssigned = true;
                isChildOfPickup = true;
                EnableVisuals();
            }
        }
    }

    // ===================== PICKUP DETECTION =====================

    private void CheckIfChildOfPickup()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        if (parent.GetComponent<PickupEffect>() != null)
        {
            isChildOfPickup = true;
            followMode = FollowMode.SpellPickup;
            follow = parent.gameObject;
            isAssigned = true;
        }
    }

    // ===================== PUBLIC API =====================

    public void SetFollowTarget(GameObject target)
    {
        if (target == null) return;

        if (followMode == FollowMode.Player && follow != null)
            claimedPlayers.Remove(follow);

        follow = target;
        isAssigned = true;
        EnableVisuals();
    }

    public void ResetTarget()
    {
        HandleLostTarget();
    }

    private void OnDestroy()
    {
        if (followMode == FollowMode.Player && follow != null)
            claimedPlayers.Remove(follow);
    }
}
