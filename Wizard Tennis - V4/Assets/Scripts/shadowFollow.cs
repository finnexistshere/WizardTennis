using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shadowFollow : MonoBehaviour
{
    public bool followBallOnly = true;      // If true, this shadow is for the ball
    public GameObject follow;               // The object to follow (optional for editor assign)
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.enabled = false; // Hide shadow initially
    }

    void Update()
    {
        // If this shadow should follow the ball
        if (followBallOnly)
        {
            if (follow == null)
                follow = Ball.GetCurrentBall(); // Try to find the ball

            if (follow != null)
            {
                if (spriteRenderer != null && !spriteRenderer.enabled)
                    spriteRenderer.enabled = true; // Enable once ball exists

                // Position shadow (lock Y to ground)
                transform.position = new Vector3(follow.transform.position.x, 0.941f, follow.transform.position.z);
                transform.rotation = Quaternion.Euler(-90, 0, 0);

                // --- Scale based on height ---
                float ballHeight = follow.transform.position.y;

                // Define limits for scaling
                float minScale = 1.0f;   // Smallest shadow (when ball is high up)
                float maxScale = 3.0f;   // Largest shadow (when ball is near ground)
                float maxHeight = 5f;    // Height at which the shadow reaches minimum size

                // Calculate scale factor based on height
                float t = Mathf.InverseLerp(0, maxHeight, ballHeight);
                float scale = Mathf.Lerp(maxScale, minScale, t);

                // Apply uniform scale to shadow
                transform.localScale = new Vector3(scale, scale, scale);
            }
        }
        else if (follow != null)
        {
            // Optional: follow another object
            transform.position = new Vector3(follow.transform.position.x, 0.941f, follow.transform.position.z);
            transform.rotation = Quaternion.Euler(-90, 0, 0);
        }
    }
}
