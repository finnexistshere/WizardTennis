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
            bool leftHanded = OptionsManager.Instance.leftHandedMode;

            // Movement keys are opposite of spell keys
            KeyCode forward = leftHanded ? KeyCode.UpArrow : KeyCode.W;
            KeyCode backward = leftHanded ? KeyCode.DownArrow : KeyCode.S;
            KeyCode left = leftHanded ? KeyCode.LeftArrow : KeyCode.A;
            KeyCode right = leftHanded ? KeyCode.RightArrow : KeyCode.D;

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
