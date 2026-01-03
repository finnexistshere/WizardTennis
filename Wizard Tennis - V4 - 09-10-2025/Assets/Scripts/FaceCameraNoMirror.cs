using UnityEngine;

public class FaceCameraNoMirror : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (!cam) return;

        // Face the camera WITHOUT mirroring
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position,
            Vector3.up
        );
    }
}
