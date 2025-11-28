using UnityEngine;
using Unity.Netcode;
using Steamworks;

/// <summary>
/// Place this script in your game scene (the scene that loads when game starts)
/// It automatically starts NetworkManager as host or client based on Steam lobby state
/// </summary>
public class SteamGameSceneManager : MonoBehaviour
{
    private NetworkManager netManager;

    private void Start()
    {
        netManager = NetworkManager.Singleton;

        if (netManager == null)
        {
            Debug.LogError("[SteamGameScene] NetworkManager not found in scene!");
            return;
        }

        // Check if we're in a lobby and what role we should have
        if (SteamLobbyManager.Instance != null)
        {
            bool isHost = SteamLobbyManager.Instance.IsHost();
            CSteamID lobbyId = SteamLobbyManager.Instance.GetCurrentLobbyId();

            if (!lobbyId.IsValid())
            {
                Debug.LogWarning("[SteamGameScene] No valid Steam lobby found!");
                return;
            }

            if (isHost)
            {
                StartHost();
            }
            else
            {
                StartClient();
            }
        }
        else
        {
            Debug.LogWarning("[SteamGameScene] SteamLobbyManager not found - starting as host by default");
            StartHost();
        }
    }

    private void StartHost()
    {
        Debug.Log("[SteamGameScene] Starting as Host...");

        if (netManager.StartHost())
        {
            Debug.Log("[SteamGameScene] ? Successfully started as Host");
            OnGameStarted(true);
        }
        else
        {
            Debug.LogError("[SteamGameScene] ? Failed to start as Host");
        }
    }

    private void StartClient()
    {
        Debug.Log("[SteamGameScene] Starting as Client...");

        if (netManager.StartClient())
        {
            Debug.Log("[SteamGameScene] ? Successfully started as Client");
            OnGameStarted(false);
        }
        else
        {
            Debug.LogError("[SteamGameScene] ? Failed to start as Client");
        }
    }

    private void OnGameStarted(bool isHost)
    {
        // Optional: Any additional setup after network starts
        Debug.Log($"[SteamGameScene] Game started! IsHost: {isHost}");

        // You can unlock gameplay here, spawn players, etc.
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.UnlockPickupSpawning();
        }
    }
}