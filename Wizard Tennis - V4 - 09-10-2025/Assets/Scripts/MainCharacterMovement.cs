using UnityEngine;

public class MainCharacterMovement : MonoBehaviour
{
    public float speed = 6.0f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    private Vector3 moveDirection = Vector3.zero;

    public TennisAI TennisAi;

    // Store current pickup debuff value and spell name
    private float debuffValue = 0f;
    private string spellName = "";

    void Update()
    {
        CharacterController controller = GetComponent<CharacterController>();
        // Basic movement, this can be fleshed out with a Jump buffer etc.
        if (controller.isGrounded)
        {
            moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            moveDirection = transform.TransformDirection(moveDirection);
            moveDirection *= speed;

            if (Input.GetButton("Jump"))
                moveDirection.y = jumpSpeed;
        }

        moveDirection.y -= gravity * Time.deltaTime;
        controller.Move(moveDirection * Time.deltaTime);
    }

    // Call this method from the pickup effect to set the debuff value and spell name
    public void SetDebuff(float value, string spell)
    {
        debuffValue = value;
        spellName = spell;
        Debug.Log($"Debuff stored: {spell} ({value})");

        UIManager.Instance?.UpdateSpellStatus(spellName, debuffValue);
    }
}
