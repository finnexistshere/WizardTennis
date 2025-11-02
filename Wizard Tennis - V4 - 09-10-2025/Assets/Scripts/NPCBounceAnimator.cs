using UnityEngine;

public class NPCBounceAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform characterMesh; // The visual child to bounce

    [Header("Bounce Settings")]
    [SerializeField] private float bounceAmplitude = 0.1f; // How high the bounce is
    [SerializeField] private float bounceSpeed = 6f;       // How fast the bounce cycles
    [SerializeField] private float returnSpeed = 5f;       // How quickly it returns to rest when stopped

    [HideInInspector] public bool isMoving = false;

    private float bounceTimer = 0f;
    private Vector3 basePosition;

    private void Start()
    {
        if (characterMesh == null)
        {
            Debug.LogWarning($"{name}: NPCBounceAnimator missing mesh reference!");
            return;
        }

        basePosition = characterMesh.localPosition;
    }

    private void Update()
    {
        if (characterMesh == null) return;

        if (isMoving)
        {
            bounceTimer += Time.deltaTime * bounceSpeed;
            float offsetY = Mathf.Sin(bounceTimer) * bounceAmplitude;
            characterMesh.localPosition = basePosition + new Vector3(0, offsetY, 0);
        }
        else
        {
            // Smoothly return to base position when idle
            characterMesh.localPosition = Vector3.Lerp(
                characterMesh.localPosition,
                basePosition,
                Time.deltaTime * returnSpeed
            );
        }
    }
}
