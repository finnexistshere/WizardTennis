using Unity.Netcode;
using UnityEngine;

public class NetworkDebugLogger : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NETCODE] No NetworkManager in scene!");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += id =>
        {
            Debug.Log($"[NETCODE] Client connected: {id}");
        };

        NetworkManager.Singleton.OnClientDisconnectCallback += id =>
        {
            Debug.LogError($"[NETCODE] Client disconnected: {id}");
        };
    }
}
