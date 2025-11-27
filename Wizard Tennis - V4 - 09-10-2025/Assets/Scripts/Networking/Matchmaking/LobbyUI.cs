using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;

public class LobbyUI : MonoBehaviour
{
    [Header("Lobby Creation")]
    public Button createLobbyButton;
    public TMP_InputField lobbyNameInput;

    [Header("Lobby Joining")]
    public TMP_InputField joinCodeInput;
    public Button joinLobbyButton;

    [Header("Lobby Info")]
    public TMP_Text lobbyCodeText;
    public TMP_Text playerListText;
    public GameObject lobbyPanel;
    public GameObject menuPanel;

    [Header("Game Start")]
    public Button startGameButton;
    public TMP_Text waitingForHostText;

    [Header("Settings")]
    public string defaultLobbyName = "MyLobby";
    public int maxPlayers = 2;
    public bool isPrivate = false;
    public string gameMode = "CaptureTheFlag";

    private bool isAuthenticating = false;

    private async void Start()
    {
        // Initially disable buttons
        createLobbyButton.interactable = false;
        joinLobbyButton.interactable = false;
        startGameButton.gameObject.SetActive(false);

        if (waitingForHostText != null)
            waitingForHostText.gameObject.SetActive(false);

        // Show menu, hide lobby
        if (menuPanel != null) menuPanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        // Authenticate
        if (!isAuthenticating)
        {
            isAuthenticating = true;
            await LobbyManager.Instance.AuthenticateAsync("Player" + Random.Range(1000, 9999));
            isAuthenticating = false;

            // Enable buttons after auth
            createLobbyButton.interactable = true;
            joinLobbyButton.interactable = true;
        }

        // Setup button listeners
        createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);

        // Subscribe to lobby events
        LobbyManager.Instance.OnPlayerListChanged += UpdatePlayerListUI;
        LobbyManager.Instance.OnJoinedLobby += OnJoinedLobby;
    }

    private void Update()
    {
        // Update lobby code display
        string code = LobbyManager.Instance.GetJoinedLobbyCode();
        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = code != null ? $"Lobby Code: {code}" : "Not in lobby";
        }

        // Show start button only for host
        bool isHost = LobbyManager.Instance.IsHost();
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(isHost);

        // Show waiting text only for non-host
        if (waitingForHostText != null)
            waitingForHostText.gameObject.SetActive(!isHost && code != null);
    }

    private void OnCreateLobbyClicked()
    {
        string lobbyName = defaultLobbyName;
        if (lobbyNameInput != null && !string.IsNullOrEmpty(lobbyNameInput.text))
        {
            lobbyName = lobbyNameInput.text;
        }

        LobbyManager.Instance.CreateLobby(lobbyName, maxPlayers, isPrivate, gameMode);
    }

    private void OnJoinLobbyClicked()
    {
        string code = joinCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Please enter a lobby code.");
            return;
        }

        Debug.Log($"Join button clicked with code: {code}");
        LobbyManager.Instance.JoinLobbyByCode(code);
    }

    private void OnJoinedLobby()
    {
        Debug.Log("OnJoinedLobby event triggered!");

        // Switch to lobby panel
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            Debug.Log("Menu panel hidden");
        }

        if (lobbyPanel != null)
        {
            lobbyPanel.SetActive(true);
            Debug.Log("Lobby panel shown");
        }

        Debug.Log("Joined lobby, switched to lobby panel");
    }

    private void UpdatePlayerListUI(List<string> players)
    {
        if (playerListText != null)
        {
            playerListText.text = "Players:\n" + string.Join("\n", players);
        }
    }

    private void OnStartGameClicked()
    {
        Debug.Log("Host clicked Start Game");
        LobbyManager.Instance.StartGame();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnPlayerListChanged -= UpdatePlayerListUI;
            LobbyManager.Instance.OnJoinedLobby -= OnJoinedLobby;
        }
    }
}