using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using Steamworks;
// Add your Steam transport using directive here
// using Netcode.Transports.Facepunch; // Example

/// <summary>
/// FREE Steam Lobby System - Uses Steam P2P networking
/// FIXED: Now properly establishes P2P connections between players
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
        // When game scene loads, check if we need to start networking
        if (scene.name == gameSceneName && isWaitingForGameStart)
        {
            isWaitingForGameStart = false;
            SetupAndStartNetworking();
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

        // Load the game scene
        SceneManager.LoadScene(gameSceneName);
    }

    public void LeaveLobby()
    {
        isWaitingForGameStart = false;

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
                SceneManager.LoadScene(gameSceneName);
            }
        }
    }

    #endregion

    #region Networking Setup

    private void SetupAndStartNetworking()
    {
        netManager = NetworkManager.Singleton;

        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager not found in game scene!");
            return;
        }

        // CRITICAL: Setup Steam transport
        // Replace this with your actual Steam transport setup
        // Example for Facepunch.Steamworks transport:
        /*
        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport != null)
        {
            transport.targetSteamId = GetHostSteamId();
        }
        */

        // EXAMPLE: If using a custom Steam transport
        // Configure it here with the host's Steam ID

        Debug.Log($"[SteamLobby] Setting up networking - IsHost: {IsHost()}");

        if (IsHost())
        {
            Debug.Log("[SteamLobby] Starting as Host...");
            if (netManager.StartHost())
            {
                Debug.Log("[SteamLobby] ? Successfully started as Host");
            }
            else
            {
                Debug.LogError("[SteamLobby] ? Failed to start as Host!");
            }
        }
        else
        {
            // Get the host's Steam ID from lobby data
            CSteamID hostId = GetHostSteamId();
            Debug.Log($"[SteamLobby] Starting as Client, connecting to host: {hostId}");

            // CRITICAL: Set the target host Steam ID in your transport
            // This is transport-specific, adjust based on your Steam transport
            SetTransportTargetHost(hostId);

            if (netManager.StartClient())
            {
                Debug.Log("[SteamLobby] ? Successfully started as Client");
            }
            else
            {
                Debug.LogError("[SteamLobby] ? Failed to start as Client!");
            }
        }
    }

    private CSteamID GetHostSteamId()
    {
        string hostIdStr = SteamMatchmaking.GetLobbyData(currentLobbyId, "host_id");

        if (string.IsNullOrEmpty(hostIdStr))
        {
            // Fallback to lobby owner
            return SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        }

        if (ulong.TryParse(hostIdStr, out ulong hostId))
        {
            return new CSteamID(hostId);
        }

        return SteamMatchmaking.GetLobbyOwner(currentLobbyId);
    }

    private void SetTransportTargetHost(CSteamID hostId)
    {
        // For SimpleSteamP2PTransport
        var transport = netManager.GetComponent<SimpleSteamP2PTransport>();
        if (transport != null)
        {
            transport.targetSteamId = hostId.m_SteamID;
            Debug.Log($"[SteamLobby] Set SimpleSteamP2PTransport target: {hostId}");
            return;
        }

        // For Facepunch Transport (if you get it working):
        /*
        var facepunchTransport = netManager.GetComponent<FacepunchTransport>();
        if (facepunchTransport != null)
        {
            facepunchTransport.targetSteamId = hostId.m_SteamID;
            Debug.Log($"[SteamLobby] Set Facepunch transport target: {hostId}");
            return;
        }
        */

        Debug.LogError("[SteamLobby] No compatible Steam transport found on NetworkManager!");
        Debug.LogError("[SteamLobby] Add SimpleSteamP2PTransport component to NetworkManager");
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