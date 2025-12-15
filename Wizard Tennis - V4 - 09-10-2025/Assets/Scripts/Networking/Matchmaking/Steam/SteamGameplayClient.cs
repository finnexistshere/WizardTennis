using UnityEngine;
using Unity.Netcode;
using Netcode.Transports.Facepunch;

public class SteamGameplayClient : MonoBehaviour
{
    [Header("Settings")]
    public bool verboseLogging = true;

    private void Start()
    {
        // Only run on clients
        if (SteamLobbyManager.Instance == null)
        {
            Debug.LogError("[SteamGameplayClient] No SteamLobbyManager found!");
            return;
        }

        if (SteamLobbyManager.Instance.IsHost())
        {
            Log("Host detected — no need to auto-connect.");
            return;
        }

        Log("Gameplay scene loaded — attempting to start client...");
        TryStartClient();
    }

    private void TryStartClient()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamGameplayClient] NetworkManager missing! Place a persistent NetworkManager in the scene.");
            return;
        }

        if (netManager.IsListening)
        {
            Log("NetworkManager already active — client may already be connected.");
            return;
        }

        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport == null)
        {
            Debug.LogError("[SteamGameplayClient] FacepunchTransport missing on NetworkManager!");
            return;
        }

        if (!SteamLobbyManager.Instance.currentLobby.HasValue)
        {
            Debug.LogWarning("[SteamGameplayClient] No current lobby found — cannot auto-connect.");
            return;
        }

        transport.targetSteamId = SteamLobbyManager.Instance.currentLobby.Value.Owner.Id;
        netManager.NetworkConfig.NetworkTransport = transport;

        try
        {
            bool started = netManager.StartClient();
            if (started)
            {
                Log("Client started successfully and connecting to host...");
            }
            else
            {
                Debug.LogError("[SteamGameplayClient] Failed to start client!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SteamGameplayClient] Exception starting client: " + e.Message);
        }
    }

    private void Log(string msg)
    {
        if (verboseLogging)
            Debug.Log("[SteamGameplayClient] " + msg);
    }
}
