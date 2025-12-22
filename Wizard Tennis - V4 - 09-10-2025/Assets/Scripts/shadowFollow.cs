using System.Collections.Generic;
using UnityEngine;

public class shadowFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("If true, this shadow follows the ball. If false, it will follow a player.")]
    public bool followBallOnly = true;

    [Tooltip("The object this shadow should follow.")]
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

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        TryFindFollowTarget();
    }

    void Update()
    {
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

    private void TryFindFollowTarget()
    {
        if (isAssigned)
            return;

        if (followBallOnly)
        {
            follow = Ball.GetCurrentBall();

            if (follow == null)
                follow = GameObject.FindWithTag("Ball");

            if (follow != null)
            {
                isAssigned = true;
                Debug.Log("[shadowFollow] Assigned to Ball.");
            }

            return;
        }

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

    private void OnDestroy()
    {
        // Clean up claim if this shadow is destroyed
        if (isAssigned && follow != null && claimedPlayers.Contains(follow))
        {
            claimedPlayers.Remove(follow);
            Debug.Log($"[shadowFollow] Released shadow from '{follow.name}'");
        }
    }
}
