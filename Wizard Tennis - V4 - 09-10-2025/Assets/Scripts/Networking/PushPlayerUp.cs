using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PushPlayerUpZone : MonoBehaviour
{
    public float pushHeight = 2f;
    public bool additive = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PushPlayerUpZone] Trigger entered by: {other.name} (Tag={other.tag})");

        if (!other.CompareTag("Player")) return;

        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogWarning("[PushPlayerUpZone] No NetworkObject found.");
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[PushPlayerUpZone] Not server ? sending RPC to server move.");
            RequestPushServerRpc(netObj.NetworkObjectId);
            return;
        }

        ApplyPush(netObj);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPushServerRpc(ulong targetId)
    {
        NetworkObject netObj = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId];
        ApplyPush(netObj);
    }

    private void ApplyPush(NetworkObject netObj)
    {
        Transform t = netObj.transform;
        Vector3 newPos = additive
            ? t.position + Vector3.up * pushHeight
            : new Vector3(t.position.x, pushHeight, t.position.z);

        Debug.Log($"[PushPlayerUpZone] Applying push to '{netObj.name}' ? {newPos}");

        // TRY NetworkTransform first
        NetworkTransform nt = netObj.GetComponent<NetworkTransform>();
        if (nt != null)
        {
            // Correct usage: Position, Rotation, Scale
            nt.Teleport(
                newPos,
                t.rotation,
                t.localScale
            );

            Debug.Log("[PushPlayerUpZone] Teleported via NetworkTransform.");
            return;
        }

        // Fallback if NetworkTransform missing
        t.position = newPos;
        Debug.Log("[PushPlayerUpZone] Teleport (manual) applied.");
    }
}
