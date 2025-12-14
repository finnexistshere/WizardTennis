using UnityEngine;
using Steamworks;

/// <summary>
/// Simple Steam initialization for Facepunch.Steamworks
/// Place this in your first scene (menu/lobby scene)
/// </summary>
public class FacepunchSteamManager : MonoBehaviour
{
    public static FacepunchSteamManager Instance { get; private set; }

    [SerializeField] private uint appId = 480; // Use 480 for testing, your App ID for production

    public static bool Initialized { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSteam();
    }

    private void InitializeSteam()
    {
        try
        {
            // Initialize Steam with Facepunch
            SteamClient.Init(appId, true);
            Initialized = true;

            Debug.Log("[FacepunchSteam] ? Steam initialized successfully");
            Debug.Log($"[FacepunchSteam] App ID: {appId}");
            Debug.Log($"[FacepunchSteam] Steam ID: {SteamClient.SteamId}");
            Debug.Log($"[FacepunchSteam] Username: {SteamClient.Name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FacepunchSteam] ? Failed to initialize Steam: {e.Message}");
            Debug.LogError("[FacepunchSteam] Make sure Steam is running and steam_appid.txt is in your project root!");
            Initialized = false;
        }
    }

    private void Update()
    {
        // CRITICAL: Facepunch requires RunCallbacks every frame
        if (Initialized)
        {
            SteamClient.RunCallbacks();
        }
    }

    private void OnApplicationQuit()
    {
        if (Initialized)
        {
            SteamClient.Shutdown();
            Debug.Log("[FacepunchSteam] Steam shut down");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this && Initialized)
        {
            SteamClient.Shutdown();
        }
    }
}