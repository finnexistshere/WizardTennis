using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TwoHandIKController_Opponent : MonoBehaviour
{
    [Header("Controller & Targets")]
    [SerializeField] private Transform twoHandController;
    [SerializeField] private Transform opponentRoot;

    [Header("Extras")]
    [SerializeField] private Transform ball;
    [SerializeField] private RigBuilder rig;

    [Header("Settings")]
    [SerializeField] private float sideOffsetMax = 0.8f;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float forwardOffset = 0.5f;
    [SerializeField] private float followSpeed = 5f;
    [SerializeField] private float swingDistance = 1f;
    [SerializeField] private float swingSharpness = 0.1f;
    [SerializeField] private float followThroughAmount = 0.6f;
    [SerializeField] private float swingTriggerDistance = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private float swingTimer;
    private const float swingDuration = 0.25f;
    private int swingDirection = 0;
    private bool hasSwung = false;

    public void AssignBall(Transform newBall)
    {
        ball = newBall;
    }

    private void LateUpdate()
    {
        if (!twoHandController || !opponentRoot || !ball) return;

        // Ball position relative to opponent root
        Vector3 localBallPos = opponentRoot.InverseTransformPoint(ball.position);

        // Forward along opponent's negative Z (since opponent is mirrored)
        float forwardZ = -localBallPos.z;
        float horizontalDir = Mathf.Clamp(localBallPos.x, -1f, 1f);

        // Trigger swing if ball is close and hasn't swung yet
        if (forwardZ < swingTriggerDistance && !hasSwung)
        {
            swingDirection = Mathf.RoundToInt(Mathf.Sign(horizontalDir));
            swingTimer = swingDuration;
            hasSwung = true;

            if (debugLogs)
                Debug.Log($"{name}: Opponent swing triggered! dir={swingDirection}, forwardZ={forwardZ}");
        }

        // Reset swing if ball moves back past swing distance
        if (forwardZ > swingDistance)
            hasSwung = false;

        // Side offset calculation
        float sideOffset;
        if (swingTimer > 0f)
        {
            float t = 1f - (swingTimer / swingDuration);
            sideOffset = Mathf.Lerp(horizontalDir * sideOffsetMax,
                                    -swingDirection * sideOffsetMax * followThroughAmount, t);
            swingTimer -= Time.deltaTime;
        }
        else
        {
            float distanceFactor = Mathf.Clamp01(forwardZ / swingDistance);
            float swingFactor = Mathf.Pow(distanceFactor, swingSharpness);
            sideOffset = horizontalDir * sideOffsetMax * swingFactor;
        }

        // Final target position
        Vector3 targetPos = opponentRoot.position
                            + opponentRoot.right * sideOffset
                            - opponentRoot.forward * forwardOffset
                            + Vector3.up * heightOffset;

        twoHandController.position = Vector3.Lerp(twoHandController.position, targetPos, Time.deltaTime * followSpeed);

        if (rig) rig.Build();

        if (debugLogs)
            Debug.Log($"{name}: Hand target={targetPos}, ball={ball.position}, forwardZ={forwardZ}");
    }

    private void OnDrawGizmos()
    {
        if (!opponentRoot) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(opponentRoot.position, swingDistance);
        Gizmos.DrawLine(opponentRoot.position, opponentRoot.position - opponentRoot.forward * swingDistance);
    }
}
