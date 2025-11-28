using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Steamworks;

/// <summary>
/// FREE Steam Lobby System - Uses Steam P2P networking
/// Requires Steamworks.NET (free) and Steam App ID
/// No Unity services needed - Steam handles everything!
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public int maxPlayers = 4;

    [Header("Steam Settings")]
    [Tooltip("Your Steam App ID (use 480 for testing)")]
    public uint steamAppId = 480; // SpaceWar (test app)

    // Events
    public event Action<List<string>> OnPlayerListChanged;
    public event Action OnJoinedLobby;
    public event Action OnConnectionFailed;
    public event Action<string> OnLobbyCodeGenerated; // Returns lobby ID as string

    // Steam lobby data
    private CSteamID currentLobbyId;
    private bool isHost = false;
    private string playerName = "Player";
    private Dictionary<CSteamID, string> lobbyMembers = new Dictionary<CSteamID, string>();

    // Callbacks
    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<LobbyEnter_t> lobbyEnterCallback;
    private Callback<LobbyMatchList_t> lobbyListCallback;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
    private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

    // Netcode
    private NetworkManager netManager;

    // Polling for game start
    private bool isPollingLobby = false;
    private float lobbyPollTimer = 0f;
    private const float LOBBY_POLL_INTERVAL = 1.5f; // Check every 1.5 seconds

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize Steam
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized! Make sure SteamManager is in scene.");
            return;
        }

        Debug.Log("[SteamLobby] Steam initialized successfully");
        Debug.Log($"[SteamLobby] Steam ID: {SteamUser.GetSteamID()}");
        Debug.Log($"[SteamLobby] Username: {SteamFriends.GetPersonaName()}");

        playerName = SteamFriends.GetPersonaName();

        // Setup callbacks
        lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyList);
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
    }

    private void Start()
    {
        // Don't setup NetworkManager here - it may not exist yet
        // Wait until we actually need it (when starting/joining game)
    }

    private void SetupNetworkManager()
    {
        // Try to find NetworkManager when we actually need it
        netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogWarning("[SteamLobby] NetworkManager not found in scene - will be needed when starting game");
            return;
        }

        // Only subscribe once
        netManager.OnServerStarted -= OnServerStarted;
        netManager.OnClientConnectedCallback -= OnClientConnected;
        netManager.OnClientDisconnectCallback -= OnClientDisconnected;

        netManager.OnServerStarted += OnServerStarted;
        netManager.OnClientConnectedCallback += OnClientConnected;
        netManager.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log("[SteamLobby] NetworkManager configured");
    }

    #region Public API

    /// <summary>
    /// Initialize with custom player name (optional - defaults to Steam name)
    /// </summary>
    public void Initialize(string customName = "")
    {
        if (!string.IsNullOrEmpty(customName))
            playerName = customName;
        else
            playerName = SteamFriends.GetPersonaName();

        Debug.Log($"[SteamLobby] Player name: {playerName}");
    }

    /// <summary>
    /// Create a Steam lobby
    /// </summary>
    public void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate = false)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized!");
            OnConnectionFailed?.Invoke();
            return;
        }

        this.maxPlayers = maxPlayers;
        isHost = true;

        ELobbyType lobbyType = isPrivate ? ELobbyType.k_ELobbyTypePrivate : ELobbyType.k_ELobbyTypeFriendsOnly;

        Debug.Log($"[SteamLobby] Creating lobby: {lobbyName} (Max: {maxPlayers}, Private: {isPrivate})");

        SteamMatchmaking.CreateLobby(lobbyType, maxPlayers);
    }

    /// <summary>
    /// Join a lobby using Steam Lobby ID
    /// </summary>
    public void JoinLobby(CSteamID lobbyId)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized!");
            OnConnectionFailed?.Invoke();
            return;
        }

        isHost = false;
        Debug.Log($"[SteamLobby] Joining lobby: {lobbyId}");
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    /// <summary>
    /// Join lobby by string ID (from invite code)
    /// </summary>
    public void JoinLobbyByString(string lobbyIdString)
    {
        if (ulong.TryParse(lobbyIdString, out ulong lobbyId))
        {
            JoinLobby(new CSteamID(lobbyId));
        }
        else
        {
            Debug.LogError($"[SteamLobby] Invalid lobby ID: {lobbyIdString}");
            OnConnectionFailed?.Invoke();
        }
    }

    /// <summary>
    /// Search for available lobbies
    /// </summary>
    public void FindLobbies()
    {
        if (!SteamManager.Initialized) return;

        Debug.Log("[SteamLobby] Searching for lobbies...");

        // Add filters if needed
        // SteamMatchmaking.AddRequestLobbyListStringFilter("game_mode", "deathmatch", ELobbyComparison.k_ELobbyComparisonEqual);

        SteamMatchmaking.RequestLobbyList();
    }

    /// <summary>
    /// Get the current lobby ID as a shareable string
    /// </summary>
    public string GetLobbyId()
    {
        return currentLobbyId.m_SteamID.ToString();
    }

    /// <summary>
    /// Get lobby ID for Steam overlay invites
    /// </summary>
    public CSteamID GetCurrentLobbyId()
    {
        return currentLobbyId;
    }

    /// <summary>
    /// Open Steam invite dialog
    /// </summary>
    public void OpenSteamInviteDialog()
    {
        if (currentLobbyId.IsValid())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyId);
            Debug.Log("[SteamLobby] Opened Steam invite dialog");
        }
        else
        {
            Debug.LogWarning("[SteamLobby] No active lobby to invite to");
        }
    }

    /// <summary>
    /// Check if this player is the host
    /// </summary>
    public bool IsHost()
    {
        return isHost && currentLobbyId.IsValid() &&
               SteamMatchmaking.GetLobbyOwner(currentLobbyId) == SteamUser.GetSteamID();
    }

    /// <summary>
    /// Get list of player names in lobby
    /// </summary>
    public List<string> GetPlayerNames()
    {
        List<string> names = new List<string>();

        if (!currentLobbyId.IsValid()) return names;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, i);
            string name = SteamFriends.GetFriendPersonaName(memberId);
            names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// Start the game (host only) - loads game scene then starts network host
    /// </summary>
    public async void StartGame()
    {
        if (!IsHost())
        {
            Debug.LogWarning("[SteamLobby] Only host can start the game!");
            return;
        }

        Debug.Log("[SteamLobby] Host starting game...");

        try
        {
            // Set lobby to in-game state
            SteamMatchmaking.SetLobbyData(currentLobbyId, "in_game", "true");

            Debug.Log("[SteamLobby] Marked game as starting, loading scene...");

            // Small delay to ensure clients see the lobby update
            await System.Threading.Tasks.Task.Delay(500);

            // Load the game scene (which should have NetworkManager)
            SceneManager.LoadScene(gameSceneName);

            // After scene loads, NetworkManager will start automatically or via a GameSceneManager
            // If you need to start it manually, do it in the game scene's Start() method
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobby] Failed to start game: {e}");
        }
    }

    /// <summary>
    /// Leave the current lobby
    /// </summary>
    public void LeaveLobby()
    {
        isPollingLobby = false; // Stop polling

        if (currentLobbyId.IsValid())
        {
            Debug.Log("[SteamLobby] Leaving lobby");
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            currentLobbyId = CSteamID.Nil;
        }

        if (netManager != null && netManager.IsListening)
        {
            netManager.Shutdown();
        }

        lobbyMembers.Clear();
        isHost = false;
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobby] Failed to create lobby: {callback.m_eResult}");
            OnConnectionFailed?.Invoke();
            return;
        }

        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        Debug.Log($"[SteamLobby] ? Lobby created! ID: {currentLobbyId}");
        Debug.Log($"[SteamLobby] ? Share this ID with friends: {currentLobbyId.m_SteamID}");
        Debug.Log($"[SteamLobby] ? Or use Steam overlay to invite (Shift+Tab)");

        // Set lobby data
        SteamMatchmaking.SetLobbyData(currentLobbyId, "name", "Game Lobby");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "game_mode", "versus");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "in_game", "false");

        UpdateLobbyMembers();
        OnLobbyCodeGenerated?.Invoke(currentLobbyId.m_SteamID.ToString());
        OnJoinedLobby?.Invoke();
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        if (callback.m_EChatRoomEnterResponse != 1) // 1 = success
        {
            Debug.LogError($"[SteamLobby] Failed to join lobby: {callback.m_EChatRoomEnterResponse}");
            OnConnectionFailed?.Invoke();
            return;
        }

        Debug.Log($"[SteamLobby] ? Joined lobby: {currentLobbyId}");

        UpdateLobbyMembers();
        OnJoinedLobby?.Invoke();

        // Check if game already started
        string inGame = SteamMatchmaking.GetLobbyData(currentLobbyId, "in_game");
        if (inGame == "true" && !isHost)
        {
            Debug.Log("[SteamLobby] Game already in progress, joining...");
            ConnectAsClient();
        }
        else if (!isHost)
        {
            // Start polling for game start
            Debug.Log("[SteamLobby] Started polling for game start...");
            isPollingLobby = true;
            lobbyPollTimer = LOBBY_POLL_INTERVAL;
        }
    }

    private void OnLobbyList(LobbyMatchList_t callback)
    {
        Debug.Log($"[SteamLobby] Found {callback.m_nLobbiesMatching} lobbies");

        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            string lobbyName = SteamMatchmaking.GetLobbyData(lobbyId, "name");
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            int maxMembers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

            Debug.Log($"[SteamLobby] Lobby {i}: {lobbyName} ({numMembers}/{maxMembers}) - ID: {lobbyId}");
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != currentLobbyId.m_SteamID)
            return;

        CSteamID userId = new CSteamID(callback.m_ulSteamIDUserChanged);
        string userName = SteamFriends.GetFriendPersonaName(userId);

        EChatMemberStateChange stateChange = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

        switch (stateChange)
        {
            case EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                Debug.Log($"[SteamLobby] {userName} joined the lobby");
                break;
            case EChatMemberStateChange.k_EChatMemberStateChangeLeft:
                Debug.Log($"[SteamLobby] {userName} left the lobby");
                break;
            case EChatMemberStateChange.k_EChatMemberStateChangeDisconnected:
                Debug.Log($"[SteamLobby] {userName} disconnected");
                break;
            case EChatMemberStateChange.k_EChatMemberStateChangeKicked:
                Debug.Log($"[SteamLobby] {userName} was kicked");
                break;
            case EChatMemberStateChange.k_EChatMemberStateChangeBanned:
                Debug.Log($"[SteamLobby] {userName} was banned");
                break;
        }

        UpdateLobbyMembers();
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        // Player clicked "Join Game" from Steam friend list or invite
        Debug.Log($"[SteamLobby] Join requested from Steam overlay: {callback.m_steamIDLobby}");
        JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        // This is called whenever lobby data changes
        if (callback.m_ulSteamIDLobby != currentLobbyId.m_SteamID)
            return;

        Debug.Log("[SteamLobby] Lobby data updated!");

        // Check if game has started
        if (!IsHost())
        {
            string inGame = SteamMatchmaking.GetLobbyData(currentLobbyId, "in_game");
            Debug.Log($"[SteamLobby] Received lobby data update - in_game: '{inGame}'");

            if (inGame == "true")
            {
                Debug.Log("[SteamLobby] Host started game - loading scene...");
                isPollingLobby = false; // Stop polling
                ConnectAsClient();
            }
        }
    }

    #endregion

    #region Netcode Integration

    private void OnServerStarted()
    {
        Debug.Log("[SteamLobby] Netcode server started");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SteamLobby] Client {clientId} connected to Netcode");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[SteamLobby] Client {clientId} disconnected from Netcode");
    }

    private async void ConnectAsClient()
    {
        Debug.Log("[SteamLobby] Game already in progress, joining...");

        try
        {
            // Small delay before loading to avoid race condition
            await System.Threading.Tasks.Task.Delay(300);

            // Load the game scene (which has the NetworkManager)
            SceneManager.LoadScene(gameSceneName);

            // After scene loads, NetworkManager will start client automatically
            // or via a GameSceneManager
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobby] Failed to join game: {e}");
        }
    }

    #endregion

    #region Helper Methods

    private void UpdateLobbyMembers()
    {
        if (!currentLobbyId.IsValid()) return;

        lobbyMembers.Clear();
        List<string> names = GetPlayerNames();

        Debug.Log($"[SteamLobby] Lobby has {names.Count} players:");
        foreach (string name in names)
        {
            Debug.Log($"  - {name}");
        }

        OnPlayerListChanged?.Invoke(names);
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        LeaveLobby();

        if (netManager != null)
        {
            netManager.OnServerStarted -= OnServerStarted;
            netManager.OnClientConnectedCallback -= OnClientConnected;
            netManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
    }

    #endregion
}