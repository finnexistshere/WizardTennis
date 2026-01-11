using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Collections.Generic;

public class MatchmakingManager : MonoBehaviour
{
    [Header("UI References")]
    public Button createLobbyButton;
    public Button joinLobbyButton;
    public TMP_InputField joinCodeInput;
    public TMP_Text lobbyCodeText;
    public Button startGameButton;

    [Header("Game Settings")]
    public string networkSceneName;
    public int maxPlayers = 2;

    private Lobby currentLobby;

    // Hold allocation for host
    private Allocation hostAllocation;
    private bool isHost = false;

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        createLobbyButton.onClick.AddListener(CreateLobby);
        joinLobbyButton.onClick.AddListener(JoinLobby);
        startGameButton.onClick.AddListener(OnStartGameClicked);

        startGameButton.interactable = false;
    }

    #region Host Lobby
    public async void CreateLobby()
    {
        try
        {
            // Create relay allocation
            hostAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);

            // Get join code
            string relayCode = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);

            // Create lobby and store relay code
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                "MyGameLobby",
                maxPlayers,
                new CreateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "relayCode", new DataObject(DataObject.VisibilityOptions.Public, relayCode) }
                    }
                }
            );

            lobbyCodeText.text = $"Lobby Code: {relayCode}";
            isHost = true;
            startGameButton.interactable = true;

            Debug.Log($"Lobby created. Relay code: {relayCode}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to create lobby: " + e);
        }
    }

    private void OnStartGameClicked()
    {
        if (!isHost || hostAllocation == null) return;

        // Set up Unity Transport
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(hostAllocation, "dtls"));

        NetworkManager.Singleton.StartHost();
        SceneManager.LoadScene(networkSceneName);
    }
    #endregion

    #region Join Lobby
    public async void JoinLobby()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning("Join code empty!");
            return;
        }

        try
        {
            // Join lobby
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);

            // Get relay code from lobby data
            if (!currentLobby.Data.TryGetValue("relayCode", out DataObject relayDataObj))
            {
                Debug.LogError("Relay code missing in lobby!");
                return;
            }

            string relayCode = relayDataObj.Value;

            // Join relay allocation
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(relayCode);

            // Setup network transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAlloc, "dtls"));

            // Load game scene before starting client
            await LoadGameSceneAsync();

            NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to join lobby: " + e);
        }
    }
    #endregion

    private async Task LoadGameSceneAsync()
    {
        if (SceneManager.GetActiveScene().name != networkSceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(networkSceneName, LoadSceneMode.Single);
            while (!op.isDone)
                await Task.Yield();
        }
    }
}
