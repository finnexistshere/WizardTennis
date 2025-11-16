using Unity.Netcode;
using UnityEngine;

public class CameraFollowElastic : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private Transform player;
    [SerializeField] private float followHeight = 5f;
    [SerializeField] private float followDistance = 10f;
    [SerializeField] private float maxSway = 2f;
    [SerializeField] private float swayStrength = 0.3f;
    [SerializeField] private float smoothSpeed = 5f;

    private Vector3 startPosition;

    void Awake()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        startPosition = player.position - player.forward * followDistance + Vector3.up * followHeight;

        if (playerCamera != null) playerCamera.enabled = true;
        if (audioListener != null) audioListener.enabled = true;
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Elastic sway along X
        float xOffset = (player.position.x - startPosition.x) * swayStrength;
        xOffset = Mathf.Clamp(xOffset, -maxSway, maxSway);

        // Target position behind player
        Vector3 targetPos = player.position - player.forward * followDistance + Vector3.up * followHeight;
        targetPos.x += xOffset;

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
        transform.LookAt(player.position + Vector3.up * 1.5f);
    }
}
