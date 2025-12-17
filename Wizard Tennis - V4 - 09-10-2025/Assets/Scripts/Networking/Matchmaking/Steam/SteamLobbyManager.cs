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
    public event Action<List<LobbyMember>> OnPlayerListChangedWithData; // NEW: includes Steam IDs
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
            Log("Detected game start — stopping polling, Netcode will handle scene sync");
            isPollingLobby = false;
            // Don't manually load scene - Netcode will sync it automatically
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
        StartHostServerForLobby();
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

        // Start client networking immediately so it is ready before the host loads the scene
        StartClientForLobby();

        // Start polling as a fallback in case host sets in_game later
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

        // Start host if not already listening, then load scene via Netcode.
        StartCoroutine(StartHostIfNeeded());
    }

    #endregion

    #region Networking helpers

    /// <summary>
    /// FIX #1: Start host server in lobby so clients can connect immediately
    /// This prevents the client connection timeout issue
    /// </summary>
    private void StartHostServerForLobby()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing! Put a persistent NetworkManager in the Lobby scene.");
            return;
        }

        var facepunch = netManager.GetComponent<FacepunchTransport>();
        if (facepunch == null)
        {
            Debug.LogError("[SteamLobby] FacepunchTransport missing on NetworkManager!");
            return;
        }
        netManager.NetworkConfig.NetworkTransport = facepunch;

        if (netManager.IsListening)
        {
            Log("NetworkManager already listening");
            SubscribeToNetcodeEvents(); // FIX #2
            return;
        }

        Log("Starting Host server for lobby...");
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
            Debug.LogError("[SteamLobby] Failed to start host in lobby!");
            return;
        }

        hasStartedNetwork = true;
        Log("Host server started - clients can now connect");

        // FIX #2: Subscribe to connection events to update player list
        SubscribeToNetcodeEvents();
    }

    private IEnumerator StartHostIfNeeded()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing! Put a persistent NetworkManager in the Lobby scene.");
            yield break;
        }

        // Configure transport
        var facepunch = netManager.GetComponent<FacepunchTransport>();
        if (facepunch == null)
        {
            Debug.LogError("[SteamLobby] FacepunchTransport missing on NetworkManager!");
            yield break;
        }
        netManager.NetworkConfig.NetworkTransport = facepunch;

        // If we're already listening (started in lobby), don't restart — just load scene.
        if (!netManager.IsListening)
        {
            Log("Starting Host...");
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

            // give NLAPI/transport a frame to initialize
            yield return null;

            // FIX #2: Subscribe if we started here instead of in lobby
            SubscribeToNetcodeEvents();
        }
        else
        {
            Log("NetworkManager already listening (host or client). Proceeding to scene load.");
        }

        hasStartedNetwork = true;

        Log("Host started — loading scene via Netcode");
        netManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Start the Netcode client and configure FacepunchTransport for the current lobby owner.
    /// This should run as soon as the player joins the lobby so the client is connected before host loads the scene.
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

        // Already listening? don't restart
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

        // Set target to the host's SteamID
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

        // Subscribe to scene events so client knows when scene sync happens
        netManager.SceneManager.OnSceneEvent += OnSceneEvent;
        Log("Client started - ready for scene synchronization");
    }

    // Kept for compatibility if you still want a general purpose coroutine elsewhere.
    // Not used by StartGame/JoinLobby flow anymore.
    private IEnumerator SetupNetworkingCoroutine()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("NetworkManager missing!");
            yield break;
        }

        // Do not shutdown an already-listening manager here - we avoid restarting to prevent scene event races.
        yield return null;

        if (IsHost())
            yield return StartCoroutine(StartHostIfNeeded());
        else
            StartClientForLobby();
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        RefreshLobbyMembers();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        RefreshLobbyMembers();
    }

    private void OnLobbyDataUpdate(Lobby lobby)
    {
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        string inGame = lobby.GetData("in_game");
        if (inGame == "true" && !IsHost() && !hasStartedNetwork)
        {
            Log("Lobby marked in-game — starting client to join host");
            StartCoroutine(SetupNetworkingCoroutine());
        }
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        JoinLobby(lobby.Id);
    }

    private IEnumerator StartAsClientCoroutine(NetworkManager netManager)
    {
        var transport = netManager.GetComponent<FacepunchTransport>();
        netManager.NetworkConfig.NetworkTransport = transport;

        transport.targetSteamId = currentLobby.Value.Owner.Id;

        Log("Starting Client...");
        if (!netManager.StartClient())
        {
            Debug.LogError("Failed to start client!");
            yield break;
        }

        hasStartedNetwork = true;

        // Wait until connected before allowing scene sync
        while (!netManager.IsClient || !netManager.IsConnectedClient)
            yield return null;

        Log("Client connected — waiting for host scene");
    }

    #endregion

    #region FIX #2: Netcode Connection Events

    /// <summary>
    /// Subscribe to Netcode connection events to update the UI when clients actually connect
    /// </summary>
    private void SubscribeToNetcodeEvents()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
            return;

        // Unsubscribe first to avoid duplicates
        netManager.OnClientConnectedCallback -= OnNetcodeClientConnected;
        netManager.OnClientDisconnectCallback -= OnNetcodeClientDisconnected;

        // Subscribe
        netManager.OnClientConnectedCallback += OnNetcodeClientConnected;
        netManager.OnClientDisconnectCallback += OnNetcodeClientDisconnected;

        Log("Subscribed to Netcode connection events");
    }

    private void OnNetcodeClientConnected(ulong clientId)
    {
        if (!IsHost())
            return;

        Log($"Netcode client connected: {clientId}");
        RefreshLobbyMembers(); // Refresh UI when client connects
    }

    private void OnNetcodeClientDisconnected(ulong clientId)
    {
        if (!IsHost())
            return;

        Log($"Netcode client disconnected: {clientId}");
        RefreshLobbyMembers(); // Refresh UI when client disconnects
    }

    #endregion

    #region Scene Synchronization

    /// <summary>
    /// Handle Netcode scene events to properly manage scene transitions
    /// This prevents the "half-loaded" scene issue
    /// </summary>
    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Only care about client-side scene events
        if (IsHost())
            return;

        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.LoadEventCompleted:
                Log($"Client: Scene load completed - {sceneEvent.SceneName}");
                break;

            case SceneEventType.Load:
                Log($"Client: Loading scene - {sceneEvent.SceneName}");
                // Stop polling when scene load starts
                isPollingLobby = false;
                break;

            case SceneEventType.Unload:
                Log($"Client: Unloading scene - {sceneEvent.SceneName}");
                break;

            case SceneEventType.Synchronize:
                Log("Client: Synchronizing with host scene");
                break;
        }
    }

    #endregion

    #region Helpers

    public struct LobbyMember
    {
        public string name;
        public ulong steamId;
        public bool isHost;
    }

    private void RefreshLobbyMembers()
    {
        if (!currentLobby.HasValue)
            return;

        List<string> names = new();
        List<LobbyMember> members = new();

        foreach (var m in currentLobby.Value.Members)
        {
            names.Add(m.Name);
            members.Add(new LobbyMember
            {
                name = m.Name,
                steamId = m.Id,
                isHost = m.Id == currentLobby.Value.Owner.Id
            });
        }

        OnPlayerListChanged?.Invoke(names);
        OnPlayerListChangedWithData?.Invoke(members); // NEW: send detailed data
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
            netManager.OnClientConnectedCallback -= OnNetcodeClientConnected;
            netManager.OnClientDisconnectCallback -= OnNetcodeClientDisconnected;
            netManager.SceneManager.OnSceneEvent -= OnSceneEvent;
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
            netManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    #endregion
}