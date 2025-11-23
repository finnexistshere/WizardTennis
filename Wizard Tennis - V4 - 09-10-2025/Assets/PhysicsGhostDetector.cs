using UnityEngine;

public class PhysicsGhostDetector : MonoBehaviour
{
    public float radius = 0.5f; // size of the ball
    public LayerMask allLayers;

    void Update()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, allLayers, QueryTriggerInteraction.Collide);

        if (hits.Length > 0)
        {
            foreach (var h in hits)
            {
                Debug.Log(
                    $"[GHOST-DETECTOR] Hit collider: {h.name} | " +
                    $"InstanceID: {h.gameObject.GetInstanceID()} | " +
                    $"Layer: {LayerMask.LayerToName(h.gameObject.layer)} | " +
                    $"Active: {h.gameObject.activeInHierarchy}"
                );
            }
        }
    }
}
