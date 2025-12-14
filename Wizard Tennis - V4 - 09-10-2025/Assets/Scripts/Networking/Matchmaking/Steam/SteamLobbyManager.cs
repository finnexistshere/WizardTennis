using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Steamworks;
using Steamworks.Data;
using Netcode.Transports.Facepunch;

/// <summary>
/// Steam Lobby System using Facepunch.Steamworks
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public int maxPlayers = 4;

    [Header("Steam Settings")]
    public uint steamAppId = 480; // Not used directly, set in FacepunchSteamManager instead

    // Events
    public event Action<List<string>> OnPlayerListChanged;
    public event Action OnJoinedLobby;
    public event Action OnConnectionFailed;
    public event Action<string> OnLobbyCodeGenerated;

    // Steam lobby data
    private Lobby? currentLobby;
    private bool isHost = false;
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

        // Check if Steam is initialized (FacepunchSteamManager should do this)
        if (!FacepunchSteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized! Make sure FacepunchSteamManager is in scene.");
            return;
        }

        Debug.Log("[SteamLobby] Steam initialized successfully (Facepunch)");
        Debug.Log($"[SteamLobby] Steam ID: {SteamClient.SteamId}");
        Debug.Log($"[SteamLobby] Username: {SteamClient.Name}");

        // Subscribe to lobby events
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
        SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataUpdate;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
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
        Debug.Log($"[SteamLobby] Scene loaded: {scene.name}");

        if (scene.name == gameSceneName && isWaitingForGameStart)
        {
            isWaitingForGameStart = false;
            Invoke(nameof(SetupAndStartNetworking), 0.5f);
        }
    }

    #region Public API

    public void Initialize(string customName = "")
    {
        // Facepunch sets name automatically from Steam
        Debug.Log($"[SteamLobby] Player name: {SteamClient.Name}");
    }

    public async void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate = false)
    {
        this.maxPlayers = maxPlayers;
        isHost = true;

        Debug.Log($"[SteamLobby] Creating lobby: {lobbyName} (Max: {maxPlayers}, Private: {isPrivate})");

        try
        {
            var createLobbyTask = SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            var lobby = await createLobbyTask;

            if (!lobby.HasValue)
            {
                Debug.LogError("[SteamLobby] Failed to create lobby");
                OnConnectionFailed?.Invoke();
                return;
            }

            currentLobby = lobby.Value;

            // Set lobby visibility
            if (isPrivate)
            {
                currentLobby.Value.SetPrivate();
            }
            else
            {
                currentLobby.Value.SetFriendsOnly();
            }

            currentLobby.Value.SetData("name", lobbyName);
            currentLobby.Value.SetData("game_mode", "versus");
            currentLobby.Value.SetData("in_game", "false");
            currentLobby.Value.SetData("host_id", SteamClient.SteamId.ToString());

            Debug.Log($"[SteamLobby] ? Lobby created! ID: {currentLobby.Value.Id}");

            UpdateLobbyMembers();
            OnLobbyCodeGenerated?.Invoke(currentLobby.Value.Id.ToString());
            OnJoinedLobby?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobby] Failed to create lobby: {e}");
            OnConnectionFailed?.Invoke();
        }
    }

    public async void JoinLobby(SteamId lobbyId)
    {
        isHost = false;
        Debug.Log($"[SteamLobby] Joining lobby: {lobbyId}");

        try
        {
            var lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);

            if (!lobby.HasValue)
            {
                Debug.LogError("[SteamLobby] Failed to join lobby");
                OnConnectionFailed?.Invoke();
                return;
            }

            currentLobby = lobby.Value;
            Debug.Log($"[SteamLobby] ? Joined lobby: {currentLobby.Value.Id}");

            UpdateLobbyMembers();
            OnJoinedLobby?.Invoke();

            // Check if game already started
            string inGame = currentLobby.Value.GetData("in_game");
            if (inGame == "true")
            {
                Debug.Log("[SteamLobby] Game already in progress, loading scene...");
                isWaitingForGameStart = true;
                hasStartedNetwork = false;
                SceneManager.LoadScene(gameSceneName);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobby] Failed to join lobby: {e}");
            OnConnectionFailed?.Invoke();
        }
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

    public void FindLobbies()
    {
        Debug.Log("[SteamLobby] Searching for lobbies...");
        // Facepunch lobby search is different, implement if needed
    }

    public string GetLobbyId()
    {
        return currentLobby?.Id.ToString() ?? "";
    }

    public SteamId GetCurrentLobbyId()
    {
        return currentLobby?.Id ?? 0;
    }

    public void OpenSteamInviteDialog()
    {
        if (currentLobby.HasValue)
        {
            SteamFriends.OpenGameInviteOverlay(currentLobby.Value.Id);
            Debug.Log("[SteamLobby] Opened Steam invite dialog");
        }
        else
        {
            Debug.LogWarning("[SteamLobby] No active lobby to invite to");
        }
    }

    public bool IsHost()
    {
        if (!currentLobby.HasValue) return false;
        return isHost && currentLobby.Value.Owner.Id == SteamClient.SteamId;
    }

    public List<string> GetPlayerNames()
    {
        List<string> names = new List<string>();

        if (!currentLobby.HasValue) return names;

        foreach (var member in currentLobby.Value.Members)
        {
            names.Add(member.Name);
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

        Debug.Log("[SteamLobby] ?? Host starting game...");

        currentLobby.Value.SetData("in_game", "true");
        currentLobby.Value.SetData("host_id", SteamClient.SteamId.ToString());

        Debug.Log("[SteamLobby] Marked game as starting, loading scene...");

        isWaitingForGameStart = true;
        hasStartedNetwork = false;

        SceneManager.LoadScene(gameSceneName);
    }

    public void LeaveLobby()
    {
        isWaitingForGameStart = false;
        hasStartedNetwork = false;

        if (currentLobby.HasValue)
        {
            Debug.Log("[SteamLobby] Leaving lobby");
            currentLobby.Value.Leave();
            currentLobby = null;
        }

        var netManager = NetworkManager.Singleton;
        if (netManager != null && netManager.IsListening)
        {
            Debug.Log("[SteamLobby] Shutting down NetworkManager");
            netManager.Shutdown();
        }

        isHost = false;
    }

    #endregion

    #region Steam Callbacks (Facepunch)

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        Debug.Log($"[SteamLobby] OnLobbyCreated callback: {result}");
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Debug.Log($"[SteamLobby] OnLobbyEntered: {lobby.Id}");
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Debug.Log($"[SteamLobby] {friend.Name} joined the lobby");
        UpdateLobbyMembers();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Debug.Log($"[SteamLobby] {friend.Name} left the lobby");
        UpdateLobbyMembers();
    }

    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId steamId)
    {
        Debug.Log($"[SteamLobby] Lobby game created");
    }

    private void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Debug.Log($"[SteamLobby] Lobby invite from {friend.Name}");
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        Debug.Log($"[SteamLobby] Join requested from Steam overlay: {lobby.Id}");
        JoinLobby(lobby.Id);
    }

    private void OnLobbyDataUpdate(Lobby lobby)
    {
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        Debug.Log("[SteamLobby] ?? Lobby data updated!");

        // Check if game has started (for clients)
        if (!IsHost())
        {
            string inGame = lobby.GetData("in_game");
            Debug.Log($"[SteamLobby] Checking in_game status: '{inGame}'");

            if (inGame == "true")
            {
                Debug.Log("[SteamLobby] ?? Host started game - loading scene...");
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
        Debug.Log("[SteamLobby] ============================================");
        Debug.Log("[SteamLobby] === SETUP AND START NETWORKING ===");
        Debug.Log("[SteamLobby] ============================================");

        if (hasStartedNetwork)
        {
            Debug.LogWarning("[SteamLobby] Network already started!");
            return;
        }

        // Find NetworkManager in the newly loaded scene
        var netManager = NetworkManager.Singleton;

        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] ? NetworkManager not found in game scene!");
            Debug.LogError("[SteamLobby] Make sure your game scene has a NetworkManager GameObject");
            return;
        }

        Debug.Log($"[SteamLobby] ? NetworkManager found: {netManager.gameObject.name}");

        // Check if NetworkManager is already running
        if (netManager.IsListening)
        {
            Debug.LogWarning("[SteamLobby] ?? NetworkManager is already running! Shutting down first...");
            netManager.Shutdown();

            // Wait a frame before restarting
            Invoke(nameof(SetupAndStartNetworking), 0.2f);
            return;
        }

        Debug.Log($"[SteamLobby] Current lobby valid: {currentLobby.HasValue}");
        if (currentLobby.HasValue)
        {
            Debug.Log($"[SteamLobby] Lobby ID: {currentLobby.Value.Id}");
            Debug.Log($"[SteamLobby] Lobby Owner: {currentLobby.Value.Owner.Name} ({currentLobby.Value.Owner.Id})");
            Debug.Log($"[SteamLobby] My Steam ID: {SteamClient.SteamId}");
            Debug.Log($"[SteamLobby] Am I Host?: {IsHost()}");
        }

        if (IsHost())
        {
            StartAsHost(netManager);
        }
        else
        {
            StartAsClient(netManager);
        }
    }

    private void StartAsHost(NetworkManager netManager)
    {
        Debug.Log("[SteamLobby] ============================================");
        Debug.Log("[SteamLobby] ===       STARTING AS HOST            ===");
        Debug.Log("[SteamLobby] ============================================");

        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport == null)
        {
            Debug.LogError("[SteamLobby] ? FacepunchTransport not found on NetworkManager!");
            Debug.LogError("[SteamLobby] Add FacepunchTransport component to NetworkManager GameObject");
            return;
        }

        Debug.Log($"[SteamLobby] ? FacepunchTransport found: {transport}");

        // Check if transport is assigned
        if (netManager.NetworkConfig.NetworkTransport != transport)
        {
            Debug.LogWarning("[SteamLobby] ?? FacepunchTransport not set as NetworkTransport, setting it now...");
            netManager.NetworkConfig.NetworkTransport = transport;
        }

        Debug.Log("[SteamLobby] ?? Calling NetworkManager.StartHost()...");

        bool success = netManager.StartHost();

        if (success)
        {
            hasStartedNetwork = true;
            Debug.Log("[SteamLobby] ============================================");
            Debug.Log("[SteamLobby] ??? HOST STARTED SUCCESSFULLY ???");
            Debug.Log("[SteamLobby] ============================================");
            Debug.Log($"[SteamLobby] NetworkManager.IsHost: {netManager.IsHost}");
            Debug.Log($"[SteamLobby] NetworkManager.IsServer: {netManager.IsServer}");
            Debug.Log($"[SteamLobby] LocalClientId: {netManager.LocalClientId}");
        }
        else
        {
            Debug.LogError("[SteamLobby] ============================================");
            Debug.LogError("[SteamLobby] ??? FAILED TO START HOST ???");
            Debug.LogError("[SteamLobby] ============================================");
            Debug.LogError("[SteamLobby] Check these:");
            Debug.LogError("[SteamLobby]   1. FacepunchTransport attached to NetworkManager?");
            Debug.LogError("[SteamLobby]   2. NetworkTransport set correctly in NetworkManager?");
            Debug.LogError("[SteamLobby]   3. Steam initialized properly?");
            Debug.LogError("[SteamLobby]   4. Check console for other errors");
        }
    }

    private void StartAsClient(NetworkManager netManager)
    {
        Debug.Log("[SteamLobby] ============================================");
        Debug.Log("[SteamLobby] ===       STARTING AS CLIENT          ===");
        Debug.Log("[SteamLobby] ============================================");

        if (!currentLobby.HasValue)
        {
            Debug.LogError("[SteamLobby] ? No lobby to connect to!");
            return;
        }

        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport == null)
        {
            Debug.LogError("[SteamLobby] ? FacepunchTransport not found on NetworkManager!");
            Debug.LogError("[SteamLobby] Add FacepunchTransport component to NetworkManager GameObject");
            return;
        }

        Debug.Log($"[SteamLobby] ? FacepunchTransport found: {transport}");

        // Check if transport is assigned
        if (netManager.NetworkConfig.NetworkTransport != transport)
        {
            Debug.LogWarning("[SteamLobby] ?? FacepunchTransport not set as NetworkTransport, setting it now...");
            netManager.NetworkConfig.NetworkTransport = transport;
        }

        // Get host Steam ID
        SteamId hostId = currentLobby.Value.Owner.Id;
        string hostName = currentLobby.Value.Owner.Name;

        Debug.Log($"[SteamLobby] ?? Target Host: {hostName}");
        Debug.Log($"[SteamLobby] ?? Host Steam ID: {hostId}");
        Debug.Log($"[SteamLobby] ?? My Steam ID: {SteamClient.SteamId}");

        // Configure transport
        transport.targetSteamId = hostId;
        Debug.Log($"[SteamLobby] ? Set FacepunchTransport.targetSteamId = {hostId}");

        Debug.Log("[SteamLobby] ?? Calling NetworkManager.StartClient()...");

        bool success = netManager.StartClient();

        if (success)
        {
            hasStartedNetwork = true;
            Debug.Log("[SteamLobby] ============================================");
            Debug.Log("[SteamLobby] ??? CLIENT STARTED SUCCESSFULLY ???");
            Debug.Log("[SteamLobby] ============================================");
            Debug.Log($"[SteamLobby] NetworkManager.IsClient: {netManager.IsClient}");
            Debug.Log($"[SteamLobby] Attempting connection to: {hostName} ({hostId})");
            Debug.Log("[SteamLobby] ? Waiting for connection approval from host...");
        }
        else
        {
            Debug.LogError("[SteamLobby] ============================================");
            Debug.LogError("[SteamLobby] ??? FAILED TO START CLIENT ???");
            Debug.LogError("[SteamLobby] ============================================");
            Debug.LogError("[SteamLobby] Check these:");
            Debug.LogError("[SteamLobby]   1. FacepunchTransport attached to NetworkManager?");
            Debug.LogError("[SteamLobby]   2. Host is actually running?");
            Debug.LogError("[SteamLobby]   3. Both using same Steam App ID?");
            Debug.LogError("[SteamLobby]   4. Check console for transport errors");
        }
    }

    #endregion

    #region Helper Methods

    private void UpdateLobbyMembers()
    {
        if (!currentLobby.HasValue) return;

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

        // Unsubscribe from events
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
        SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreated;
        SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
        SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataUpdate;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
        // Don't shutdown Steam here - FacepunchSteamManager handles it
    }

    #endregion
}