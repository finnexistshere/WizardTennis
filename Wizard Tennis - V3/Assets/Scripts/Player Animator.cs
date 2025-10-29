using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TwoHandIKController : MonoBehaviour
{
    [Header("Controller & Targets")]
    [SerializeField] private Transform twoHandController;
    [SerializeField] private Transform playerPos;

    [Header("Extras")]
    [SerializeField] private Transform ball;
    [SerializeField] private RigBuilder rig;

    [Header("Settings")]
    [SerializeField] private float sideOffsetMax = 0.8f;     // Max sideways reach
    [SerializeField] private float heightOffset = 0.5f;      // Hand height
    [SerializeField] private float forwardOffset = 0.5f;     // Constant forward reach
    [SerializeField] private float followSpeed = 1f;         // Smooth follow speed
    [SerializeField] private float swingDistance = 1f;       // Distance where swing starts
    [SerializeField] private float swingSharpness = 0.1f;      // How suddenly swing begins
    [SerializeField] private float followThroughAmount = 0.6f; // How far across to swing

    private float swingTimer;
    private const float swingDuration = 0.25f;
    private int swingDirection = 0;

    private void LateUpdate()
    {
        if (!ball || !playerPos || !twoHandController) return;

        Vector3 toBall = (ball.position - playerPos.position).normalized;

        bool facingBall = Vector3.Dot(playerPos.forward, toBall) > 0f;

        Vector3 localBallPos = playerPos.InverseTransformPoint(ball.position);

        if (!facingBall)
        {
            localBallPos.z *= -1f;
            localBallPos.x *= -1f;
        }

        float horizontalDir = Mathf.Clamp(localBallPos.x, -1f, 1f);
        float forwardZ = Mathf.Max(localBallPos.z, 0.001f);

        float distanceFactor = Mathf.Clamp01(forwardZ / swingDistance);
        float swingFactor = Mathf.Pow(distanceFactor, swingSharpness);

        if (forwardZ < 0.3f && swingTimer <= 0f)
        {
            swingDirection = Mathf.RoundToInt(Mathf.Sign(horizontalDir));
            swingTimer = swingDuration;
        }

        float sideOffset;
        if (swingTimer > 0f)
        {
            float t = 1f - (swingTimer / swingDuration);
            sideOffset = Mathf.Lerp(horizontalDir * sideOffsetMax, -swingDirection * sideOffsetMax * followThroughAmount, t);
            swingTimer -= Time.deltaTime;
        }
        else
        {
            sideOffset = horizontalDir * sideOffsetMax * swingFactor;
        }

        Vector3 targetPos = playerPos.position
                            + playerPos.right * sideOffset
                            + playerPos.forward * forwardOffset
                            + Vector3.up * heightOffset;

        twoHandController.position = Vector3.Lerp(twoHandController.position, targetPos, Time.deltaTime * followSpeed);

        if (rig) rig.Build();
    }
}
