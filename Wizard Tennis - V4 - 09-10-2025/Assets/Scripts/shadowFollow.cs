using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shadowFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("If true, this shadow follows the ball. If false, it will follow the player.")]
    public bool followBallOnly = true;

    [Tooltip("The object this shadow should follow. If not assigned, the script will try to find one at runtime.")]
    public GameObject follow;

    private SpriteRenderer spriteRenderer;
    private float groundY = 0.941f;

    // Height-based scale control
    private const float minScale = 1.0f;   // Smallest shadow (when object is high)
    private const float maxScale = 3.0f;   // Largest shadow (when near ground)
    private const float maxHeight = 5f;    // Height at which the shadow reaches min size

    private float nextSearchTime = 0f;
    private const float searchInterval = 1.0f; // seconds between search attempts

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning("[shadowFollow] Missing SpriteRenderer component on " + gameObject.name);
        }

        // Hide shadow until valid target is found
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        TryFindFollowTarget();
    }

    void Update()
    {
        if (follow == null)
        {
            // Occasionally retry finding a target
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
        // Position shadow directly below the target, locked to ground height
        transform.position = new Vector3(follow.transform.position.x, groundY, follow.transform.position.z);
        transform.rotation = Quaternion.Euler(-90, 0, 0);

        // Scale shadow based on height difference
        float height = follow.transform.position.y;
        float t = Mathf.InverseLerp(0, maxHeight, height);
        float scale = Mathf.Lerp(maxScale, minScale, t);
        transform.localScale = new Vector3(scale, scale, scale);
    }

    private void TryFindFollowTarget()
    {
        if (followBallOnly)
        {
            // Try to use Ball.GetCurrentBall() first
            follow = Ball.GetCurrentBall();

            if (follow == null)
            {
                follow = GameObject.FindWithTag("Ball");
                if (follow != null)
                    Debug.Log("[shadowFollow] Found Ball via tag lookup.");
                else
                    Debug.LogWarning("[shadowFollow] Could not find Ball in scene.");
            }
            else
            {
                Debug.Log("[shadowFollow] Linked to Ball via Ball.GetCurrentBall().");
            }
        }
        else
        {
            follow = GameObject.FindWithTag("Player");
            if (follow != null)
                Debug.Log("[shadowFollow] Found Player via tag lookup.");
            else
                Debug.LogWarning("[shadowFollow] Could not find Player in scene.");
        }
    }
}
