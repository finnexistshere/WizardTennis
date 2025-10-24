using UnityEngine;

public class MainCharacterMovement : MonoBehaviour
{
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    private Vector3 moveDirection = Vector3.zero;

    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
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
            if (Input.GetKey(right)) input.x += 1;
            if (Input.GetKey(left)) input.x -= 1;

            moveDirection = transform.TransformDirection(input.normalized * speed);

            // Jump is always Space
            if (Input.GetKey(KeyCode.Space))
                moveDirection.y = jumpSpeed;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }
}
