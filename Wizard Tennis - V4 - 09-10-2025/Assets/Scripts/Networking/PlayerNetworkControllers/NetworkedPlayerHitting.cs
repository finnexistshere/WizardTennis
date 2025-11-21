using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class NetworkedPlayerHitting : NetworkBehaviour
{
    [Header("References")]
    public Transform aimTarget;
    public Transform ballSpawnPoint;
    public SpellEffects SpellEffects;
    public CollisionTrackerBall CollisionTracker;

    [Header("Hit Settings")]
    public float strength = 25f;
    public float ogUpForce = 11f;
    private float upForce = 11f;

    private bool serving = true;
    private bool nearBall = false;
    private NetworkBall ball;

    public override void OnNetworkSpawn()
    {
        if (IsOwner && PlayerReferenceRelay.Instance != null)
        {
            PlayerReferenceRelay.Instance.ApplyTo(this);
        }
        upForce = ogUpForce;
    }

    private void Update()
    {
        if (!IsOwner) return;

        TryFindBall();

        if (Input.GetKeyDown(KeyCode.E) && serving)
        {
            // Request server to spawn and serve automatically
            ServeRequestServerRpc();
        }
    }

    private void TryFindBall()
    {
        if (ball != null) return;

        GameObject obj = GameObject.FindWithTag("Ball");
        if (obj != null)
            ball = obj.GetComponent<NetworkBall>();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServeRequestServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only server should spawn or serve
        if (!IsServer) return;

        // Spawn ball if it doesn't exist
        if (GameObject.FindWithTag("Ball") == null)
        {
            GameObject ballObj = Instantiate(BallSpawnManager.Instance.ballPrefab,
                                             ballSpawnPoint.position + Vector3.up * 0.5f,
                                             ballSpawnPoint.rotation);
            NetworkObject netObj = ballObj.GetComponent<NetworkObject>();
            netObj.Spawn(true);

            ball = ballObj.GetComponent<NetworkBall>();
        }
        else
        {
            ball = GameObject.FindWithTag("Ball").GetComponent<NetworkBall>();
        }

        // Apply serve velocity immediately
        Vector3 serveDir = aimTarget.position - transform.position;
        serveDir.y = 0; // horizontal
        serveDir.Normalize();

        Vector3 finalDir = serveDir * (strength / 2f) + Vector3.up * upForce;

        ball.HitBallServerRpc(finalDir, finalDir.magnitude, upForce, rpcParams.Receive.SenderClientId);

        serving = false;

        Debug.Log("[PlayerHitting] Server spawned and served the ball!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner || !other.CompareTag("Ball")) return;

        nearBall = true;
        ball = other.GetComponent<NetworkBall>();

        if (!serving)
        {
            Vector3 hitDir = aimTarget.position - transform.position;
            PlayLocalHitFeedback(other.transform.position);
            ball.HitBallServerRpc(hitDir, strength, upForce, OwnerClientId);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball"))
            nearBall = false;
    }

    private void PlayLocalHitFeedback(Vector3 pos)
    {
        if (SpellEffects != null && SpellEffects.plrHitSpell)
            SpellEffects.castSpell();
    }
}
