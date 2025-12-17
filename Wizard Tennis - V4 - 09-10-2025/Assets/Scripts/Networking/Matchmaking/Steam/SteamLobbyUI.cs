using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
    public TextMeshProUGUI playerListText; // Keep for fallback
    public Button startGameButton;
    public Button inviteFriendsButton;
    public Button leaveLobbyButton;

    [Header("Player List with Avatars")]
    [Tooltip("Container for player entries (uses prefab)")]
    public Transform playerListContainer;
    [Tooltip("Prefab with Image (avatar) and TextMeshProUGUI (name)")]
    public GameObject playerEntryPrefab;

    [Header("Status")]
    public TextMeshProUGUI statusText;

    private SteamLobbyManager Lobby => SteamLobbyManager.Instance;
    private List<GameObject> playerEntries = new List<GameObject>();

    private void Start()
    {
        if (Lobby == null)
        {
            Debug.LogError("[SteamLobbyUI] SteamLobbyManager not found!");
            SetStatus("ERROR: SteamLobbyManager missing!");
            return;
        }

        // Button listeners
        createLobbyButton?.onClick.AddListener(OnCreateLobby);
        joinLobbyButton?.onClick.AddListener(OnJoinLobby);
        findLobbiesButton?.onClick.AddListener(OnFindLobbies);
        startGameButton?.onClick.AddListener(OnStartGame);
        inviteFriendsButton?.onClick.AddListener(OnInviteFriends);
        leaveLobbyButton?.onClick.AddListener(OnLeaveLobby);

        // Click-to-copy lobby code
        if (lobbyCodeText != null)
        {
            var btn = lobbyCodeText.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(CopyLobbyCode);
        }

        // Subscribe to lobby events
        Lobby.OnLobbyCodeGenerated += OnLobbyCreated;
        Lobby.OnJoinedLobby += OnJoinedLobby;
        Lobby.OnPlayerListChanged += OnPlayerListChanged;
        Lobby.OnPlayerListChangedWithData += OnPlayerListChangedWithAvatars; // NEW
        Lobby.OnConnectionFailed += OnConnectionFailed;

        ShowMainMenu();
        SetStatus("Ready");
    }

    #region Button Handlers

    private void OnCreateLobby()
    {
        string lobbyName = string.IsNullOrEmpty(lobbyNameInput.text)
            ? "Game Lobby"
            : lobbyNameInput.text;

        int maxPlayers = 4;
        if (!string.IsNullOrEmpty(maxPlayersInput.text))
            int.TryParse(maxPlayersInput.text, out maxPlayers);

        SetStatus("Creating lobby...");
        Lobby.CreateLobby(lobbyName, maxPlayers);
    }

    private void OnJoinLobby()
    {
        string lobbyCode = lobbyCodeInput.text.Trim();

        if (string.IsNullOrEmpty(lobbyCode))
        {
            SetStatus("Enter a lobby code");
            return;
        }

        SetStatus($"Joining lobby {lobbyCode}...");
        Lobby.JoinLobbyByString(lobbyCode);
    }

    private void OnFindLobbies()
    {
        SetStatus("Public matchmaking not implemented");
    }

    private void OnStartGame()
    {
        if (!Lobby.IsHost())
        {
            SetStatus("Only the host can start the game");
            return;
        }

        SetStatus("Starting game...");
        Lobby.StartGame();
    }

    private void OnInviteFriends()
    {
        Lobby.OpenSteamInviteDialog();
        SetStatus("Steam invite opened (Shift+Tab)");
    }

    private void OnLeaveLobby()
    {
        Lobby.LeaveLobby();

        // Clear avatar cache when leaving
        if (SteamAvatarManager.Instance != null)
            SteamAvatarManager.Instance.ClearCache();

        ShowMainMenu();
        SetStatus("Left lobby");
    }

    #endregion

    #region Lobby Events

    private void OnLobbyCreated(string lobbyId)
    {
        ShowLobbyPanel();

        lobbyCodeText.text = $"Lobby ID: {lobbyId}";
        lobbyNameText.text = lobbyNameInput.text;

        UpdateStartButton();
        SetStatus("Lobby created");
    }

    private void OnJoinedLobby()
    {
        ShowLobbyPanel();

        lobbyCodeText.text = $"Lobby ID: {Lobby.GetLobbyId()}";
        UpdateStartButton();

        SetStatus("Joined lobby");
    }

    private void OnPlayerListChanged(List<string> players)
    {
        // Fallback if not using avatar system
        if (playerListText != null && (playerListContainer == null || playerEntryPrefab == null))
        {
            playerListText.text = $"Players ({players.Count}):\n";
            foreach (var p in players)
                playerListText.text += $"• {p}\n";
        }

        UpdateStartButton();
        SetStatus($"{players.Count} player(s) in lobby");
    }

    private void OnPlayerListChangedWithAvatars(List<SteamLobbyManager.LobbyMember> members)
    {
        // Only use avatar system if components are set up
        if (playerListContainer == null || playerEntryPrefab == null)
            return;

        // Clear existing entries
        foreach (var entry in playerEntries)
            Destroy(entry);
        playerEntries.Clear();

        // Create new entries with avatars
        foreach (var member in members)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            playerEntries.Add(entry);

            // Find components in prefab
            Image avatarImage = entry.GetComponentInChildren<Image>();
            TextMeshProUGUI nameText = entry.GetComponentInChildren<TextMeshProUGUI>();

            // Set name
            if (nameText != null)
            {
                string suffix = member.isHost ? " (Host)" : "";
                nameText.text = member.name + suffix;
            }

            // Fetch and set avatar
            if (avatarImage != null && SteamAvatarManager.Instance != null)
            {
                SteamAvatarManager.Instance.GetAvatar(member.steamId, (sprite) =>
                {
                    if (avatarImage != null && sprite != null)
                        avatarImage.sprite = sprite;
                });
            }
        }

        SetStatus($"{members.Count} player(s) in lobby");
    }

    private void OnConnectionFailed()
    {
        ShowMainMenu();
        SetStatus("Failed to connect to lobby");
    }

    #endregion

    #region UI Helpers

    private void ShowMainMenu()
    {
        mainMenuPanel?.SetActive(true);
        lobbyPanel?.SetActive(false);
    }

    private void ShowLobbyPanel()
    {
        mainMenuPanel?.SetActive(false);
        lobbyPanel?.SetActive(true);
    }

    private void UpdateStartButton()
    {
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(Lobby.IsHost());
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[SteamLobbyUI] " + message);
    }

    #endregion

    private void CopyLobbyCode()
    {
        if (lobbyCodeText == null)
            return;

        string code = lobbyCodeText.text.Replace("Lobby ID:", "").Trim();
        GUIUtility.systemCopyBuffer = code;

        SetStatus("Lobby code copied");
    }

    private void OnDestroy()
    {
        if (Lobby == null)
            return;

        Lobby.OnLobbyCodeGenerated -= OnLobbyCreated;
        Lobby.OnJoinedLobby -= OnJoinedLobby;
        Lobby.OnPlayerListChanged -= OnPlayerListChanged;
        Lobby.OnPlayerListChangedWithData -= OnPlayerListChangedWithAvatars;
        Lobby.OnConnectionFailed -= OnConnectionFailed;
    }
}