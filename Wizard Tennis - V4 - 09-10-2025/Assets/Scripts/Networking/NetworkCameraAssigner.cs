using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Manages assignment of pre-existing cameras in the scene to networked players
/// </summary>
public class NetworkCameraManager : MonoBehaviour
{
    private static NetworkCameraManager instance;

    private static List<NetworkCameraAssigner> registeredCameras = new List<NetworkCameraAssigner>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[NetworkCameraManager] Multiple instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    /// <summary>
    /// Registers a camera with the manager
    /// </summary>
    public static void RegisterCamera(NetworkCameraAssigner camera)
    {
        if (!registeredCameras.Contains(camera))
        {
            registeredCameras.Add(camera);
            Debug.Log($"[NetworkCameraManager] Registered camera: {camera.gameObject.name} (Host: {camera.IsHostCamera})");
        }
    }

    /// <summary>
    /// Unregisters a camera from the manager
    /// </summary>
    public static void UnregisterCamera(NetworkCameraAssigner camera)
    {
        registeredCameras.Remove(camera);
    }

    /// <summary>
    /// Attempts to assign a camera to a player based on whether they're host or client
    /// </summary>
    public static bool TryAssignCameraToPlayer(bool isHost, Transform playerTransform, Transform spawnTransform)
    {
        Debug.Log($"[NetworkCameraManager] Attempting to assign {(isHost ? "host" : "client")} camera. Registered cameras: {registeredCameras.Count}");

        // Debug: List all registered cameras
        foreach (var cam in registeredCameras)
        {
            if (cam != null)
            {
                Debug.Log($"  - Camera: {cam.gameObject.name}, IsHost: {cam.IsHostCamera}, IsAssigned: {cam.IsAssigned}");
            }
        }

        // Find an unassigned camera matching the host/client type
        NetworkCameraAssigner availableCamera = null;

        foreach (var camera in registeredCameras)
        {
            if (camera != null && !camera.IsAssigned && camera.IsHostCamera == isHost)
            {
                availableCamera = camera;
                break;
            }
        }

        if (availableCamera == null)
        {
            Debug.LogError($"[NetworkCameraManager] No available {(isHost ? "host" : "client")} camera found! Registered: {registeredCameras.Count}");
            return false;
        }

        availableCamera.AssignToPlayer(playerTransform, spawnTransform);
        return true;
    }

    /// <summary>
    /// Gets all registered cameras (useful for debugging)
    /// </summary>
    public static List<NetworkCameraAssigner> GetRegisteredCameras()
    {
        return new List<NetworkCameraAssigner>(registeredCameras);
    }

    /// <summary>
    /// Clears all camera assignments
    /// </summary>
    public static void ClearAllAssignments()
    {
        foreach (var camera in registeredCameras)
        {
            if (camera != null)
            {
                camera.UnassignCamera();
            }
        }
        Debug.Log("[NetworkCameraManager] All camera assignments cleared");
    }
}

/// <summary>
/// Attach to each camera in the scene and mark as host or client camera
/// </summary>
public class NetworkCameraAssigner : MonoBehaviour
{
    [Header("Camera Assignment")]
    [SerializeField] private bool isHostCamera = true; // Toggle this in inspector

    [Header("References")]
    [SerializeField] private CameraElasticSway cameraSwayComponent;

    private bool isAssigned = false;

    private void Awake()
    {
        // Get CameraElasticSway if not assigned
        if (cameraSwayComponent == null)
        {
            cameraSwayComponent = GetComponent<CameraElasticSway>();
            if (cameraSwayComponent == null)
            {
                Debug.LogError($"[NetworkCameraAssigner] No CameraElasticSway found on {gameObject.name}!");
            }
        }

        // Start disabled, will be enabled when assigned
        if (cameraSwayComponent != null)
        {
            cameraSwayComponent.enabled = false;
        }

        // Keep camera inactive but don't disable the GameObject yet
        // so that OnEnable can fire for registration
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.enabled = false;
        }

        Debug.Log($"[NetworkCameraAssigner] Camera {gameObject.name} initialized (Host: {isHostCamera})");
    }

    private void OnEnable()
    {
        // Register with the camera manager immediately when enabled
        NetworkCameraManager.RegisterCamera(this);
    }

    private void Start()
    {
        // Ensure registration even if OnEnable was missed
        NetworkCameraManager.RegisterCamera(this);
    }

    private void OnDestroy()
    {
        // Unregister when destroyed
        NetworkCameraManager.UnregisterCamera(this);
    }

    public bool IsHostCamera => isHostCamera;
    public bool IsAssigned => isAssigned;

    /// <summary>
    /// Assigns this camera to follow the specified player
    /// </summary>
    public void AssignToPlayer(Transform playerTransform, Transform spawnTransform)
    {
        if (isAssigned)
        {
            Debug.LogWarning($"[NetworkCameraAssigner] Camera {gameObject.name} is already assigned!");
            return;
        }

        if (cameraSwayComponent == null)
        {
            Debug.LogError($"[NetworkCameraAssigner] Cannot assign camera - no CameraElasticSway component!");
            return;
        }

        Debug.Log($"[NetworkCameraAssigner] Assigning {gameObject.name} to player {playerTransform.name}");

        // Set up the camera sway component
        cameraSwayComponent.player = playerTransform;
        cameraSwayComponent.spawnPoint = spawnTransform;

        // Enable the component and activate the camera
        cameraSwayComponent.enabled = true;

        // Enable the Camera component
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.enabled = true;
            Debug.Log($"[NetworkCameraAssigner] Camera component enabled on {gameObject.name}");
        }

        // Make sure AudioListener is on the correct camera
        AudioListener listener = GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = true;
        }

        // Ensure GameObject is active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        isAssigned = true;

        Debug.Log($"[NetworkCameraAssigner] {(isHostCamera ? "Host" : "Client")} camera successfully assigned to player at {playerTransform.name}");
    }

    /// <summary>
    /// Unassigns this camera from its current player
    /// </summary>
    public void UnassignCamera()
    {
        if (!isAssigned) return;

        if (cameraSwayComponent != null)
        {
            cameraSwayComponent.player = null;
            cameraSwayComponent.enabled = false;
        }

        gameObject.SetActive(false);
        isAssigned = false;

        Debug.Log($"[NetworkCameraAssigner] Camera {gameObject.name} unassigned");
    }
}