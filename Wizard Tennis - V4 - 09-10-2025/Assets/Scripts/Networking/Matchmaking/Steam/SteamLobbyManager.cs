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

/// <summary>
/// Production-ready Steam Lobby System using Facepunch.Steamworks
/// Handles lobby creation, joining, and seamless game start synchronization
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public int maxPlayers = 4;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

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

    // Polling for lobby changes
    private bool isPollingLobby = false;
    private float pollTimer = 0f;
    private const float POLL_INTERVAL = 0.5f;

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

        if (!FacepunchSteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam not initialized! Make sure FacepunchSteamManager is in scene.");
            return;
        }

        Log($"Steam initialized - User: {SteamClient.Name} ({SteamClient.SteamId})");

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

    private void Update()
    {
        // Poll for lobby updates (clients only)
        if (isPollingLobby && currentLobby.HasValue && !IsHost())
        {
            pollTimer -= Time.deltaTime;

            if (pollTimer <= 0f)
            {
                pollTimer = POLL_INTERVAL;
                CheckForGameStart();
            }
        }
        else if (isPollingLobby && verboseLogging)
        {
            // Debug why polling stopped
            if (!currentLobby.HasValue)
                Debug.LogWarning("[SteamLobby] ?? Polling but no lobby!");
            if (IsHost())
                Debug.LogWarning("[SteamLobby] ?? Polling but we are host!");
        }
    }

    private void CheckForGameStart()
    {
        if (!currentLobby.HasValue)
        {
            Log("?? CheckForGameStart: No lobby!");
            return;
        }

        // Refresh lobby data first
        currentLobby.Value.Refresh();

        string inGame = currentLobby.Value.GetData("in_game");

        if (verboseLogging)
        {
            Log($"?? Polling check - in_game: '{inGame}'");
        }

        if (inGame == "true")
        {
            Log("??? DETECTED: Host started game! ???");
            isPollingLobby = false;
            isWaitingForGameStart = true;
            hasStartedNetwork = false;

            Log($"?? Loading scene: {gameSceneName}");
            SceneManager.LoadScene(gameSceneName);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Log($"Scene loaded: {scene.name}, waiting for game: {isWaitingForGameStart}");

        if (scene.name == gameSceneName && isWaitingForGameStart)
        {
            isWaitingForGameStart = false;

            // Delay to ensure scene is fully loaded
            StartCoroutine(SetupNetworkingCoroutine());
        }
    }

    #region Public API

    public void Initialize(string customName = "")
    {
        Log($"Initialized - Player: {SteamClient.Name}");
    }

    public async void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate = false)
    {
        this.maxPlayers = maxPlayers;
        isHost = true;

        Log($"Creating lobby: '{lobbyName}' (Max: {maxPlayers}, Private: {isPrivate})");

        try
        {
            var lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);

            if (!lobby.HasValue)
            {
                Debug.LogError("[SteamLobby] Failed to create lobby");
                OnConnectionFailed?.Invoke();
                return;
            }

            currentLobby = lobby.Value;

            // Set lobby visibility
            if (isPrivate)
                currentLobby.Value.SetPrivate();
            else
                currentLobby.Value.SetFriendsOnly();

            // Set lobby metadata
            currentLobby.Value.SetData("name", lobbyName);
            currentLobby.Value.SetData("game_mode", "versus");
            currentLobby.Value.SetData("in_game", "false");
            currentLobby.Value.SetData("host_id", SteamClient.SteamId.ToString());
            currentLobby.Value.SetJoinable(true);

            Log($"? Lobby created! ID: {currentLobby.Value.Id}");

            RefreshLobbyMembers();
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
        Log($"Joining lobby: {lobbyId}");

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
            Log($"? Joined lobby: {currentLobby.Value.Id}");

            // Refresh to get latest member list
            currentLobby.Value.Refresh();
            RefreshLobbyMembers();
            OnJoinedLobby?.Invoke();

            // Check if game already started
            string inGame = currentLobby.Value.GetData("in_game");
            Log($"?? Initial in_game check: '{inGame}'");

            if (inGame == "true")
            {
                Log("? Game already in progress, joining immediately!");
                isPollingLobby = false;
                isWaitingForGameStart = true;
                hasStartedNetwork = false;
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                // Start polling for game start
                isPollingLobby = true;
                pollTimer = POLL_INTERVAL;
                Log("============================================");
                Log("?????? STARTED POLLING FOR GAME START ??????");
                Log("============================================");
                Log($"Will check every {POLL_INTERVAL} seconds");
                Log($"Current in_game value: '{inGame}'");
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
            Log("Opened Steam invite overlay");
        }
        else
        {
            Debug.LogWarning("[SteamLobby] No active lobby to invite to");
        }
    }

    public bool IsHost()
    {
        // For local testing with same Steam account, use the flag we set
        // For real Steam testing with different accounts, verify with lobby owner
        if (!currentLobby.HasValue) return false;

        // Use the isHost flag we set when creating/joining
        // This works correctly even with same Steam account
        return isHost;
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

        if (!currentLobby.HasValue)
        {
            Debug.LogError("[SteamLobby] No lobby to start game in!");
            return;
        }

        Log("============================================");
        Log("?????? HOST STARTING GAME ??????");
        Log("============================================");

        // Make lobby unjoinable
        currentLobby.Value.SetJoinable(false);
        Log("? Set lobby unjoinable");

        // Set game as in progress
        currentLobby.Value.SetData("in_game", "true");
        currentLobby.Value.SetData("host_id", SteamClient.SteamId.ToString());

        Log("? Set in_game = 'true'");
        Log("? Set host_id = " + SteamClient.SteamId);

        // Verify it was set
        string verification = currentLobby.Value.GetData("in_game");
        Log($"?? Verification check - in_game is now: '{verification}'");

        Log("?? Clients should detect this via polling or callbacks");

        // Stop polling since we're loading
        isPollingLobby = false;
        isWaitingForGameStart = true;
        hasStartedNetwork = false;

        Log($"?? Host loading scene: {gameSceneName}");

        // Load game scene
        SceneManager.LoadScene(gameSceneName);
    }

    public void LeaveLobby()
    {
        isWaitingForGameStart = false;
        hasStartedNetwork = false;
        isPollingLobby = false;

        if (currentLobby.HasValue)
        {
            Log("Leaving lobby");
            currentLobby.Value.Leave();
            currentLobby = null;
        }

        var netManager = NetworkManager.Singleton;
        if (netManager != null && netManager.IsListening)
        {
            Log("Shutting down NetworkManager");
            netManager.Shutdown();
        }

        isHost = false;
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        Log($"OnLobbyCreated callback: {result}");
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Log($"OnLobbyEntered: {lobby.Id}");

        // Refresh member list
        if (currentLobby.HasValue)
        {
            currentLobby.Value.Refresh();
            RefreshLobbyMembers();
        }
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Log($"?? {friend.Name} joined the lobby");

        // Force refresh to ensure we have latest data
        if (currentLobby.HasValue && lobby.Id == currentLobby.Value.Id)
        {
            currentLobby.Value.Refresh();
            RefreshLobbyMembers();
        }
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Log($"?? {friend.Name} left the lobby");

        if (currentLobby.HasValue && lobby.Id == currentLobby.Value.Id)
        {
            currentLobby.Value.Refresh();
            RefreshLobbyMembers();
        }
    }

    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId steamId)
    {
        Log($"Lobby game created");
    }

    private void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Log($"Lobby invite from {friend.Name}");
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        Log($"Join requested from Steam overlay: {lobby.Id}");
        JoinLobby(lobby.Id);
    }

    private void OnLobbyDataUpdate(Lobby lobby)
    {
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        Log("?? Lobby data changed");

        // Refresh member list on any data change
        currentLobby.Value.Refresh();
        RefreshLobbyMembers();

        // Check if game started (clients only)
        if (!IsHost())
        {
            string inGame = lobby.GetData("in_game");

            if (inGame == "true")
            {
                Log("? Host started game via callback - loading scene");
                isPollingLobby = false;
                isWaitingForGameStart = true;
                hasStartedNetwork = false;
                SceneManager.LoadScene(gameSceneName);
            }
        }
    }

    #endregion

    #region Networking Setup

    private IEnumerator SetupNetworkingCoroutine()
    {
        Log("=== SETUP AND START NETWORKING ===");

        if (hasStartedNetwork)
        {
            Debug.LogWarning("[SteamLobby] Network already started!");
            yield break;
        }

        // Wait for scene to fully load
        yield return new WaitForSeconds(0.5f);

        var netManager = NetworkManager.Singleton;

        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] ? NetworkManager not found!");
            yield break;
        }

        Log($"? NetworkManager found: {netManager.gameObject.name}");

        // Check if already running
        if (netManager.IsListening)
        {
            Debug.LogWarning("[SteamLobby] NetworkManager already running, shutting down first");
            netManager.Shutdown();
            yield return new WaitForSeconds(0.2f);
        }

        if (!currentLobby.HasValue)
        {
            Debug.LogError("[SteamLobby] ? No lobby!");
            yield break;
        }

        Log($"Lobby Owner: {currentLobby.Value.Owner.Name} ({currentLobby.Value.Owner.Id})");
        Log($"My Steam ID: {SteamClient.SteamId}");
        Log($"Am I Host?: {IsHost()}");

        if (IsHost())
        {
            yield return StartCoroutine(StartAsHostCoroutine(netManager));
        }
        else
        {
            yield return StartCoroutine(StartAsClientCoroutine(netManager));
        }
    }

    private IEnumerator StartAsHostCoroutine(NetworkManager netManager)
    {
        Log("=== STARTING AS HOST ===");

        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport == null)
        {
            Debug.LogError("[SteamLobby] ? FacepunchTransport not found!");
            yield break;
        }

        if (netManager.NetworkConfig.NetworkTransport != transport)
        {
            netManager.NetworkConfig.NetworkTransport = transport;
        }

        Log("?? Calling NetworkManager.StartHost()");

        bool success = netManager.StartHost();

        if (success)
        {
            hasStartedNetwork = true;
            Log("============================================");
            Log("??? HOST STARTED SUCCESSFULLY ???");
            Log("============================================");
            Log($"IsHost: {netManager.IsHost}, IsServer: {netManager.IsServer}");
            Log($"LocalClientId: {netManager.LocalClientId}");
        }
        else
        {
            Debug.LogError("[SteamLobby] ??? FAILED TO START HOST ???");
        }

        yield break;
    }

    private IEnumerator StartAsClientCoroutine(NetworkManager netManager)
    {
        Log("=== STARTING AS CLIENT ===");

        if (!currentLobby.HasValue)
        {
            Debug.LogError("[SteamLobby] ? No lobby!");
            yield break;
        }

        // Check if we're using UnityTransport (LocalTestHelper active)
        var unityTransport = netManager.GetComponent<UnityTransport>();
        var facepunchTransport = netManager.GetComponent<FacepunchTransport>();

        bool usingLocalTest = (netManager.NetworkConfig.NetworkTransport == unityTransport);

        if (usingLocalTest)
        {
            Log("?? LOCAL TEST MODE - Using Unity Transport");
            Log("? Unity Transport configured for localhost (127.0.0.1:7777)");
        }
        else
        {
            // Use FacepunchTransport for real Steam P2P
            if (facepunchTransport == null)
            {
                Debug.LogError("[SteamLobby] ? FacepunchTransport not found!");
                yield break;
            }

            if (netManager.NetworkConfig.NetworkTransport != facepunchTransport)
            {
                netManager.NetworkConfig.NetworkTransport = facepunchTransport;
            }

            // Get host ID
            SteamId hostId = currentLobby.Value.Owner.Id;
            string hostName = currentLobby.Value.Owner.Name;

            Log($"?? Target Host: {hostName} ({hostId})");
            Log($"?? My Steam ID: {SteamClient.SteamId}");

            // CRITICAL: Check if trying to connect to self
            if (hostId == SteamClient.SteamId)
            {
                Debug.LogError("[SteamLobby] ? Cannot connect to ourselves!");
                Debug.LogError("[SteamLobby] You need TWO DIFFERENT Steam accounts for testing!");
                Debug.LogError("[SteamLobby] OR enable LocalTestHelper for same-account testing");
                yield break;
            }

            // Configure transport
            facepunchTransport.targetSteamId = hostId;
            Log($"? Set targetSteamId = {hostId}");
        }

        // Small delay for networking to be ready
        yield return new WaitForSeconds(0.5f);

        Log("?? Calling NetworkManager.StartClient()");

        bool success = false;
        try
        {
            success = netManager.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SteamLobby] Exception: {e.Message}");

            if (e.Message.Contains("Invalid Connection"))
            {
                Debug.LogError("[SteamLobby] ? STEAM P2P CONNECTION FAILED");
                Debug.LogError("[SteamLobby] Common causes:");
                Debug.LogError("[SteamLobby]   - Using same Steam account on both instances");
                Debug.LogError("[SteamLobby]   - Firewall blocking Steam P2P");
                Debug.LogError("[SteamLobby]   - Different App IDs");
                Debug.LogError("[SteamLobby] TIP: Enable LocalTestHelper.useLocalTestingMode for same-account testing");
            }
        }

        if (success)
        {
            hasStartedNetwork = true;
            Log("============================================");
            Log("??? CLIENT STARTED SUCCESSFULLY ???");
            Log("============================================");
            Log($"IsClient: {netManager.IsClient}");

            if (usingLocalTest)
            {
                Log("Connecting to: 127.0.0.1:7777 (localhost)");
            }
            else
            {
                Log($"Connecting to: {currentLobby.Value.Owner.Name} via Steam P2P");
            }

            Log("? Waiting for connection approval...");
        }
        else
        {
            Debug.LogError("[SteamLobby] ??? FAILED TO START CLIENT ???");
        }

        yield break;
    }

    #endregion

    #region Helper Methods

    private void RefreshLobbyMembers()
    {
        if (!currentLobby.HasValue) return;

        List<string> names = GetPlayerNames();

        Log($"?? Lobby members ({names.Count}):");
        foreach (string name in names)
        {
            Log($"  - {name}");
        }

        OnPlayerListChanged?.Invoke(names);
    }

    private void Log(string message)
    {
        if (verboseLogging)
            Debug.Log($"[SteamLobby] {message}");
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        LeaveLobby();

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
    }

    #endregion
}