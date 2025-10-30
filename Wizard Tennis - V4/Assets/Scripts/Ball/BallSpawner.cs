using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    public Rigidbody ball; // assign the ball in the Inspector
    public Transform spawnPoint; // optional: set a spawn/serve position

    public void ResetBall()
    {
        if (ball == null)
        {
            Debug.LogWarning("Ball not assigned to BallSpawner!");
            return;
        }

        // Reset physics
        ball.velocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;

        // Reset position & rotation
        if (spawnPoint != null)
        {
            ball.transform.position = spawnPoint.position;
            ball.transform.rotation = spawnPoint.rotation;
        }
        else
        {
            // fallback: reset to spawner's position
            ball.transform.position = transform.position;
            ball.transform.rotation = Quaternion.identity;
        }
    }
}
