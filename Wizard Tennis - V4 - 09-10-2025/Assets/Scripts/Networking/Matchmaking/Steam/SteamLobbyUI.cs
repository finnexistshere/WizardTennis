using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI Controller for Steam Lobby - works identically to Unity Services LobbyUI
/// </summary>
public class SteamLobbyUI : MonoBehaviour
{
    [Header("Main Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;

    [Header("Create Lobby")]
    public TMP_InputField lobbyNameInput;
    public TMP_InputField maxPlayersInput;
    public Toggle privateToggle;
    public Button createLobbyButton;

    [Header("Join Lobby")]
    public TMP_InputField lobbyCodeInput;
    public Button joinLobbyButton;
    public Button findLobbiesButton;

    [Header("Lobby Panel")]
    public TextMeshProUGUI lobbyCodeText;
    public TextMeshProUGUI lobbyNameText;
    public TextMeshProUGUI playerListText;
    public Button startGameButton;
    public Button inviteFriendsButton;
    public Button leaveLobbyButton;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    private void Start()
    {
        // Initialize Steam Lobby Manager
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.Initialize();
        }
        else
        {
            Debug.LogError("[SteamLobbyUI] SteamLobbyManager not found in scene!");
            SetStatus("ERROR: SteamLobbyManager missing!");
            return;
        }

        // Setup button listeners
        createLobbyButton?.onClick.AddListener(OnCreateLobby);
        joinLobbyButton?.onClick.AddListener(OnJoinLobby);
        findLobbiesButton?.onClick.AddListener(OnFindLobbies);
        startGameButton?.onClick.AddListener(OnStartGame);
        inviteFriendsButton?.onClick.AddListener(OnInviteFriends);
        leaveLobbyButton?.onClick.AddListener(OnLeaveLobby);

        // Add click-to-copy listener on lobby code
        if (lobbyCodeText != null)
        {
            Button codeButton = lobbyCodeText.GetComponent<Button>();
            if (codeButton != null)
                codeButton.onClick.AddListener(CopyLobbyCode);
            else
                Debug.LogWarning("LobbyCodeText has no Button component for copying.");
        }

        // Subscribe to events
        SteamLobbyManager.Instance.OnLobbyCodeGenerated += OnLobbyCreated;
        SteamLobbyManager.Instance.OnJoinedLobby += OnJoinedLobby;
        SteamLobbyManager.Instance.OnPlayerListChanged += OnPlayerListChanged;
        SteamLobbyManager.Instance.OnConnectionFailed += OnConnectionFailed;

        // Initial UI state
        ShowMainMenu();
        SetStatus("Ready to create or join lobby");
    }

    #region Button Handlers

    private void OnCreateLobby()
    {
        string lobbyName = string.IsNullOrEmpty(lobbyNameInput.text) ? "Game Lobby" : lobbyNameInput.text;
        int maxPlayers = 4;

        if (!string.IsNullOrEmpty(maxPlayersInput.text))
        {
            if (!int.TryParse(maxPlayersInput.text, out maxPlayers))
                maxPlayers = 4;
        }

        bool isPrivate = privateToggle != null && privateToggle.isOn;

        SetStatus("Creating lobby...");
        SteamLobbyManager.Instance.CreateLobby(lobbyName, maxPlayers, isPrivate);
    }

    private void OnJoinLobby()
    {
        string lobbyCode = lobbyCodeInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyCode))
        {
            SetStatus("Please enter a lobby code");
            return;
        }

        SetStatus($"Joining lobby {lobbyCode}...");
        SteamLobbyManager.Instance.JoinLobbyByString(lobbyCode);
    }

    private void OnFindLobbies()
    {
        SetStatus("Searching for lobbies...");
        SteamLobbyManager.Instance.FindLobbies();
        // Results will appear in console - you can extend this to show a list UI
    }

    private void OnStartGame()
    {
        if (!SteamLobbyManager.Instance.IsHost())
        {
            SetStatus("Only host can start the game!");
            return;
        }

        SetStatus("Starting game...");
        SteamLobbyManager.Instance.StartGame();
    }

    private void OnInviteFriends()
    {
        SteamLobbyManager.Instance.OpenSteamInviteDialog();
        SetStatus("Steam invite dialog opened (Shift+Tab)");
    }

    private void OnLeaveLobby()
    {
        SteamLobbyManager.Instance.LeaveLobby();
        ShowMainMenu();
        SetStatus("Left lobby");
    }

    #endregion

    #region Event Handlers

    private void OnLobbyCreated(string lobbyId)
    {
        ShowLobbyPanel();

        if (lobbyCodeText != null)
            lobbyCodeText.text = $"Lobby ID: {lobbyId}";

        if (lobbyNameText != null)
            lobbyNameText.text = lobbyNameInput.text;

        // Host can start game
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(true);

        SetStatus("Lobby created! Share the ID with friends or use Steam invite");
    }

    private void OnJoinedLobby()
    {
        ShowLobbyPanel();

        string lobbyId = SteamLobbyManager.Instance.GetLobbyId();

        if (lobbyCodeText != null)
            lobbyCodeText.text = $"Lobby ID: {lobbyId}";

        // Only host can start game
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(SteamLobbyManager.Instance.IsHost());

        SetStatus("Joined lobby!");
    }

    private void OnPlayerListChanged(List<string> players)
    {
        if (playerListText != null)
        {
            playerListText.text = $"Players ({players.Count}):\n";
            foreach (string player in players)
            {
                playerListText.text += $"• {player}\n";
            }
        }
    }

    private void OnConnectionFailed()
    {
        ShowMainMenu();
        SetStatus("Failed to connect to lobby");
    }

    #endregion

    #region UI State Management

    private void ShowMainMenu()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    private void ShowLobbyPanel()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"[SteamLobbyUI] {message}");
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnLobbyCodeGenerated -= OnLobbyCreated;
            SteamLobbyManager.Instance.OnJoinedLobby -= OnJoinedLobby;
            SteamLobbyManager.Instance.OnPlayerListChanged -= OnPlayerListChanged;
            SteamLobbyManager.Instance.OnConnectionFailed -= OnConnectionFailed;
        }
    }

    private void CopyLobbyCode()
    {
        if (lobbyCodeText == null)
            return;

        string code = lobbyCodeText.text.Replace("Lobby ID: ", "").Trim();
        GUIUtility.systemCopyBuffer = code;

        SetStatus("Lobby code copied to clipboard!");
        Debug.Log("[SteamLobbyUI] Copied lobby code: " + code);
    }
}