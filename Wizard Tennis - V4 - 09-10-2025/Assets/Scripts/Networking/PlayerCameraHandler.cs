using Unity.Netcode;
using UnityEngine;

public class PlayerCameraHandler : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera; // Assign your camera here

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            playerCamera.enabled = false;
            // Optionally, disable other camera-related components like AudioListener
            // if they are on the same GameObject or child GameObjects
            AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                audioListener.enabled = false;
            }
        }
    }
}