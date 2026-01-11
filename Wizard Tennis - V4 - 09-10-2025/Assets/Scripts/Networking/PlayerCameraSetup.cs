using UnityEngine;
using Unity.Netcode;

public class PlayerCameraSetup : NetworkBehaviour
{
    [SerializeField] private Camera cam; // assign prefab camera in inspector

    void Start()
    {
        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>(true); // find disabled camera
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer)
        {
            cam.gameObject.SetActive(false);
            return;
        }

        // Local player camera
        cam.gameObject.SetActive(true);
        cam.tag = "MainCamera"; // ensures Camera.main works
        cam.targetDisplay = 0; // render to main display
    }
}
