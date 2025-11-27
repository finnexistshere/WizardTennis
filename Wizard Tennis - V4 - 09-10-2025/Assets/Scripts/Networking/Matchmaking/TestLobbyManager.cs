using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public const string KEY_PLAYER_NAME = "PlayerName";
    public const string KEY_PLAYER_CHARACTER = "Character";
    public const string KEY_GAME_MODE = "GameMode";
    public const string KEY_LOBBY_CODE = "LobbyCode";

    private Lobby joinedLobby;
    private string playerName;

    private float heartbeatTimer;
    private float lobbyPollTimer;

    private List<string> currentPlayerIds = new List<string>();

    // Dictionary mapping lobby codes to lobby IDs (for quick local lookup)
    private static Dictionary<string, string> lobbyCodeMap = new Dictionary<string, string>();

    private void Awake() => Instance = this;

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

        await UnityServices.InitializeAsync();

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

    #region Lobby Code System

    private string GenerateLobbyCode(int length = 6)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var code = "";
        for (int i = 0; i < length; i++)
            code += chars[UnityEngine.Random.Range(0, chars.Length)];
        return code;
    }

    #endregion

    #region Lobby Management

    public async void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate, string gameMode)
    {
        Player player = GetPlayer();

        // Generate a unique join code
        string joinCode;
        do
        {
            joinCode = GenerateLobbyCode();
        } while (lobbyCodeMap.ContainsKey(joinCode));

        CreateLobbyOptions options = new CreateLobbyOptions
        {
            Player = player,
            IsPrivate = isPrivate,
            Data = new Dictionary<string, DataObject>
            {
                { KEY_GAME_MODE, new DataObject(DataObject.VisibilityOptions.Public, gameMode) },
                { KEY_LOBBY_CODE, new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
            }
        };

        joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

        // Store mapping
        lobbyCodeMap[joinCode] = joinedLobby.Id;

        Debug.Log($"Lobby created! Name: {joinedLobby.Name}, Code: {joinCode}");
    }

    public async void JoinLobbyByCode(string code)
    {
        if (!lobbyCodeMap.ContainsKey(code))
        {
            Debug.LogWarning("No lobby exists with code: " + code);
            return;
        }

        string lobbyId = lobbyCodeMap[code];
        Player player = GetPlayer();

        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, new JoinLobbyByIdOptions
            {
                Player = player
            });

            Debug.Log($"Joined lobby: {joinedLobby.Name} with code: {code}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to join lobby: " + e);
        }
    }

    public string GetJoinedLobbyCode()
    {
        if (joinedLobby != null && joinedLobby.Data.ContainsKey(KEY_LOBBY_CODE))
            return joinedLobby.Data[KEY_LOBBY_CODE].Value;
        return null;
    }

    public async Task<List<string>> GetPlayerNamesAsync()
    {
        if (joinedLobby == null) return new List<string>();

        joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

        List<string> playerNames = new List<string>();
        foreach (var player in joinedLobby.Players)
        {
            playerNames.Add(player.Data.ContainsKey(KEY_PLAYER_NAME)
                ? player.Data[KEY_PLAYER_NAME].Value
                : "Unknown");
        }

        return playerNames;
    }

    #endregion

    #region Heartbeat & Polling

    private async void HandleLobbyHeartbeat()
    {
        if (joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = 15f;
                await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        if (joinedLobby == null) return;

        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer <= 0f)
        {
            lobbyPollTimer = 1f;
            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
        }
    }

    #endregion
}
