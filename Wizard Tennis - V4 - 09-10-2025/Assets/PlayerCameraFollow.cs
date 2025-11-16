using UnityEngine;
using Unity.Netcode;

public class PlayerCameraFollow : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera playerCamera; // Assign in Inspector
    [SerializeField] private float maxSway = 2f;
    [SerializeField] private float swayStrength = 0.3f;
    [SerializeField] private float smoothSpeed = 5f;

    private Transform targetPlayer;
    private Vector3 startPosition;

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer)
        {
            // Only enable the camera for the local player
            playerCamera.gameObject.SetActive(false);
            return;
        }

        // Assign the target as this player's transform
        targetPlayer = transform;

        // Enable local player camera
        playerCamera.gameObject.SetActive(true);

        // Start position of camera
        startPosition = playerCamera.transform.position;
    }

    void LateUpdate()
    {
        if (targetPlayer == null || !IsLocalPlayer)
            return;

        // Elastic sway only in X-axis
        float xOffset = (targetPlayer.position.x - startPosition.x) * swayStrength;
        xOffset = Mathf.Clamp(xOffset, -maxSway, maxSway);

        Vector3 targetPos = new Vector3(startPosition.x + xOffset, startPosition.y, startPosition.z);

        playerCamera.transform.position = Vector3.Lerp(playerCamera.transform.position, targetPos, Time.deltaTime * smoothSpeed);

        // Optional: look at player’s chest height
        playerCamera.transform.LookAt(targetPlayer.position + Vector3.up * 2);
    }
}
