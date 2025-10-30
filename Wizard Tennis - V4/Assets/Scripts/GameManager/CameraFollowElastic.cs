using UnityEngine;
using UnityEngine.UIElements;

public class CameraElasticSway : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform player; // Assign in Inspector or auto-find by tag
    [SerializeField] private float maxSway = 2f; // maximum distance camera can move from center
    [SerializeField] private float swayStrength = 0.3f; // how much player movement affects sway
    [SerializeField] private float smoothSpeed = 5f; // how smoothly camera moves

    private Vector3 startPosition;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // Remember the original world position (center point)
        startPosition = transform.position;
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // Get player's X offset relative to camera's X position
        float xOffset = (player.position.x - startPosition.x) * swayStrength;
        xOffset = Mathf.Clamp(xOffset, -maxSway, maxSway);

        // Target position is only shifted on X
        Vector3 targetPos = new Vector3(startPosition.x + xOffset, startPosition.y, startPosition.z);

        // Smoothly move camera toward this position
        transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(player.position)) * Quaternion.Euler(45, 180, 0);
        Vector3 test = transform.localEulerAngles;
        transform.rotation = Quaternion.Euler(test.x, test.y, 0);
    }
}
