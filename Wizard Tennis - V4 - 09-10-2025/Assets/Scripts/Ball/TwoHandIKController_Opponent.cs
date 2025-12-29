using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;

public class TwoHandIKController_Opponent : MonoBehaviour
{
    [Header("Controller & Targets")]
    [SerializeField] private Transform twoHandController;
    [SerializeField] private Transform opponentRoot;

    [Header("Extras")]
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

    private Transform ball;
    private bool rigInitialized = false;

    private float swingTimer;
    private const float swingDuration = 0.25f;
    private int swingDirection = 0;
    private bool hasSwung = false;

    private Coroutine assignBallRoutine;

    public void StartAssignBallCoroutine()
    {
        if (assignBallRoutine != null)
            StopCoroutine(assignBallRoutine);
        assignBallRoutine = StartCoroutine(AssignBallRepeatedly());
    }

    public void AssignBall(Transform newBall)
    {
        if (ball == newBall || newBall == null)
            return;

        ball = newBall;
        rigInitialized = false;

        if (debugLogs)
            Debug.Log($"{name}: Ball assigned -> {ball.name}");
    }

    private IEnumerator AssignBallRepeatedly()
    {
        while (ball == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Ball");
            if (found)
            {
                AssignBall(found.transform);
                Debug.Log($"[IK Controller] found {found}");
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void LateUpdate()
    {
        if (!twoHandController || !opponentRoot || ball == null)
            return;

        if (!rigInitialized && rig != null)
        {
            rig.Build();
            rigInitialized = true;
        }

        if (ball == null)
        {
            GameObject foundBall = GameObject.FindGameObjectWithTag("Ball");
            if (foundBall != null)
            {
                AssignBall(foundBall.transform);
            }
            return;
        }

        Vector3 localBallPos = opponentRoot.InverseTransformPoint(ball.position);

        float forwardZ = -localBallPos.z;
        float horizontalDir = Mathf.Clamp(localBallPos.x, -1f, 1f);

        if (forwardZ < swingTriggerDistance && !hasSwung)
        {
            swingDirection = Mathf.RoundToInt(Mathf.Sign(horizontalDir));
            swingTimer = swingDuration;
            hasSwung = true;
        }

        if (forwardZ > swingDistance)
            hasSwung = false;

        float sideOffset;
        if (swingTimer > 0f)
        {
            float t = 1f - (swingTimer / swingDuration);
            sideOffset = Mathf.Lerp(
                horizontalDir * sideOffsetMax,
                -swingDirection * sideOffsetMax * followThroughAmount,
                t
            );
            swingTimer -= Time.deltaTime;
        }
        else
        {
            float distanceFactor = Mathf.Clamp01(forwardZ / swingDistance);
            sideOffset = horizontalDir * sideOffsetMax * Mathf.Pow(distanceFactor, swingSharpness);
        }

        Vector3 targetPos =
            opponentRoot.position
            + opponentRoot.right * sideOffset
            - opponentRoot.forward * forwardOffset
            + Vector3.up * heightOffset;

        twoHandController.position = Vector3.Lerp(twoHandController.position, targetPos, Time.deltaTime * followSpeed);
    }
}
