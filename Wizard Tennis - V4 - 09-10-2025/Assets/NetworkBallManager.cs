using UnityEngine;
using Unity.Netcode;

public class BallSpawnManager : NetworkBehaviour
{
    public static BallSpawnManager Instance;

    public GameObject ballPrefab;

    private void Awake() => Instance = this;

    [ServerRpc(RequireOwnership = false)]
    public void RequestSpawnBallServerRpc(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        if (GameObject.FindWithTag("Ball") != null) return;

        GameObject ball = Instantiate(ballPrefab, pos, rot);
        ball.GetComponent<NetworkObject>().Spawn(true);

        Debug.Log("[BallSpawnManager] Spawned ball at server.");
    }
}
