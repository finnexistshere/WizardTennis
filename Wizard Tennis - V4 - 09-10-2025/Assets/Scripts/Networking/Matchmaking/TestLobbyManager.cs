using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public const string KEY_PLAYER_NAME = "PlayerName";
    public const string KEY_PLAYER_CHARACTER = "Character";
    public const string KEY_GAME_MODE = "GameMode";
    public const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";

    private Lobby joinedLobby;
    private string playerName;

    private float heartbeatTimer;
    private float lobbyPollTimer;

    public event Action<List<string>> OnPlayerListChanged;
    public event Action OnJoinedLobby;

    [Header("Netcode")]
    public string gameSceneName = "GameScene"; // Your actual game scene name

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
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPolling();
    }

    #region Authentication

    public async Task AuthenticateAsync(string playerName)
    {
        this.playerName = playerName;

        InitializationOptions options = new InitializationOptions();
        options.SetProfile(playerName);

        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in! PlayerId: " + AuthenticationService.Instance.PlayerId);
        }
    }

    private Player GetPlayer()
    {
        return new Player(AuthenticationService.Instance.PlayerId, null,
            new Dictionary<string, PlayerDataObject>
            {
                { KEY_PLAYER_NAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { KEY_PLAYER_CHARACTER, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "Marine") }
            });
    }

    #endregion

    #region Lobby Management

    private string GenerateLobbyCode(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < length; i++)
            code += chars[UnityEngine.Random.Range(0, chars.Length)];
        return code;
    }

    public async void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate, string gameMode)
    {
        try
        {
            Player player = GetPlayer();

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Player = player,
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_GAME_MODE, new DataObject(DataObject.VisibilityOptions.Public, gameMode) },
                    { KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, "0") }
                }
            };

            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log($"Lobby created! Name: {joinedLobby.Name}, ID: {joinedLobby.LobbyCode}");

            UpdatePlayerList();
            OnJoinedLobby?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to create lobby: " + e);
        }
    }

    public async void JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            Debug.Log($"Attempting to join lobby with code: {lobbyCode}");
            Player player = GetPlayer();

            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, new JoinLobbyByCodeOptions
            {
                Player = player
            });

            Debug.Log($"Successfully joined lobby: {joinedLobby.Name}, ID: {joinedLobby.Id}");
            Debug.Log($"Players in lobby: {joinedLobby.Players.Count}");

            await UpdatePlayerList();
            OnJoinedLobby?.Invoke();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Failed to join lobby with code '{lobbyCode}': {e.Message}");
        }
    }

    public string GetJoinedLobbyCode()
    {
        return joinedLobby?.LobbyCode;
    }

    public string GetJoinedLobbyId()
    {
        return joinedLobby?.Id;
    }

    public bool IsHost()
    {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private async Task UpdatePlayerList()
    {
        if (joinedLobby == null) return;

        try
        {
            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

            List<string> playerNames = new List<string>();
            foreach (var player in joinedLobby.Players)
            {
                string name = player.Data.ContainsKey(KEY_PLAYER_NAME) ? player.Data[KEY_PLAYER_NAME].Value : "Unknown";
                playerNames.Add(name);
            }

            OnPlayerListChanged?.Invoke(playerNames);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to update player list: " + e);
        }
    }

    #endregion

    #region Heartbeat & Polling

    private async void HandleLobbyHeartbeat()
    {
        if (IsHost() && joinedLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = 15f;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError("Heartbeat failed: " + e);
                }
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        if (joinedLobby == null) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = 1.5f;
            await UpdatePlayerList();

            // Check if host has started the game (for clients only)
            if (!IsHost() && joinedLobby.Data.ContainsKey(KEY_RELAY_JOIN_CODE))
            {
                string status = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;

                // If host has started the game
                if (status == "STARTING")
                {
                    Debug.Log("Host started game, auto-joining...");
                    JoinGame();
                }
            }
        }
    }

    #endregion

    #region Start Game with Netcode

    public async void StartGame()
    {
        if (!IsHost())
        {
            Debug.LogWarning("Only host can start the game!");
            return;
        }

        Debug.Log("Host starting game...");

        try
        {
            // Mark that we're starting the game by setting a flag in lobby data
            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, "STARTING") }
                }
            });

            Debug.Log("Host marked game as starting, loading scene...");

            // Load the game scene (which has the NetworkManager)
            SceneManager.LoadScene(gameSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start game: " + e);
        }
    }

    public async void JoinGame()
    {
        if (IsHost())
        {
            Debug.LogWarning("Host should use StartGame instead!");
            return;
        }

        Debug.Log("Client joining game...");

        try
        {
            // Get latest lobby data
            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

            if (joinedLobby.Data.ContainsKey(KEY_RELAY_JOIN_CODE))
            {
                string status = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;

                if (status == "STARTING") // Host has started the game
                {
                    Debug.Log("Host started game, loading scene...");

                    // Load the game scene (which has the NetworkManager)
                    SceneManager.LoadScene(gameSceneName);
                }
                else
                {
                    Debug.Log("Waiting for host to start...");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to join game: " + e);
        }
    }

    #endregion

    #region Cleanup

    public async void LeaveLobby()
    {
        if (joinedLobby == null) return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            Debug.Log("Left lobby");
            joinedLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to leave lobby: " + e);
        }
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
    }

    #endregion
}