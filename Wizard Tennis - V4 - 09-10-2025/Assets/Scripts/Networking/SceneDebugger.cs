using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach this to any GameObject in your game scene
/// Press F1 to see detailed network and scene state
/// </summary>
public class SceneDebugger : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PrintDebugInfo();
        }
    }

    private void PrintDebugInfo()
    {
        Debug.Log("==================== DEBUG INFO ====================");

        // Network State
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            Debug.Log("=== NETWORK STATE ===");
            Debug.Log($"IsListening: {nm.IsListening}");
            Debug.Log($"IsHost: {nm.IsHost}");
            Debug.Log($"IsClient: {nm.IsClient}");
            Debug.Log($"IsServer: {nm.IsServer}");
            Debug.Log($"Connected Clients: {nm.ConnectedClientsList.Count}");
            Debug.Log($"Local Client ID: {nm.LocalClientId}");
            Debug.Log($"Local Player Object: {nm.LocalClient?.PlayerObject?.name ?? "null"}");

            if (nm.LocalClient?.PlayerObject != null)
            {
                Debug.Log($"Local Player Position: {nm.LocalClient.PlayerObject.transform.position}");
            }
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is NULL!");
        }

        // Scene State
        Debug.Log("=== SCENE STATE ===");
        Debug.Log($"Active Scene: {SceneManager.GetActiveScene().name}");
        Debug.Log($"Total Loaded Scenes: {SceneManager.sceneCount}");

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"  [{i}] {scene.name} - IsLoaded: {scene.isLoaded}, RootObjects: {scene.rootCount}");

            // List some root objects
            GameObject[] roots = scene.GetRootGameObjects();
            for (int j = 0; j < Mathf.Min(5, roots.Length); j++)
            {
                Debug.Log($"      - {roots[j].name}");
            }
            if (roots.Length > 5)
                Debug.Log($"      ... and {roots.Length - 5} more objects");
        }

        // Camera State
        Debug.Log("=== CAMERA STATE ===");
        Camera[] cameras = FindObjectsOfType<Camera>();
        Debug.Log($"Total Cameras in Scene: {cameras.Length}");
        foreach (Camera cam in cameras)
        {
            Debug.Log($"  - {cam.name} | Enabled: {cam.enabled} | Depth: {cam.depth} | Scene: {cam.gameObject.scene.name}");
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"Main Camera: {mainCam.name} in scene {mainCam.gameObject.scene.name}");
        }
        else
        {
            Debug.LogWarning("No Main Camera found!");
        }

        // Audio Listeners
        Debug.Log("=== AUDIO LISTENERS ===");
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        Debug.Log($"Total Audio Listeners: {listeners.Length}");
        foreach (AudioListener listener in listeners)
        {
            Debug.Log($"  - {listener.name} | Enabled: {listener.enabled} | Scene: {listener.gameObject.scene.name}");
        }

        // SteamLobbyManager State
        Debug.Log("=== STEAM LOBBY STATE ===");
        if (SteamLobbyManager.Instance != null)
        {
            Debug.Log($"SteamLobbyManager exists");
            Debug.Log($"IsHost: {SteamLobbyManager.Instance.IsHost()}");
            Debug.Log($"Lobby ID: {SteamLobbyManager.Instance.GetLobbyId()}");
        }
        else
        {
            Debug.LogWarning("SteamLobbyManager.Instance is NULL!");
        }

        Debug.Log("==================================================");
    }
}