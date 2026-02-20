using Unity.Netcode;
using UnityEngine;

public class PlayerCameraRig : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private GameObject cameraRigPrefab;
    [SerializeField] private Transform cameraSpawnPoint;

    private GameObject cameraInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // only for local player

        Debug.Log($"[PlayerCameraRig] OnNetworkSpawn for local player {OwnerClientId}");

        if (cameraRigPrefab != null)
        {
            // CRITICAL: Disable any existing cameras before spawning ours
            DisableExistingCameras();

            cameraInstance = Instantiate(cameraRigPrefab);
            Debug.Log($"[PlayerCameraRig] Instantiated camera rig");

            // Assign player and spawn point
            CameraElasticSway camSway = cameraInstance.GetComponent<CameraElasticSway>();
            if (camSway != null)
            {
                camSway.player = transform;
                if (cameraSpawnPoint != null)
                    camSway.spawnPoint = cameraSpawnPoint;
                Debug.Log($"[PlayerCameraRig] Assigned player to CameraElasticSway");
            }

            // Ensure camera and audio listener are enabled
            Camera cam = cameraInstance.GetComponent<Camera>();
            if (cam != null)
            {
                cam.enabled = true;
                Debug.Log($"[PlayerCameraRig] Camera enabled - Depth: {cam.depth}");
            }

            AudioListener audio = cameraInstance.GetComponent<AudioListener>();
            if (audio != null)
            {
                audio.enabled = true;
                Debug.Log($"[PlayerCameraRig] AudioListener enabled");
            }

            // Mark as main camera
            if (cam != null)
            {
                cam.tag = "MainCamera";
            }
        }
        else
        {
            Debug.LogError("[PlayerCameraRig] Camera rig prefab not assigned!");
        }
    }

    /// <summary>
    /// Disable any cameras that aren't ours (from lobby scene, etc.)
    /// </summary>
    private void DisableExistingCameras()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>();
        Debug.Log($"[PlayerCameraRig] Found {allCameras.Length} existing cameras before spawn");

        foreach (Camera cam in allCameras)
        {
            // Don't disable cameras that are part of other players' rigs
            var otherPlayerCam = cam.GetComponentInParent<PlayerCameraRig>();
            if (otherPlayerCam == null)
            {
                Debug.Log($"[PlayerCameraRig] Disabling existing camera: {cam.name} from scene {cam.gameObject.scene.name}");
                cam.enabled = false;

                // Also disable audio listeners
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (cameraInstance != null)
        {
            Destroy(cameraInstance);
            Debug.Log("[PlayerCameraRig] Camera rig destroyed");
        }
    }

    private void OnDestroy()
    {
        if (cameraInstance != null)
        {
            Destroy(cameraInstance);
        }
    }
}