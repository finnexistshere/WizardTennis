using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;

public class LobbyUI : MonoBehaviour
{
    public Button createLobbyButton;
    public TMP_InputField joinCodeInput;
    public Button joinLobbyButton;
    public TMP_Text lobbyCodeText;
    public TMP_Text playerListText;

    public string defaultLobbyName = "MyLobby";
    public int maxPlayers = 4;
    public bool isPrivate = false;
    public string gameMode = "CaptureTheFlag";

    private async void Start()
    {
        createLobbyButton.interactable = false;
        joinLobbyButton.interactable = false;

        // Authenticate player
        await LobbyManager.Instance.AuthenticateAsync("Player" + Random.Range(1000, 9999));

        createLobbyButton.interactable = true;
        joinLobbyButton.interactable = true;

        createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
    }

    private async void Update()
    {
        string code = LobbyManager.Instance.GetJoinedLobbyCode();
        lobbyCodeText.text = code != null ? $"Lobby Code: {code}" : "No lobby created/joined";

        // Update player list
        if (code != null)
        {
            List<string> players = await LobbyManager.Instance.GetPlayerNamesAsync();
            playerListText.text = string.Join("\n", players);
        }
    }

    private void OnCreateLobbyClicked()
    {
        LobbyManager.Instance.CreateLobby(defaultLobbyName, maxPlayers, isPrivate, gameMode);
    }

    private void OnJoinLobbyClicked()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Please enter a lobby code.");
            return;
        }

        LobbyManager.Instance.JoinLobbyByCode(code);
    }
}
