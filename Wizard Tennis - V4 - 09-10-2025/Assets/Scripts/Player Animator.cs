using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;

public class TwoHandIKController : MonoBehaviour
{
    [Header("Controller & Targets")]
    [SerializeField] public Transform twoHandController;
    [SerializeField] private Transform playerPos;

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

    [Header("IK Nudge")]
    [SerializeField] private float nudgeDamping = 12f;

    private Coroutine nudgeRoutine;
    private bool isNudging = false;
    private Vector3 currentNudge;
    private Vector3 targetNudge;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private float swingTimer;
    private const float swingDuration = 0.25f;
    private int swingDirection = 0;
    private bool hasSwung = false;

    // Robustness
    private bool rigInitialized = false;
    private Coroutine findBallRoutine;

    /* -------------------- BALL ASSIGNMENT -------------------- */

    public void AssignBall(Transform newBall)
    {
        if (newBall == null || ball == newBall)
            return;

        ball = newBall;
        rigInitialized = false;

        if (debugLogs)
            Debug.Log($"{name}: Ball assigned -> {ball.name}");
    }

    private void Start()
    {
        // Start searching immediately in case ball spawns late
        StartFindingBall();
    }

    private void StartFindingBall()
    {
        if (findBallRoutine != null)
            StopCoroutine(findBallRoutine);

        findBallRoutine = StartCoroutine(FindBallRepeatedly());
    }

    private IEnumerator FindBallRepeatedly()
    {
        while (ball == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Ball");
            if (found != null)
            {
                AssignBall(found.transform);
                break;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    /// <summary>
    /// Called when the ball is destroyed/reset. Clears reference and starts searching.
    /// </summary>
    public void ResetBallReference()
    {
        ball = null;
        rigInitialized = false;

        if (debugLogs)
            Debug.Log($"{name}: Ball reference reset, starting search...");

        StartFindingBall();
    }

    /* -------------------- IK UPDATE -------------------- */

    private void LateUpdate()
    {
        if (!twoHandController || !playerPos)
            return;

        // Self-heal if ball was destroyed or not yet spawned
        if (ball == null)
        {
            if (findBallRoutine == null)
                StartFindingBall();
            return;
        }

        // Build rig once per valid assignment
        if (rig != null)
        {
            rig.Build();
        }

        // Ball position relative to player
        Vector3 localBallPos = playerPos.InverseTransformPoint(ball.position);

        float forwardZ = localBallPos.z;
        float horizontalDir = Mathf.Clamp(localBallPos.x, -1f, 1f);

        // Trigger swing
        if (forwardZ < swingTriggerDistance && !hasSwung)
        {
            swingDirection = Mathf.RoundToInt(Mathf.Sign(horizontalDir));
            swingTimer = swingDuration;
            hasSwung = true;

            if (debugLogs)
                Debug.Log($"{name}: Player swing triggered dir={swingDirection}");
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
            playerPos.position
            + playerPos.right * sideOffset
            + playerPos.forward * forwardOffset
            + Vector3.up * heightOffset;

        currentNudge = Vector3.Lerp(
            currentNudge,
            targetNudge,
            Time.deltaTime * nudgeDamping
        );

        twoHandController.position =
            Vector3.Lerp(
                twoHandController.position,
                targetPos + currentNudge,
                Time.deltaTime * followSpeed
            );

        if (debugLogs)
            Debug.Log($"{name}: Hand target={targetPos}, ball={ball.position}");
    }

    /* -------------------- DEBUG -------------------- */

    private void OnDrawGizmos()
    {
        if (!playerPos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(playerPos.position, swingDistance);
        Gizmos.DrawLine(
            playerPos.position,
            playerPos.position + playerPos.forward * swingDistance
        );
    }

    public void Nudge(Vector3 worldOffset, float duration)
    {
        if (nudgeRoutine != null)
            StopCoroutine(nudgeRoutine);

        nudgeRoutine = StartCoroutine(NudgeRoutine(worldOffset, duration));
    }

    private IEnumerator NudgeRoutine(Vector3 offset, float duration)
    {
        isNudging = true;

        targetNudge = offset;

        yield return new WaitForSeconds(duration);

        // Release back to ball tracking
        targetNudge = Vector3.zero;

        // Wait until we've visually settled back
        while (currentNudge.sqrMagnitude > 0.0001f)
            yield return null;

        currentNudge = Vector3.zero;
        isNudging = false;
    }
}
