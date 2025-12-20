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

    private bool isPollingForGameStart = false;
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
        // Client polls lobby to detect when host starts game
        if (!isPollingForGameStart || !currentLobby.HasValue || IsHost())
            return;

        pollTimer -= Time.deltaTime;
        if (pollTimer > 0f)
            return;

        pollTimer = POLL_INTERVAL;
        currentLobby.Value.Refresh();

        string inGame = currentLobby.Value.GetData("in_game");
        if (inGame == "true")
        {
            Log("Host started game - connecting as client");
            isPollingForGameStart = false;
            StartClientAndLoadGame();
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

        // Check if game already started
        string inGame = currentLobby.Value.GetData("in_game");
        if (inGame == "true")
        {
            Log("Game already in progress - connecting");
            StartClientAndLoadGame();
        }
        else
        {
            // Start polling for game start
            isPollingForGameStart = true;
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

        // Start host and load game scene
        StartCoroutine(StartHostAndLoadGame());
    }

    #endregion

    #region Networking Helpers

    private IEnumerator StartHostAndLoadGame()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing!");
            yield break;
        }

        var facepunch = netManager.GetComponent<FacepunchTransport>();
        if (facepunch == null)
        {
            Debug.LogError("[SteamLobby] FacepunchTransport missing!");
            yield break;
        }

        // Configure transport
        netManager.NetworkConfig.NetworkTransport = facepunch;
        netManager.NetworkConfig.PlayerPrefab = null; // Let game scene spawner handle players

        Log("Starting as Host...");

        if (!netManager.StartHost())
        {
            Debug.LogError("[SteamLobby] Failed to start host!");
            yield break;
        }

        hasStartedNetwork = true;
        Log("Host started successfully");

        // Give network a moment to initialize
        yield return new WaitForSeconds(0.1f);

        // Load game scene
        Log($"Loading game scene: {gameSceneName}");
        var status = netManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);

        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[SteamLobby] Failed to load scene! Status: {status}");
        }
    }

    private void StartClientAndLoadGame()
    {
        if (!currentLobby.HasValue)
        {
            Debug.LogError("[SteamLobby] No lobby to connect to!");
            return;
        }

        StartCoroutine(StartClientAndLoadGameCoroutine());
    }

    private IEnumerator StartClientAndLoadGameCoroutine()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing!");
            yield break;
        }

        var transport = netManager.GetComponent<FacepunchTransport>();
        if (transport == null)
        {
            Debug.LogError("[SteamLobby] FacepunchTransport missing!");
            yield break;
        }

        // Get host Steam ID
        SteamId hostId = currentLobby.Value.Owner.Id;
        if (hostId == SteamClient.SteamId)
        {
            Debug.LogError("[SteamLobby] Cannot connect to own lobby as client!");
            yield break;
        }

        // Configure transport
        transport.targetSteamId = hostId;
        netManager.NetworkConfig.NetworkTransport = transport;
        netManager.NetworkConfig.PlayerPrefab = null; // Let game scene spawner handle players

        Log($"Starting as Client, connecting to host {hostId}...");

        if (!netManager.StartClient())
        {
            Debug.LogError("[SteamLobby] Failed to start client!");
            yield break;
        }

        hasStartedNetwork = true;
        Log("Client started - waiting for connection and scene sync");

        // Netcode will automatically sync to host's scene (GameScene)
        // No need to manually load anything
    }

    #endregion

    #region Steam Callbacks

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Log($"Player joined: {friend.Name}");
        RefreshLobbyMembers();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        Log($"Player left: {friend.Name}");
        RefreshLobbyMembers();
    }

    private void OnLobbyDataUpdate(Lobby lobby)
    {
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        // Lobby data changed - client's polling will handle game start
        Log("Lobby data updated");
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        JoinLobby(lobby.Id);
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
        isPollingForGameStart = false;
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