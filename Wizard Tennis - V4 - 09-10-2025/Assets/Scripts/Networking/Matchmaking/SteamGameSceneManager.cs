using UnityEngine;
using Unity.Netcode;
using Steamworks;

/// <summary>
/// Place this script in your game scene (the scene that loads when game starts)
/// It handles post-connection setup and ensures players are properly initialized
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

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private NetworkManager netManager;
    private bool hasInitialized = false;

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

        Log("SteamGameSceneManager initialized");
        Log($"Transport: {netManager.NetworkConfig.NetworkTransport?.GetType().Name ?? "None"}");
        Log($"IsListening: {netManager.IsListening}");
        Log($"IsHost: {netManager.IsHost}");
        Log($"IsClient: {netManager.IsClient}");
        Log($"IsServer: {netManager.IsServer}");

        // Subscribe to network events for debugging
        netManager.OnClientConnectedCallback += OnClientConnectedCallback;
        netManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
        netManager.OnServerStarted += OnServerStartedCallback;

        // Give a moment for everything to settle, then check network state
        Invoke(nameof(CheckNetworkState), fallbackDelay);
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
        Log("SERVER STARTED!");
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        if (clientId == netManager.LocalClientId)
        {
            Log("Local client connected - initializing game");
            OnLocalClientReady();
        }
        else
        {
            Log($"Remote client {clientId} connected");
        }
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        bool isLocalClient = clientId == netManager.LocalClientId;
        Log($"CLIENT DISCONNECTED! ClientID: {clientId} {(isLocalClient ? "(This is us!)" : "(Remote player)")}");
    }

    private void CheckNetworkState()
    {
        if (hasInitialized)
        {
            Log("Already initialized, skipping");
            return;
        }

        Log("Checking network state...");

        // Check if we're already connected (normal case when using SteamLobbyManager)
        if (netManager.IsListening)
        {
            Log($"NetworkManager already listening - IsHost: {netManager.IsHost}, IsClient: {netManager.IsClient}");

            if (SteamLobbyManager.Instance != null)
            {
                bool isHost = SteamLobbyManager.Instance.IsHost();
                Log($"SteamLobbyManager found - IsHost: {isHost}");
                hasInitialized = true;
                OnGameReady(isHost);
            }
            else
            {
                Log("No SteamLobbyManager but network is active (editor mode?)");
                hasInitialized = true;
                OnGameReady(netManager.IsHost);
            }
        }
        else
        {
            // Not connected - try fallback for editor testing
            Log("NetworkManager NOT listening");

            if (SteamLobbyManager.Instance != null)
            {
                Debug.LogWarning("[SteamGameScene] SteamLobbyManager exists but network isn't started - this shouldn't happen!");
            }

            HandleFallback("Network not started");
        }
    }

    private void OnLocalClientReady()
    {
        if (hasInitialized) return;

        Log("Local client ready - performing post-connection setup");
        hasInitialized = true;

        bool isHost = netManager.IsHost;
        OnGameReady(isHost);
    }

    private void OnGameReady(bool isHost)
    {
        Log($"Game ready! IsHost: {isHost}");

        // Unlock pickup spawning (server only)
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.RequestUnlockPickupSpawning();
            Log("Pickup spawning unlocked");
        }

        // Additional client-specific setup
        if (!isHost)
        {
            SetupLocalClient();
        }
    }

    /// <summary>
    /// Perform any client-specific setup needed after scene loads
    /// </summary>
    private void SetupLocalClient()
    {
        Log("Setting up local client...");

        // Wait a frame for player spawning to complete
        StartCoroutine(WaitForLocalPlayerSetup());
    }

    private System.Collections.IEnumerator WaitForLocalPlayerSetup()
    {
        // Wait for player object to be assigned
        int attempts = 0;
        while (netManager.LocalClient?.PlayerObject == null && attempts < 100)
        {
            attempts++;
            yield return null;
        }

        if (netManager.LocalClient?.PlayerObject == null)
        {
            Debug.LogError("[SteamGameScene] Local player object never spawned!");
            yield break;
        }

        GameObject localPlayer = netManager.LocalClient.PlayerObject.gameObject;
        Log($"Local player found: {localPlayer.name}");

        // Ensure camera is properly assigned
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Check if player has a camera follower or similar component
            var cameraFollow = localPlayer.GetComponentInChildren<Camera>();
            if (cameraFollow == null)
            {
                // If player doesn't have its own camera, make sure main camera follows it
                Log("No camera on player - main camera should be set up separately");
            }
            else
            {
                Log("Player has camera component");
            }
        }

        // Disable any components that should only be active on this client's player
        // (This depends on your player setup - you may need to add more here)

        Log("Client setup complete");
    }

    private void HandleFallback(string reason)
    {
        if (!enableEditorFallback)
        {
            Debug.LogWarning($"[SteamGameScene] {reason} - Fallback disabled, not starting network");
            return;
        }

#if UNITY_EDITOR
        Log($"{reason} - Using editor fallback mode: {fallbackMode}");
        
        switch (fallbackMode)
        {
            case FallbackMode.Host:
                Log("EDITOR MODE: Starting as Host for testing");
                StartHost();
                break;
                
            case FallbackMode.Client:
                Log("EDITOR MODE: Starting as Client for testing");
                Debug.LogWarning("[SteamGameScene] Client mode in editor without lobby is not recommended");
                break;
                
            case FallbackMode.None:
                Log("EDITOR MODE: Fallback set to None, not auto-starting");
                break;
        }
#else
        Debug.LogWarning($"[SteamGameScene] {reason} - Not in editor, cannot use fallback. Build should always have lobby manager!");
#endif
    }

    private void StartHost()
    {
        if (hasInitialized)
        {
            Debug.LogWarning("[SteamGameScene] Already initialized!");
            return;
        }

        Log("Starting as Host (editor fallback)...");

        if (netManager.StartHost())
        {
            hasInitialized = true;
            Log("Successfully started as Host");
            OnGameReady(true);
        }
        else
        {
            Debug.LogError("[SteamGameScene] Failed to start as Host");
        }
    }

    private void Log(string msg)
    {
        if (verboseLogging)
            Debug.Log($"[SteamGameScene] {msg}");
    }

    // Public methods for manual control (useful for UI buttons in editor)
    public void ForceStartAsHost()
    {
        if (!hasInitialized)
        {
            Log("Forced start as Host");
            StartHost();
        }
    }
}