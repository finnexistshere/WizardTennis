using Unity.Netcode;
using UnityEngine;
using Netcode.Transports.Facepunch;

public class PlayerVoiceLink : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsOwner) return; // don't create playback for your own mic

        if (FacepunchVoiceManager.Instance != null)
        {
            FacepunchVoiceManager.Instance.RegisterPlayer(OwnerClientId, transform);
            Debug.Log("Manager found and Registered");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) return;

        if (FacepunchVoiceManager.Instance != null)
            FacepunchVoiceManager.Instance.UnregisterPlayer(OwnerClientId);
    }
}