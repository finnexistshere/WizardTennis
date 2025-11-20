using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class MainCharacterMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    [Header("Bounce Settings")]
    public Transform meshChild;
    public float bounceAmplitude = 0.05f;
    public float bounceFrequency = 8.0f;

    [Header("Prediction Settings")]
    public float correctionSpeed = 10f; // how fast client snaps to server position

    private CharacterController controller;
    private Vector3 meshOriginalLocalPos;
    private float bounceTimer = 0f;

    // Client-side predicted position
    private Vector3 predictedPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (meshChild != null)
            meshOriginalLocalPos = meshChild.localPosition;

        predictedPosition = transform.position;
    }

    private void Update()
    {
        if (!IsOwner) return;

        Vector3 input = GetInputVector();
        bool jump = Input.GetKey(KeyCode.Space);

        // Apply client-side prediction
        Vector3 moveDir = transform.TransformDirection(input.normalized * speed);
        if (controller.isGrounded && jump)
            moveDir.y = jumpSpeed;
        moveDir.y -= gravity * Time.deltaTime;

        controller.Move(moveDir * Time.deltaTime);

        predictedPosition = transform.position; // store predicted position

        HandleMeshBounce(moveDir);

        // Send input to server
        SendInputServerRpc(input, jump);
    }

    private Vector3 GetInputVector()
    {
        Vector3 input = Vector3.zero;
        KeyCode forward = KeyCode.W;
        KeyCode backward = KeyCode.S;
        KeyCode left = KeyCode.A;
        KeyCode right = KeyCode.D;

        if (OptionsManager.Instance != null && OptionsManager.Instance.leftHandedMode)
        {
            forward = KeyCode.UpArrow;
            backward = KeyCode.DownArrow;
            left = KeyCode.LeftArrow;
            right = KeyCode.RightArrow;
        }

        if (Input.GetKey(forward)) input.z += 1;
        if (Input.GetKey(backward)) input.z -= 1;
        if (Input.GetKey(right)) input.x += 1;
        if (Input.GetKey(left)) input.x -= 1;

        return input;
    }

    [ServerRpc]
    private void SendInputServerRpc(Vector3 input, bool jump)
    {
        if (controller == null) return;

        // Compute movement on server
        Vector3 moveDir = transform.TransformDirection(input.normalized * speed);
        if (controller.isGrounded && jump)
            moveDir.y = jumpSpeed;
        moveDir.y -= gravity * Time.deltaTime;

        controller.Move(moveDir * Time.deltaTime);

        // After server moves, update clients
        UpdateClientPositionClientRpc(transform.position);
    }

    [ClientRpc]
    private void UpdateClientPositionClientRpc(Vector3 serverPosition)
    {
        if (!IsOwner) return; // only correct the owning client

        // Smoothly correct client prediction
        transform.position = Vector3.Lerp(predictedPosition, serverPosition, Time.deltaTime * correctionSpeed);
        predictedPosition = transform.position;
    }

    private void HandleMeshBounce(Vector3 moveVelocity)
    {
        if (meshChild == null) return;

        Vector3 horizontalVelocity = new Vector3(moveVelocity.x, 0, moveVelocity.z);
        float moveSpeed = horizontalVelocity.magnitude;

        if (moveSpeed > 0.1f && controller.isGrounded)
        {
            bounceTimer += Time.deltaTime * bounceFrequency;
            float bounceOffset = Mathf.Sin(bounceTimer) * bounceAmplitude;
            meshChild.localPosition = meshOriginalLocalPos + Vector3.up * bounceOffset;
        }
        else
        {
            bounceTimer = 0f;
            meshChild.localPosition = Vector3.Lerp(
                meshChild.localPosition,
                meshOriginalLocalPos,
                Time.deltaTime * 10f
            );
        }
    }
}
