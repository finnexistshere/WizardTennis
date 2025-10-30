using UnityEngine;

public class TestIKMove : MonoBehaviour
{
    [SerializeField] private Transform twoHandController;
    [SerializeField] private Transform playerPos;
    [SerializeField] private Transform ball;

    [SerializeField] private float sideOffset = 0.5f;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float forwardOffset = 0.5f;
    [SerializeField] private float followSpeed = 5f;

    private Vector3 velocity;

    public void AssignBall(Transform newBall)
    {
        ball = newBall;
    }

    void LateUpdate()
    {
        if (!twoHandController || !playerPos || !ball) return;

        // Move the hand to a point relative to playerPos + ball
        Vector3 target = playerPos.position
                         + playerPos.right * sideOffset
                         + playerPos.forward * forwardOffset
                         + Vector3.up * heightOffset;

        // Move smoothly
        twoHandController.position = Vector3.SmoothDamp(twoHandController.position, target, ref velocity, 1f / followSpeed);
    }
}
