using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private MonoBehaviour[] localOnlyComponents;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Disable camera & controls on remote players
            if (playerCamera != null)
                playerCamera.enabled = false;

            foreach (var component in localOnlyComponents)
                component.enabled = false;
        }
    }
}
