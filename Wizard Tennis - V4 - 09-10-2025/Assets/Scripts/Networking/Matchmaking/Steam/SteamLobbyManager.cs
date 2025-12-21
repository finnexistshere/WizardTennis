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

    public struct LobbyMember
    {
        public SteamId steamId;
        public string name;
        public bool isHost;
    }

    public event Action<List<LobbyMember>> OnPlayerListChangedWithData;

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
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void Update()
    {
        if (!isPollingLobby || IsHost() || !currentLobby.HasValue || hasStartedNetwork)
            return;

        pollTimer -= Time.deltaTime;
        if (pollTimer > 0f)
            return;

        pollTimer = POLL_INTERVAL;

        // ?? Force Steam to pull latest lobby data
        currentLobby.Value.Refresh();

        string ready = currentLobby.Value.GetData("netcode_ready");

        Log("Polling lobby: netcode_ready = " + ready);

        if (ready == "true")
        {
            Log("Detected host netcode readiness — starting client");
            isPollingLobby = false;
            StartClientForLobby();
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

        // Hosts typically should start as host only when starting the game.
        // If you want the host to start networking immediately upon creation, call StartHostIfNeeded() here.
    }

    private void StartHostImmediately()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing!");
            return;
        }

        if (netManager.IsListening)
            return;

        var transport = netManager.GetComponent<FacepunchTransport>();
        netManager.NetworkConfig.NetworkTransport = transport;

        Log("Starting Netcode Host (Lobby Phase)");
        if (!netManager.StartHost())
        {
            Debug.LogError("[SteamLobby] Failed to start host!");
            return;
        }

        hasStartedNetwork = true;
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

        Log("Joined lobby, waiting for host netcode signal...");

        // Start polling as a fallback in case host sets in_game later
        isPollingLobby = true;
        pollTimer = 0f;
    }

    public void StartGame()
    {
        if (!IsHost() || !currentLobby.HasValue)
            return;

        Log("HOST STARTING GAME");

        StartCoroutine(HostStartSequence());
    }

    private IEnumerator HostStartSequence()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager missing!");
            yield break;
        }

        // If host is already running, do NOT restart it
        if (!netManager.IsListening)
        {
            var transport = netManager.GetComponent<FacepunchTransport>();
            if (transport == null)
            {
                Debug.LogError("[SteamLobby] FacepunchTransport missing!");
                yield break;
            }

            netManager.NetworkConfig.NetworkTransport = transport;

            Log("Starting host netcode...");
            if (!netManager.StartHost())
            {
                Debug.LogError("[SteamLobby] Failed to start host!");
                yield break;
            }

            hasStartedNetwork = true;
        }
        else
        {
            Log("Host already listening — reusing existing host");
        }

        // Give Facepunch + Steam time to settle
        yield return null;
        yield return null;

        // SIGNAL CLIENTS (nonce-based, unmissable)
        currentLobby.Value.SetData(
            "netcode_ready",
            DateTime.UtcNow.Ticks.ToString()
        );

        Log("Netcode ready signal sent");

        // Safety delay for slow clients
        yield return new WaitForSeconds(0.5f);

        netManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
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

        // If we're already listening (maybe started earlier for testing), don't restart — just load scene.
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
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        Log($"Member joined: {friend.Name}");
        RefreshLobbyMembers();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        if (!currentLobby.HasValue || lobby.Id != currentLobby.Value.Id)
            return;

        Log($"Member left: {friend.Name}");
        RefreshLobbyMembers();
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

    #region Helpers

    private void RefreshLobbyMembers()
    {
        if (!currentLobby.HasValue)
            return;

        List<string> names = new();
        List<LobbyMember> members = new();

        SteamId hostId = currentLobby.Value.Owner.Id;

        foreach (var m in currentLobby.Value.Members)
        {
            names.Add(m.Name);

            members.Add(new LobbyMember
            {
                steamId = m.Id,
                name = m.Name,
                isHost = (m.Id == hostId)
            });
        }

        // Legacy support
        OnPlayerListChanged?.Invoke(names);

        // New rich data (avatars, host badge, etc)
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