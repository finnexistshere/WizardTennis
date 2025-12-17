using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public int maxPlayers = 4;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public event Action<List<string>> OnPlayerListChanged;
    public event Action OnJoinedLobby;
    public event Action OnConnectionFailed;
    public event Action<string> OnLobbyCodeGenerated;

    public Lobby? currentLobby;
    private bool isHost = false;
    private bool hasStartedNetwork = false;

    private bool isPollingLobby = false;
    private float pollTimer = 0f;
    private const float POLL_INTERVAL = 0.5f;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!FacepunchSteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized!");
            return;
        }

        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataUpdate;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void Update()
    {
        if (!isPollingLobby || !currentLobby.HasValue || IsHost())
            return;

        pollTimer -= Time.deltaTime;
        if (pollTimer > 0f)
            return;

        pollTimer = POLL_INTERVAL;
        currentLobby.Value.Refresh();

        string inGame = currentLobby.Value.GetData("in_game");
        if (inGame == "true")
        {
            Log("Detected game start — host will load scene via Netcode");
            isPollingLobby = false;
        }
    }

    #region Public API

    public async void CreateLobby(string lobbyName, int maxPlayers)
    {
        isHost = true;
        this.maxPlayers = maxPlayers;

        var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
        if (!lobby.HasValue)
        {
            OnConnectionFailed?.Invoke();
            return;
        }

        currentLobby = lobby.Value;
        currentLobby.Value.SetJoinable(true);
        currentLobby.Value.SetData("name", lobbyName);
        currentLobby.Value.SetData("in_game", "false");
        currentLobby.Value.SetData("host_id", SteamClient.SteamId.ToString());

        OnLobbyCodeGenerated?.Invoke(currentLobby.Value.Id.ToString());
        OnJoinedLobby?.Invoke();
        RefreshLobbyMembers();

        // FIX #1: Start host server immediately so clients can connect when they join
        Log("Starting host server for incoming connections...");
        StartCoroutine(StartHostServerOnly());
    }

    public async void JoinLobby(SteamId lobbyId)
    {
        isHost = false;

        var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
        if (!lobby.HasValue)
        {
            OnConnectionFailed?.Invoke();
            return;
        }

        currentLobby = lobby.Value;
        currentLobby.Value.Refresh();
        RefreshLobbyMembers();
        OnJoinedLobby?.Invoke();

        Log("Successfully joined lobby, starting client...");

        // FIX #1: Now the host server is already running, so client can connect immediately
        StartClientForLobby();

        // Start polling to detect when host starts the game
        string inGame = currentLobby.Value.GetData("in_game");
        if (inGame != "true")
        {
            isPollingLobby = true;
            pollTimer = POLL_INTERVAL;
        }
    }

    public void StartGame()
    {
        if (!IsHost() || !currentLobby.HasValue)
            return;

        Log("HOST STARTING GAME");

        currentLobby.Value.SetJoinable(false);
        currentLobby.Value.SetData("in_game", "true");

        // Server is already running, just load the scene
        StartCoroutine(LoadGameScene());
    }

    #endregion

    #region Networking helpers

    /// <summary>
    /// FIX #1: Start the host server immediately when lobby is created
    /// This allows clients to connect as soon as they join the lobby
    /// </summary>
    private IEnumerator StartHostServerOnly()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing! Put a persistent NetworkManager in the Lobby scene.");
            yield break;
        }

        var facepunch = netManager.GetComponent<FacepunchTransport>();
        if (facepunch == null)
        {
            Debug.LogError("[SteamLobby] FacepunchTransport missing on NetworkManager!");
            yield break;
        }
        netManager.NetworkConfig.NetworkTransport = facepunch;

        if (netManager.IsListening)
        {
            Log("NetworkManager already listening");
            yield break;
        }

        Log("Starting Host server (lobby phase)...");
        bool started = false;
        try
        {
            started = netManager.StartHost();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobby] Exception starting host: {e.Message}");
        }

        if (!started)
        {
            Debug.LogError("[SteamLobby] Failed to start host!");
            yield break;
        }

        hasStartedNetwork = true;
        Log("Host server started - ready for client connections");

        // FIX #2: Subscribe to Netcode connection events to update UI
        netManager.OnClientConnectedCallback += OnNetcodeClientConnected;
        netManager.OnClientDisconnectCallback += OnNetcodeClientDisconnected;

        yield return null;
    }

    /// <summary>
    /// Load the game scene when host clicks "Start Game"
    /// Server is already running, so this just triggers scene load
    /// </summary>
    private IEnumerator LoadGameScene()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null || !netManager.IsListening)
        {
            Debug.LogError("[SteamLobby] NetworkManager not ready to load scene!");
            yield break;
        }

        Log("Loading game scene via Netcode...");
        netManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Start the Netcode client and configure FacepunchTransport for the current lobby owner.
    /// </summary>
    private void StartClientForLobby()
    {
        if (!currentLobby.HasValue)
            return;

        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("NetworkManager missing!");
            return;
        }

        if (netManager.IsListening)
        {
            hasStartedNetwork = true;
            return;
        }

        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport == null)
        {
            Debug.LogError("FacepunchTransport missing on NetworkManager!");
            return;
        }

        SteamId hostId = currentLobby.Value.Owner.Id;
        if (hostId == SteamClient.SteamId)
        {
            Log("Joined own lobby as host — skipping client start.");
            hasStartedNetwork = true;
            return;
        }

        transport.targetSteamId = hostId;
        netManager.NetworkConfig.NetworkTransport = transport;

        Log($"Starting Netcode client -> host {hostId}");
        if (!netManager.StartClient())
        {
            Debug.LogError("Failed to start client!");
            return;
        }

        hasStartedNetwork = true;
        Log("Client started successfully");
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Log($"Steam lobby member joined: {friend.Name}");
        RefreshLobbyMembers();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Log($"Steam lobby member left: {friend.Name}");
        RefreshLobbyMembers();
    }

    private void OnLobbyDataUpdate(Lobby lobby)
    {
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        string inGame = lobby.GetData("in_game");
        if (inGame == "true" && !IsHost() && !hasStartedNetwork)
        {
            Log("Lobby marked in-game but client not started — this shouldn't happen");
        }
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        JoinLobby(lobby.Id);
    }

    #endregion

    #region FIX #2: Netcode Connection Callbacks

    /// <summary>
    /// Called when a Netcode client connects (not just Steam lobby join)
    /// This updates the UI with actual connected players
    /// </summary>
    private void OnNetcodeClientConnected(ulong clientId)
    {
        if (!IsHost())
            return;

        Log($"Netcode client connected: {clientId}");

        // Refresh the player list to show all connected Netcode clients
        RefreshNetcodeConnectedPlayers();
    }

    /// <summary>
    /// Called when a Netcode client disconnects
    /// </summary>
    private void OnNetcodeClientDisconnected(ulong clientId)
    {
        if (!IsHost())
            return;

        Log($"Netcode client disconnected: {clientId}");

        // Refresh the player list
        RefreshNetcodeConnectedPlayers();
    }

    /// <summary>
    /// FIX #2: Get the actual list of Netcode-connected players
    /// This is more accurate than Steam lobby members for gameplay
    /// </summary>
    private void RefreshNetcodeConnectedPlayers()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null || !netManager.IsListening)
        {
            RefreshLobbyMembers(); // Fallback to Steam lobby members
            return;
        }

        List<string> names = new();

        // Add host (always connected)
        names.Add($"{SteamClient.Name} (Host)");

        // Add all connected clients via Netcode
        foreach (var clientId in netManager.ConnectedClientsIds)
        {
            if (clientId == netManager.LocalClientId)
                continue; // Skip self (host)

            // Try to get Steam ID from the connected client
            // Note: This requires the client's Steam ID, which we can get from lobby members
            if (currentLobby.HasValue)
            {
                foreach (var member in currentLobby.Value.Members)
                {
                    if (member.Id != SteamClient.SteamId) // Not the host
                    {
                        names.Add(member.Name);
                        break; // Simplified - in production you'd map clientId to SteamId properly
                    }
                }
            }
        }

        Log($"Netcode-connected players: {names.Count}");
        OnPlayerListChanged?.Invoke(names);
    }

    #endregion

    #region Helpers

    private void RefreshLobbyMembers()
    {
        if (!currentLobby.HasValue)
            return;

        List<string> names = new();
        foreach (var m in currentLobby.Value.Members)
        {
            string suffix = m.Id == currentLobby.Value.Owner.Id ? " (Host)" : "";
            names.Add(m.Name + suffix);
        }

        Log($"Steam lobby members: {names.Count}");
        OnPlayerListChanged?.Invoke(names);
    }

    public bool IsHost()
    {
        return isHost;
    }

    private void Log(string msg)
    {
        if (verboseLogging)
            Debug.Log("[SteamLobby] " + msg);
    }

    public void JoinLobbyByString(string lobbyIdString)
    {
        if (ulong.TryParse(lobbyIdString, out ulong lobbyId))
        {
            JoinLobby(lobbyId);
        }
        else
        {
            Debug.LogError($"[SteamLobby] Invalid lobby ID: {lobbyIdString}");
            OnConnectionFailed?.Invoke();
        }
    }

    public void OpenSteamInviteDialog()
    {
        if (!currentLobby.HasValue)
        {
            Debug.LogWarning("[SteamLobby] No lobby to invite to");
            return;
        }

        SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
    }

    public void LeaveLobby()
    {
        isPollingLobby = false;
        hasStartedNetwork = false;
        isHost = false;

        if (currentLobby.HasValue)
        {
            currentLobby.Value.Leave();
            currentLobby = null;
        }

        var netManager = NetworkManager.Singleton;
        if (netManager != null && netManager.IsListening)
        {
            // Unsubscribe from events
            netManager.OnClientConnectedCallback -= OnNetcodeClientConnected;
            netManager.OnClientDisconnectCallback -= OnNetcodeClientDisconnected;

            netManager.Shutdown();
        }
    }

    public string GetLobbyId()
    {
        return currentLobby?.Id.ToString() ?? string.Empty;
    }

    private void OnDestroy()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager != null)
        {
            netManager.OnClientConnectedCallback -= OnNetcodeClientConnected;
            netManager.OnClientDisconnectCallback -= OnNetcodeClientDisconnected;
        }
    }

    #endregion
}