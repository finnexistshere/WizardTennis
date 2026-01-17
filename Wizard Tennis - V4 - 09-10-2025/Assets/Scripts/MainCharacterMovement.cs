using UnityEngine;
using Unity.Netcode;

public class MainCharacterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    [Header("Bounce Settings")]
    public Transform meshChild;          // Assign the character mesh here
    public float bounceAmplitude = 0.05f; // How high it bounces
    public float bounceFrequency = 8.0f;  // How fast it bounces

    [Header("Dust Effect")]
    public ParticleSystem dustParticles;  // Assign particle system here
    public float dustSpeedThreshold = 0.1f; // Minimum speed to trigger dust
    public float dustLagAmount = 0.2f;     // How much the dust lags behind (0-1)

    private Vector3 moveDirection = Vector3.zero;
    public CharacterController controller;
    public Rigidbody rb;
    private float bounceTimer = 0f;
    private Vector3 meshOriginalLocalPos;

    public bool gemini = false;

    // ADD THESE for multiplayer knockback support
    [HideInInspector] public bool inputDisabled = false;
    private NetworkObject netObj; // Cache the NetworkObject if present

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        if (meshChild != null)
            meshOriginalLocalPos = meshChild.localPosition;

        // Check if we're in multiplayer
        netObj = GetComponent<NetworkObject>();
    }

    void Update()
    {
        //In multiplayer, only run on owner
        if (netObj != null && !netObj.IsOwner)
            return;

        // Skip input if disabled (for knockback)
        if (inputDisabled)
        {
            // DON'T apply moveDirection here - it contains knockback velocity
            // Only apply gravity separately
            Vector3 gravityOnly = Vector3.zero;
            gravityOnly.y = moveDirection.y - (gravity * Time.deltaTime);
            controller.Move(gravityOnly * Time.deltaTime);

            // Keep the y component for gravity continuity
            moveDirection.y = gravityOnly.y;

            HandleMeshBounce();
            HandleDustEffect();
            return;
        }

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

            if (UnityEngine.Input.GetKey(forward)) input.z += 1;
            if (UnityEngine.Input.GetKey(backward)) input.z -= 1;
            if (gemini)
            {
                if (UnityEngine.Input.GetKey(right)) input.x -= 1;
                if (UnityEngine.Input.GetKey(left)) input.x += 1;
            }
            else
            {
                if (UnityEngine.Input.GetKey(right)) input.x += 1;
                if (UnityEngine.Input.GetKey(left)) input.x -= 1;
            }

            moveDirection = transform.TransformDirection(input.normalized * speed);

            // Jump
            if (UnityEngine.Input.GetKey(KeyCode.Space))
                moveDirection.y = jumpSpeed;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);

        // Bounce effect when moving
        HandleMeshBounce();

        // Dust effect when moving
        HandleDustEffect();
    }

    /// <summary>
    /// Clears moveDirection - called after knockback to prevent lingering velocity
    /// </summary>
    public void ClearMoveDirection()
    {
        moveDirection = Vector3.zero;
        Debug.Log($"[MainCharacterMovement] Cleared moveDirection for {gameObject.name}");
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

    private void HandleDustEffect()
    {
        if (dustParticles == null)
            return;

        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        float moveSpeed = horizontalVelocity.magnitude;

        // Play dust when moving on ground
        if (moveSpeed > dustSpeedThreshold && controller.isGrounded)
        {
            if (!dustParticles.isPlaying)
                dustParticles.Play();

            // Make dust lag behind movement direction
            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 oppositeDirection = -horizontalVelocity.normalized;
                Vector3 targetPosition = transform.position + (oppositeDirection * dustLagAmount);
                dustParticles.transform.position = Vector3.Lerp(
                    dustParticles.transform.position,
                    targetPosition,
                    Time.deltaTime * 10f
                );
            }
        }
        else
        {
            if (dustParticles.isPlaying)
                dustParticles.Stop();
        }
    }

    // These two bits of code are for forcing this script to reposition the client during a round reset

    // Is this a bad way of doing this?

    // Yes!

    // But I'm beyond giving a shit
    public void ForceMovementRefresh()
    {
        // Reset velocity so no lingering fall/gravity momentum after teleport
        moveDirection = Vector3.zero;

        // Reset bounce so mesh doesn't snap
        bounceTimer = 0f;

        // Force CharacterController to update grounding state
        if (controller != null)
            controller.Move(Vector3.zero);
    }

    public void Nudge(Vector3 amount)
    {
        if (controller != null)
            controller.Move(amount);
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