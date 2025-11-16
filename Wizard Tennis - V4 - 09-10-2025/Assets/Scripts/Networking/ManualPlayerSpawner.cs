using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ManualPlayerSpawner : MonoBehaviour
{
    [Header("References")]
    public NetworkManager networkManager;
    public GameObject playerPrefab;

    [Header("Spawn Positions")]
    public Vector3 hostSpawnPosition = new Vector3(-5, 1, 0);
    public Vector3 clientSpawnPosition = new Vector3(5, 1, 0);

    [Header("UI Buttons")]
    public Button hostButton;
    public Button clientButton;

    void Start()
    {
        hostButton.onClick.AddListener(StartHostAndSpawnPlayer);
        clientButton.onClick.AddListener(StartClientAndSpawnPlayer);
    }

    void StartHostAndSpawnPlayer()
    {
        hostButton.interactable = false;
        clientButton.interactable = false;

        networkManager.StartHost();
        SpawnLocalPlayer(NetworkManager.Singleton.LocalClientId);
    }

    void StartClientAndSpawnPlayer()
    {
        hostButton.interactable = false;
        clientButton.interactable = false;

        networkManager.StartClient();

        // Wait until connected before spawning
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            SpawnLocalPlayer(clientId);
        }
    }

    private void SpawnLocalPlayer(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("SpawnLocalPlayer called but we are not server or host!");
            return;
        }

        Vector3 spawnPos = clientId == NetworkManager.Singleton.LocalClientId
            ? (NetworkManager.Singleton.IsHost ? hostSpawnPosition : clientSpawnPosition)
            : clientSpawnPosition;

        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();


        if (!netObj)
        {
            Debug.LogError("Player prefab is missing NetworkObject! FIX THIS!");
            return;
        }

        netObj.SpawnAsPlayerObject(clientId);
        Debug.Log($"Spawned PLAYER for client {clientId} at {spawnPos}");
    }
}
