using UnityEngine;
using Unity.Netcode;
using Steamworks;

/// <summary>
/// Place this script in your game scene (the scene that loads when game starts)
/// It automatically starts NetworkManager as host or client based on Steam lobby state
/// Includes fallback for Unity Editor testing without Steam/Lobby
/// </summary>
public class SteamGameSceneManager : MonoBehaviour
{
    [Header("Editor Testing Fallback")]
    [Tooltip("When true and no lobby is found, will start as host automatically (useful for editor testing)")]
    [SerializeField] private bool enableEditorFallback = true;

    [Tooltip("Auto-start mode when no lobby manager exists (Editor testing)")]
    [SerializeField] private FallbackMode fallbackMode = FallbackMode.Host;

    [Tooltip("Delay before starting fallback (gives time for lobby manager to initialize)")]
    [SerializeField] private float fallbackDelay = 0.5f;

    private NetworkManager netManager;
    private bool hasStarted = false;

    public enum FallbackMode
    {
        Host,
        Client,
        None // Don't auto-start
    }

    private void Start()
    {
        netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamGameScene] NetworkManager not found in scene!");
            return;
        }

        // Give lobby manager time to initialize if it exists
        Invoke(nameof(CheckAndStartNetwork), fallbackDelay);
    }

    private void CheckAndStartNetwork()
    {
        if (hasStarted) return;

        // Check if we're in a lobby and what role we should have
        if (SteamLobbyManager.Instance != null)
        {
            bool isHost = SteamLobbyManager.Instance.IsHost();
            CSteamID lobbyId = SteamLobbyManager.Instance.GetCurrentLobbyId();

            if (!lobbyId.IsValid())
            {
                Debug.LogWarning("[SteamGameScene] No valid Steam lobby found!");
                HandleFallback("No valid lobby");
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
            // No lobby manager found
            HandleFallback("SteamLobbyManager not found");
        }
    }

    private void HandleFallback(string reason)
    {
        if (!enableEditorFallback)
        {
            Debug.LogWarning($"[SteamGameScene] {reason} - Fallback disabled, not starting network");
            return;
        }

#if UNITY_EDITOR
        Debug.Log($"[SteamGameScene] {reason} - Using editor fallback mode: {fallbackMode}");
        
        switch (fallbackMode)
        {
            case FallbackMode.Host:
                Debug.Log("[SteamGameScene] ? EDITOR MODE: Starting as Host for testing");
                StartHost();
                break;
                
            case FallbackMode.Client:
                Debug.Log("[SteamGameScene] ? EDITOR MODE: Starting as Client for testing");
                StartClient();
                break;
                
            case FallbackMode.None:
                Debug.Log("[SteamGameScene] ? EDITOR MODE: Fallback set to None, not auto-starting");
                break;
        }
#else
        Debug.LogWarning($"[SteamGameScene] {reason} - Not in editor, cannot use fallback. Build should always have lobby manager!");
#endif
    }

    private void StartHost()
    {
        if (hasStarted)
        {
            Debug.LogWarning("[SteamGameScene] Already started network!");
            return;
        }

        Debug.Log("[SteamGameScene] Starting as Host...");
        if (netManager.StartHost())
        {
            hasStarted = true;
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
        if (hasStarted)
        {
            Debug.LogWarning("[SteamGameScene] Already started network!");
            return;
        }

        Debug.Log("[SteamGameScene] Starting as Client...");
        if (netManager.StartClient())
        {
            hasStarted = true;
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

        // Unlock gameplay systems
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.UnlockPickupSpawning();
        }
    }

    // Public method to manually start as host (useful for UI buttons in editor)
    public void ForceStartAsHost()
    {
        if (!hasStarted)
        {
            Debug.Log("[SteamGameScene] Forced start as Host");
            StartHost();
        }
    }

    // Public method to manually start as client (useful for UI buttons in editor)
    public void ForceStartAsClient()
    {
        if (!hasStarted)
        {
            Debug.Log("[SteamGameScene] Forced start as Client");
            StartClient();
        }
    }
}