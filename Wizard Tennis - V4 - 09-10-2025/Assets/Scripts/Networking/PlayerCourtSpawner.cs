using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
public class TennisPlayerSpawner : MonoBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Spawn Points")]
    [Tooltip("Assign exactly 2 spawn points in the scene for host and client.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Camera Setup")]
    [Tooltip("Camera prefab with CameraElasticSway component")]
    [SerializeField] private GameObject cameraRigPrefab;

    private void Awake()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[TennisPlayerSpawner] No NetworkManager found!");
            return;
        }

        if (!NetworkManager.Singleton.IsServer) return;

        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoadComplete;
    }

    private void OnSceneLoadComplete(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        SpawnPlayers();
    }

    private void SpawnPlayers()
    {
        if (spawnPoints.Length < 2)
        {
            Debug.LogError("[TennisPlayerSpawner] You need exactly 2 spawn points.");
            return;
        }

        var clients = NetworkManager.Singleton.ConnectedClientsList;

        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            int spawnIndex = i % spawnPoints.Length;

            // Instantiate player at the spawn point
            GameObject player = Instantiate(playerPrefab, spawnPoints[spawnIndex].position, spawnPoints[spawnIndex].rotation);
            player.GetComponent<NetworkObject>().SpawnWithOwnership(client.ClientId);

            // Setup camera only for the owner
            if (client.ClientId == NetworkManager.Singleton.LocalClientId)
            {
                GameObject camRig = Instantiate(cameraRigPrefab);
                CameraElasticSway camSway = camRig.GetComponent<CameraElasticSway>();
                if (camSway != null)
                {
                    camSway.player = player.transform;
                    camRig.transform.position = spawnPoints[spawnIndex].position + Vector3.up * 5f - player.transform.forward * 10f;
                    camRig.transform.LookAt(player.transform.position + Vector3.up * 1.5f);
                }

                Camera cam = camRig.GetComponentInChildren<Camera>();
                AudioListener audio = camRig.GetComponentInChildren<AudioListener>();
                if (cam != null) cam.enabled = true;
                if (audio != null) audio.enabled = true;
            }
        }

        Debug.Log("[TennisPlayerSpawner] Players spawned.");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
        }
    }
}
