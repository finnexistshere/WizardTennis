using UnityEngine;
using UnityEngine.Animations.Rigging;

public class TwoHandIKController : MonoBehaviour
{
    [Header("Controller & Targets")]
    [SerializeField] private Transform twoHandController; // Main controller
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;

    [Header("Extras")]
    [SerializeField] private Transform ball; // Ball
    [SerializeField] private RigBuilder rig;

    private void LateUpdate()
    {
        // Move controller to ball
        twoHandController.position = ball.position;

        rig.Build(); // Apply transformations to rig
    }
}
