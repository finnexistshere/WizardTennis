using UnityEngine;

public class MainCharacterMovement_V1 : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    [Header("Bounce Settings")]
    public Transform meshChild;          // Assign the character mesh here
    public float bounceAmplitude = 0.05f; // How high it bounces
    public float bounceFrequency = 8.0f;  // How fast it bounces

    private Vector3 moveDirection = Vector3.zero;
    private CharacterController controller;
    private float bounceTimer = 0f;
    private Vector3 meshOriginalLocalPos;

    public bool gemini = false;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (meshChild != null)
            meshOriginalLocalPos = meshChild.localPosition;
    }

    void Update()
    {
        if (controller.isGrounded)
        {
            Vector3 input = Vector3.zero;

            // Default movement keys (WASD)
            KeyCode forward = KeyCode.W;
            KeyCode backward = KeyCode.S;
            KeyCode left = KeyCode.A;
            KeyCode right = KeyCode.D;

            // If OptionsManager exists, swap keys for left-handed mode
            if (OptionsManager.Instance != null && OptionsManager.Instance.leftHandedMode)
            {
                forward = KeyCode.UpArrow;
                backward = KeyCode.DownArrow;
                left = KeyCode.LeftArrow;
                right = KeyCode.RightArrow;
            }

            if (Input.GetKey(forward)) input.z += 1;
            if (Input.GetKey(backward)) input.z -= 1;
            if (gemini)
            {
                if (Input.GetKey(right)) input.x -= 1;
                if (Input.GetKey(left)) input.x += 1;
            }
            else
            {
                if (Input.GetKey(right)) input.x += 1;
                if (Input.GetKey(left)) input.x -= 1;
            }

            moveDirection = transform.TransformDirection(input.normalized * speed);

            // Jump
            if (Input.GetKey(KeyCode.Space))
                moveDirection.y = jumpSpeed;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);

        // Bounce effect when moving
        HandleMeshBounce();
    }

    private void HandleMeshBounce()
    {
        if (meshChild == null)
            return;

        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        float moveSpeed = horizontalVelocity.magnitude;

        if (moveSpeed > 0.1f && controller.isGrounded)
        {
            bounceTimer += Time.deltaTime * bounceFrequency;
            float bounceOffset = Mathf.Sin(bounceTimer) * bounceAmplitude;
            meshChild.localPosition = meshOriginalLocalPos + Vector3.up * bounceOffset;
        }
        else
        {
            // Reset bounce when idle or in air
            bounceTimer = 0f;
            meshChild.localPosition = Vector3.Lerp(
                meshChild.localPosition,
                meshOriginalLocalPos,
                Time.deltaTime * 10f
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Mud")) speed = 3f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Mud")) speed = 6f;
    }
}
