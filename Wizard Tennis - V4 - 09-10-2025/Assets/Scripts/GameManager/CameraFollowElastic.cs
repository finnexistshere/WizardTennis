using UnityEngine;

public class CameraElasticSway : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] public Transform player;
    [SerializeField] public Transform spawnPoint;

    [Header("Camera Settings")]
    [SerializeField] private float maxSway = 2f;       // maximum X offset
    [SerializeField] private float swayStrength = 0.3f;// how much player movement affects sway
    [SerializeField] private float smoothSpeed = 5f;   // how smooth the camera moves

    private Vector3 startPosition;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // Initialize start position
        if (spawnPoint != null)
            startPosition = spawnPoint.position;
        else
            startPosition = transform.position;

        // Place camera at start position
        transform.position = startPosition;
    }

    void LateUpdate()
    {
        if (player == null) return;

        // Compute X offset relative to start position
        float xOffset = (player.position.x - startPosition.x) * swayStrength;
        xOffset = Mathf.Clamp(xOffset, -maxSway, maxSway);

        // Target position only shifts X like the old version
        Vector3 targetPos = new Vector3(startPosition.x + xOffset, startPosition.y, startPosition.z);

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
    }
}
