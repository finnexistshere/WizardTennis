using UnityEngine;

public class CameraElasticSway : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform player; // player to follow
    public Transform spawnPoint; // optional start point

    [Header("Camera Settings")]
    public float followHeight = 5f;
    public float followDistance = 10f;
    public float maxSway = 2f;
    public float swayStrength = 0.3f;
    public float smoothSpeed = 5f;

    private Vector3 startPosition;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // Initialize position
        if (spawnPoint != null)
            transform.position = spawnPoint.position;
        else if (player != null)
            transform.position = player.position - player.forward * followDistance + Vector3.up * followHeight;

        startPosition = transform.position;
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Elastic sway along X
        float xOffset = (player.position.x - startPosition.x) * swayStrength;
        xOffset = Mathf.Clamp(xOffset, -maxSway, maxSway);

        Vector3 targetPos = player.position - player.forward * followDistance + Vector3.up * followHeight;
        targetPos.x += xOffset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);

        // Always look at player
        transform.LookAt(player.position + Vector3.up * 1.5f);
    }
}
