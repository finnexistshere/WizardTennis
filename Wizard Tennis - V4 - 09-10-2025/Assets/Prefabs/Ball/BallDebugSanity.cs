using UnityEngine;
using Unity.Netcode;

public class BallHitDebugSanity : NetworkBehaviour
{
    private Collider col;
    private Rigidbody rb;
    private NetworkObject netObj;

    private void Awake()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        netObj = GetComponent<NetworkObject>();
    }

    private void Start()
    {
        Invoke(nameof(ReportState), 0.25f); // allow spawn system to settle
    }

    private void ReportState()
    {
        string physicsMode = rb != null ? rb.collisionDetectionMode.ToString() : "none";

        Debug.Log(
            "[SANITY] --- REPORT (Client " + NetworkManager.Singleton.LocalClientId + ") ---\n" +
            "Object: " + gameObject.name + "\n" +
            "IsServer: " + IsServer + ", IsClient: " + IsClient + ", IsOwner: " + IsOwner + "\n" +
            "NetworkObjectID: " + (netObj != null ? netObj.NetworkObjectId : 999999) + "\n" +
            "NetworkObject present: " + (netObj != null) + "\n" +
            "Rb present: " + (rb != null) + "\n" +
            "Collider present: " + (col != null) + "\n" +
            "Collider enabled: " + (col != null && col.enabled) + "\n" +
            "Layer: " + LayerMask.LayerToName(gameObject.layer) + "\n" +
            "Rigidbody kinematic: " + (rb != null && rb.isKinematic) + "\n" +
            "Rigidbody gravity: " + (rb != null && rb.useGravity) + "\n" +
            "Physics Mode: " + physicsMode + "\n" +
            "Prefab registered in NetworkManager?: " + IsPrefabRegistered() + "\n" +
            "In PlayerPrefabs: " + IsInPlayerPrefabList() + "\n" +
            "------------------------------------------"
        );
    }

    private bool IsPrefabRegistered()
    {
        if (NetworkManager.Singleton == null)
            return false;

        foreach (var p in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
        {
            if (p.Prefab != null && p.Prefab.name == gameObject.name)
                return true;
        }
        return false;
    }

    private bool IsInPlayerPrefabList()
    {
        if (NetworkManager.Singleton == null)
            return false;

        var playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        if (playerPrefab == null)
            return false;

        foreach (var p in playerPrefab.GetComponentsInChildren<NetworkObject>())
        {
            if (p.name == gameObject.name)
                return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[SANITY] (Client " + NetworkManager.Singleton.LocalClientId + ") -> " +
                  gameObject.name + " OnTriggerEnter with " + other.name);
    }
}
