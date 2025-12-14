using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Steamworks;

/// <summary>
/// FREE Steam Lobby System - Uses Steam P2P networking
/// FIXED: Robust version with proper error handling
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public int maxPlayers = 4;

    [Header("Steam Settings")]
    [Tooltip("Your Steam App ID (use 480 for testing)")]
    public uint steamAppId = 480;

    // Events
    public event Action<List<string>> OnPlayerListChanged;
    public event Action OnJoinedLobby;
    public event Action OnConnectionFailed;
    public event Action<string> OnLobbyCodeGenerated;

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

    // Game start tracking
    private bool isWaitingForGameStart = false;
    private bool hasStartedNetwork = false;

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

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized!");
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SteamLobby] Scene loaded: {scene.name}, waiting for game start: {isWaitingForGameStart}");

        // When game scene loads, check if we need to start networking
        if (scene.name == gameSceneName && isWaitingForGameStart)
        {
            isWaitingForGameStart = false;

            // Small delay to ensure scene is fully loaded
            Invoke(nameof(SetupAndStartNetworking), 0.5f);
        }
    }

    #region Public API

    public void Initialize(string customName = "")
    {
        if (!string.IsNullOrEmpty(customName))
            playerName = customName;
        else
            playerName = SteamFriends.GetPersonaName();

        Debug.Log($"[SteamLobby] Player name: {playerName}");
    }

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

    public void FindLobbies()
    {
        if (!SteamManager.Initialized) return;

        Debug.Log("[SteamLobby] Searching for lobbies...");
        SteamMatchmaking.RequestLobbyList();
    }

    public string GetLobbyId()
    {
        return currentLobbyId.m_SteamID.ToString();
    }

    public CSteamID GetCurrentLobbyId()
    {
        return currentLobbyId;
    }

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

    public bool IsHost()
    {
        return isHost && currentLobbyId.IsValid() &&
               SteamMatchmaking.GetLobbyOwner(currentLobbyId) == SteamUser.GetSteamID();
    }

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

    public void StartGame()
    {
        if (!IsHost())
        {
            Debug.LogWarning("[SteamLobby] Only host can start the game!");
            return;
        }

        Debug.Log("[SteamLobby] Host starting game...");

        // Set lobby data to signal game start
        SteamMatchmaking.SetLobbyData(currentLobbyId, "in_game", "true");

        // Store host's Steam ID for clients to connect to
        SteamMatchmaking.SetLobbyData(currentLobbyId, "host_id", SteamUser.GetSteamID().m_SteamID.ToString());

        Debug.Log("[SteamLobby] Marked game as starting, loading scene...");

        // Flag that we're starting the game
        isWaitingForGameStart = true;
        hasStartedNetwork = false;

        // Load the game scene
        SceneManager.LoadScene(gameSceneName);
    }

    public void LeaveLobby()
    {
        isWaitingForGameStart = false;
        hasStartedNetwork = false;

        if (currentLobbyId.IsValid())
        {
            Debug.Log("[SteamLobby] Leaving lobby");
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            currentLobbyId = CSteamID.Nil;
        }

        if (netManager != null && netManager.IsListening)
        {
            Debug.Log("[SteamLobby] Shutting down NetworkManager");
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
        Debug.Log($"[SteamLobby] ? Share this ID: {currentLobbyId.m_SteamID}");

        // Set lobby data
        SteamMatchmaking.SetLobbyData(currentLobbyId, "name", "Game Lobby");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "game_mode", "versus");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "in_game", "false");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "host_id", SteamUser.GetSteamID().m_SteamID.ToString());

        UpdateLobbyMembers();
        OnLobbyCodeGenerated?.Invoke(currentLobbyId.m_SteamID.ToString());
        OnJoinedLobby?.Invoke();
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        if (callback.m_EChatRoomEnterResponse != 1)
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
            Debug.Log("[SteamLobby] Game already in progress, loading scene...");
            isWaitingForGameStart = true;
            hasStartedNetwork = false;
            SceneManager.LoadScene(gameSceneName);
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
        }

        UpdateLobbyMembers();
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log($"[SteamLobby] Join requested from Steam overlay: {callback.m_steamIDLobby}");
        JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != currentLobbyId.m_SteamID)
            return;

        Debug.Log("[SteamLobby] Lobby data updated!");

        // Check if game has started (for clients)
        if (!IsHost())
        {
            string inGame = SteamMatchmaking.GetLobbyData(currentLobbyId, "in_game");
            Debug.Log($"[SteamLobby] in_game status: '{inGame}'");

            if (inGame == "true")
            {
                Debug.Log("[SteamLobby] Host started game - loading scene...");
                isWaitingForGameStart = true;
                hasStartedNetwork = false;
                SceneManager.LoadScene(gameSceneName);
            }
        }
    }

    #endregion

    #region Networking Setup

    private void SetupAndStartNetworking()
    {
        if (hasStartedNetwork)
        {
            Debug.LogWarning("[SteamLobby] Network already started!");
            return;
        }

        // Find NetworkManager in the newly loaded scene
        netManager = NetworkManager.Singleton;

        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager not found in game scene!");
            Debug.LogError("[SteamLobby] Make sure your game scene has a NetworkManager GameObject");
            return;
        }

        // Check if NetworkManager is already running
        if (netManager.IsListening)
        {
            Debug.LogWarning("[SteamLobby] NetworkManager is already running! Shutting down first...");
            netManager.Shutdown();

            // Wait a frame before restarting
            Invoke(nameof(SetupAndStartNetworking), 0.1f);
            return;
        }

        Debug.Log($"[SteamLobby] Setting up networking - IsHost: {IsHost()}");
        Debug.Log($"[SteamLobby] Current lobby: {currentLobbyId}");
        Debug.Log($"[SteamLobby] My Steam ID: {SteamUser.GetSteamID()}");

        if (IsHost())
        {
            StartAsHost();
        }
        else
        {
            StartAsClient();
        }
    }

    private void StartAsHost()
    {
        Debug.Log("[SteamLobby] === STARTING AS HOST ===");

        // Verify transport is configured
        if (!VerifyTransport())
        {
            Debug.LogError("[SteamLobby] Transport verification failed!");
            return;
        }

        Debug.Log("[SteamLobby] Starting NetworkManager.StartHost()...");

        bool success = netManager.StartHost();

        if (success)
        {
            hasStartedNetwork = true;
            Debug.Log("[SteamLobby] ? Successfully started as Host");
            Debug.Log($"[SteamLobby] NetworkManager.IsHost: {netManager.IsHost}");
            Debug.Log($"[SteamLobby] NetworkManager.IsServer: {netManager.IsServer}");
        }
        else
        {
            Debug.LogError("[SteamLobby] ? Failed to start as Host!");
            Debug.LogError("[SteamLobby] Check these common issues:");
            Debug.LogError("[SteamLobby]   1. Is SimpleSteamP2PTransport attached to NetworkManager?");
            Debug.LogError("[SteamLobby]   2. Is the transport set as the NetworkTransport in NetworkManager?");
            Debug.LogError("[SteamLobby]   3. Are there multiple NetworkManagers in the scene?");
            Debug.LogError("[SteamLobby]   4. Check the Unity console for transport errors");
        }
    }

    private void StartAsClient()
    {
        Debug.Log("[SteamLobby] === STARTING AS CLIENT ===");

        // Get the host's Steam ID
        CSteamID hostId = GetHostSteamId();
        Debug.Log($"[SteamLobby] Host Steam ID: {hostId}");

        if (!hostId.IsValid())
        {
            Debug.LogError("[SteamLobby] Invalid host Steam ID!");
            return;
        }

        // Verify and configure transport
        if (!VerifyTransport())
        {
            Debug.LogError("[SteamLobby] Transport verification failed!");
            return;
        }

        // Set the target host Steam ID in transport
        SetTransportTargetHost(hostId);

        Debug.Log("[SteamLobby] Starting NetworkManager.StartClient()...");

        bool success = netManager.StartClient();

        if (success)
        {
            hasStartedNetwork = true;
            Debug.Log("[SteamLobby] ? Successfully started as Client");
            Debug.Log($"[SteamLobby] NetworkManager.IsClient: {netManager.IsClient}");
            Debug.Log($"[SteamLobby] Connecting to host: {hostId}");
        }
        else
        {
            Debug.LogError("[SteamLobby] ? Failed to start as Client!");
            Debug.LogError("[SteamLobby] Check these common issues:");
            Debug.LogError("[SteamLobby]   1. Is SimpleSteamP2PTransport attached to NetworkManager?");
            Debug.LogError("[SteamLobby]   2. Did the transport targetSteamId get set correctly?");
            Debug.LogError("[SteamLobby]   3. Is the host actually running and listening?");
            Debug.LogError("[SteamLobby]   4. Check the Unity console for transport errors");
        }
    }

    private bool VerifyTransport()
    {
        var transport = netManager.GetComponent<SimpleSteamP2PTransport>();

        if (transport == null)
        {
            Debug.LogError("[SteamLobby] SimpleSteamP2PTransport not found on NetworkManager!");
            Debug.LogError("[SteamLobby] Add SimpleSteamP2PTransport component to your NetworkManager GameObject");
            return false;
        }

        if (netManager.NetworkConfig.NetworkTransport != transport)
        {
            Debug.LogError("[SteamLobby] SimpleSteamP2PTransport exists but is not set as the NetworkTransport!");
            Debug.LogError("[SteamLobby] In NetworkManager inspector, set 'Network Transport' to SimpleSteamP2PTransport");
            return false;
        }

        Debug.Log("[SteamLobby] ? Transport verified: SimpleSteamP2PTransport");
        return true;
    }

    private CSteamID GetHostSteamId()
    {
        string hostIdStr = SteamMatchmaking.GetLobbyData(currentLobbyId, "host_id");

        Debug.Log($"[SteamLobby] Host ID from lobby data: '{hostIdStr}'");

        if (string.IsNullOrEmpty(hostIdStr))
        {
            // Fallback to lobby owner
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
            Debug.Log($"[SteamLobby] Using lobby owner as host: {owner}");
            return owner;
        }

        if (ulong.TryParse(hostIdStr, out ulong hostId))
        {
            return new CSteamID(hostId);
        }

        Debug.LogWarning($"[SteamLobby] Failed to parse host ID: '{hostIdStr}', using lobby owner");
        return SteamMatchmaking.GetLobbyOwner(currentLobbyId);
    }

    private void SetTransportTargetHost(CSteamID hostId)
    {
        var transport = netManager.GetComponent<SimpleSteamP2PTransport>();
        if (transport != null)
        {
            transport.targetSteamId = hostId.m_SteamID;
            Debug.Log($"[SteamLobby] ? Set SimpleSteamP2PTransport target: {hostId.m_SteamID}");
            return;
        }

        Debug.LogError("[SteamLobby] No SimpleSteamP2PTransport found on NetworkManager!");
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
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
    }

    #endregion
}