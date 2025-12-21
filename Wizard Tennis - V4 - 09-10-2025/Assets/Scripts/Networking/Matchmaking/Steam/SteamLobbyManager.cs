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
    public event Action<List<LobbyMember>> OnPlayerListChangedWithData;
    public event Action OnJoinedLobby;
    public event Action OnConnectionFailed;
    public event Action<string> OnLobbyCodeGenerated;

    public Lobby? currentLobby;
    private bool isHost = false;
    private bool hasStartedNetwork = false;

    private bool isPollingLobby = false;
    private float pollTimer = 0f;
    private const float POLL_INTERVAL = 0.5f;

    public struct LobbyMember
    {
        public string name;
        public ulong steamId;
        public bool isHost;
    }

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

        Log("Lobby created - waiting for Start Game");
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

        Log("Successfully joined lobby");

        // Start polling to detect when host starts game
        string inGame = currentLobby.Value.GetData("in_game");
        if (inGame != "true")
        {
            isPollingLobby = true;
            pollTimer = POLL_INTERVAL;
            Log("Waiting for host to start game");
        }
    }

    public void StartGame()
    {
        if (!IsHost() || !currentLobby.HasValue)
            return;

        Log("HOST STARTING GAME");

        currentLobby.Value.SetJoinable(false);
        currentLobby.Value.SetData("in_game", "true");

        // Start host and load scene via Netcode
        StartCoroutine(StartHostIfNeeded());
    }

    #endregion

    #region Networking helpers

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

        // CRITICAL: Disable auto player spawning - game scene will handle it
        netManager.NetworkConfig.PlayerPrefab = null;

        // If already listening, just load scene
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

            // Give transport a frame to initialize
            yield return null;
        }
        else
        {
            Log("NetworkManager already listening. Proceeding to scene load.");
        }

        hasStartedNetwork = true;

        Log("Host started — loading scene via Netcode");
        netManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    private void StartClientForLobby()
    {
        if (!currentLobby.HasValue)
            return;

        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing!");
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
            Debug.LogError("[SteamLobby] FacepunchTransport missing on NetworkManager!");
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

        // CRITICAL: Disable auto player spawning
        netManager.NetworkConfig.PlayerPrefab = null;

        Log($"Starting Netcode client -> host {hostId}");
        if (!netManager.StartClient())
        {
            Debug.LogError("[SteamLobby] Failed to start client!");
            return;
        }

        hasStartedNetwork = true;
        Log("Client started - will auto-sync to host's scene");
    }

    private IEnumerator SetupNetworkingCoroutine()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing!");
            yield break;
        }

        // Don't restart if already listening
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
        Log($"Member joined: {friend.Name}");
        RefreshLobbyMembers();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Log($"Member left: {friend.Name}");
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

    #endregion

    #region Helpers

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
        OnPlayerListChangedWithData?.Invoke(members);
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
            netManager.Shutdown();
        }
    }

    public string GetLobbyId()
    {
        return currentLobby?.Id.ToString() ?? string.Empty;
    }

    #endregion
}