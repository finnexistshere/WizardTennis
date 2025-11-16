using Unity.Netcode;
using UnityEngine;

public class PlayerCameraRig : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private GameObject cameraRigPrefab; // assign your camera rig prefab
    [SerializeField] private Transform cameraSpawnPoint; // optional empty transform behind player

    private GameObject cameraInstance;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // only for local player

        if (cameraRigPrefab != null)
        {
            cameraInstance = Instantiate(cameraRigPrefab);

            // Assign player and spawn point
            CameraElasticSway camSway = cameraInstance.GetComponent<CameraElasticSway>();
            if (camSway != null)
            {
                camSway.player = transform;
                if (cameraSpawnPoint != null)
                    camSway.spawnPoint = cameraSpawnPoint;
            }

            // Ensure camera and audio listener are enabled
            Camera cam = cameraInstance.GetComponent<Camera>();
            if (cam != null) cam.enabled = true;

            AudioListener audio = cameraInstance.GetComponent<AudioListener>();
            if (audio != null) audio.enabled = true;
        }
    }
}
