using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

/// <summary>
/// Attach this to your NetworkManager in the Game Scene.
/// It automatically starts the host or client when the scene loads.
/// </summary>
public class GameSceneNetworkStarter : MonoBehaviour
{
    private async void Start()
    {
        // Wait a moment for the scene to fully load
        await Task.Delay(500);

        if (LobbyManager.Instance == null)
        {
            Debug.LogError("LobbyManager not found! Make sure it persists from Main Menu.");
            return;
        }

        bool isHost = LobbyManager.Instance.IsHost();
        Debug.Log($"Game scene loaded. Player is {(isHost ? "HOST" : "CLIENT")}");

        if (isHost)
        {
            // Start as host
            NetworkManager.Singleton.StartHost();
            Debug.Log("Started as Host");

            // Update lobby with connection info
            await UpdateLobbyWithConnectionData();
        }
        else
        {
            // Wait for host's connection data, then connect
            await ConnectAsClient();
        }
    }

    private async Task UpdateLobbyWithConnectionData()
    {
        try
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            string connectionData = $"{transport.ConnectionData.Address}:{transport.ConnectionData.Port}";

            string lobbyId = LobbyManager.Instance.GetJoinedLobbyId();
            if (string.IsNullOrEmpty(lobbyId))
            {
                Debug.LogError("No lobby ID found!");
                return;
            }

            await LobbyService.Instance.UpdateLobbyAsync(lobbyId, new UpdateLobbyOptions
            {
                Data = new System.Collections.Generic.Dictionary<string, DataObject>
                {
                    { LobbyManager.KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, connectionData) }
                }
            });

            Debug.Log($"Updated lobby with connection data: {connectionData}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to update lobby: " + e);
        }
    }

    private async Task ConnectAsClient()
    {
        Debug.Log("Waiting for host connection data...");

        // Poll for connection data
        for (int i = 0; i < 30; i++) // Try for 15 seconds
        {
            await Task.Delay(500);

            try
            {
                string lobbyId = LobbyManager.Instance.GetJoinedLobbyId();
                if (string.IsNullOrEmpty(lobbyId))
                {
                    Debug.LogError("No lobby ID found!");
                    return;
                }

                var lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);

                if (lobby.Data.ContainsKey(LobbyManager.KEY_RELAY_JOIN_CODE))
                {
                    string connectionData = lobby.Data[LobbyManager.KEY_RELAY_JOIN_CODE].Value;

                    if (connectionData != "0" && connectionData != "STARTING")
                    {
                        // Parse connection data
                        string[] parts = connectionData.Split(':');
                        if (parts.Length == 2)
                        {
                            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                            transport.SetConnectionData(parts[0], ushort.Parse(parts[1]));

                            // Connect as client
                            NetworkManager.Singleton.StartClient();
                            Debug.Log($"Connected to host at {connectionData}");
                            return;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Polling error: " + e.Message);
            }
        }

        Debug.LogError("Timed out waiting for host connection data!");
    }
}