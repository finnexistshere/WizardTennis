using UnityEngine;
using Unity.Netcode;
using Steamworks; // Facepunch.Steamworks

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
        None
    }

    private void Start()
    {
        netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamGameScene] NetworkManager not found in scene!");
            return;
        }

        // Subscribe to network events for debugging
        netManager.OnClientConnectedCallback += OnClientConnectedCallback;
        netManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
        netManager.OnServerStarted += OnServerStartedCallback;

        Debug.Log("[SteamGameScene] ?? SteamGameSceneManager initialized");
        Debug.Log($"[SteamGameScene] Transport: {netManager.NetworkConfig.NetworkTransport?.GetType().Name ?? "None"}");

        // Give lobby manager time to initialize if it exists
        Invoke(nameof(CheckAndStartNetwork), fallbackDelay);
    }

    private void OnDestroy()
    {
        if (netManager != null)
        {
            netManager.OnClientConnectedCallback -= OnClientConnectedCallback;
            netManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            netManager.OnServerStarted -= OnServerStartedCallback;
        }
    }

    private void OnServerStartedCallback()
    {
        Debug.Log("[SteamGameScene] ?? SERVER STARTED!");
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        bool isLocalClient = clientId == netManager.LocalClientId;
        Debug.Log($"[SteamGameScene] ?? CLIENT CONNECTED! ClientID: {clientId} {(isLocalClient ? "(This is us!)" : "(Remote player)")}");
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        bool isLocalClient = clientId == netManager.LocalClientId;
        Debug.Log($"[SteamGameScene] ?? CLIENT DISCONNECTED! ClientID: {clientId} {(isLocalClient ? "(This is us!)" : "(Remote player)")}");
    }

    private void CheckAndStartNetwork()
    {
        if (hasStarted)
        {
            Debug.Log("[SteamGameScene] Already started network, skipping");
            return;
        }

        Debug.Log("[SteamGameScene] ?? Checking lobby state...");

        // With Facepunch, SteamLobbyManager handles all networking startup
        // This script just marks that we've initialized
        if (SteamLobbyManager.Instance != null)
        {
            Debug.Log("[SteamGameScene] ? SteamLobbyManager found");
            Debug.Log("[SteamGameScene] ? SteamLobbyManager will handle network startup");

            bool isHost = SteamLobbyManager.Instance.IsHost();
            Debug.Log($"[SteamGameScene] IsHost: {isHost}");

            hasStarted = true;
            OnGameStarted(isHost);
        }
        else
        {
            Debug.LogWarning("[SteamGameScene] ? SteamLobbyManager not found");
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
                Debug.Log("[SteamGameScene] ?? EDITOR MODE: Starting as Host for testing");
                StartHost();
                break;
                
            case FallbackMode.Client:
                Debug.Log("[SteamGameScene] ?? EDITOR MODE: Starting as Client for testing");
                StartClient();
                break;
                
            case FallbackMode.None:
                Debug.Log("[SteamGameScene] ?? EDITOR MODE: Fallback set to None, not auto-starting");
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

        Debug.Log("[SteamGameScene] ?? Starting as Host...");

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

        Debug.Log("[SteamGameScene] ?? Starting as Client...");

        // Note: With the new Facepunch setup, SteamLobbyManager handles
        // all the transport configuration internally
        Debug.Log("[SteamGameScene] Transport configuration handled by SteamLobbyManager");

        Debug.Log("[SteamGameScene] ?? Client will connect via SteamLobbyManager...");

        // Mark as started - the actual connection happens in SteamLobbyManager
        hasStarted = true;
        Debug.Log("[SteamGameScene] ? Client startup initiated");
        OnGameStarted(false);
    }

    private void OnGameStarted(bool isHost)
    {
        Debug.Log($"[SteamGameScene] ?? Game started! IsHost: {isHost}");

        // Optional: Any additional setup after network starts
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
            Debug.Log("[SteamGameScene] ?? Forced start as Host");
            StartHost();
        }
    }

    // Public method to manually start as client (useful for UI buttons in editor)
    public void ForceStartAsClient()
    {
        if (!hasStarted)
        {
            Debug.Log("[SteamGameScene] ?? Forced start as Client");
            StartClient();
        }
    }
}