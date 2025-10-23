using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TwoHandIKController : MonoBehaviour
{
    [Header("Controller & Targets")]
    [SerializeField] private Transform twoHandController; // Main controller
    [SerializeField] private Transform playerPos;

    [Header("Extras")]
    [SerializeField] private Transform ball; // Ball
    [SerializeField] private RigBuilder rig;

    private void LateUpdate()
    {
        Vector3 targetPosition = ball.position;
        targetPosition.y = twoHandController.position.y + 0.5f;
        Vector3 pos = Vector3.MoveTowards(playerPos.position, targetPosition, 0.3f);
        //pos.y = twoHandController.position.y + 0.5f;

        twoHandController.position = pos;

        rig.Build(); // Apply transformations to rig
    }
}
