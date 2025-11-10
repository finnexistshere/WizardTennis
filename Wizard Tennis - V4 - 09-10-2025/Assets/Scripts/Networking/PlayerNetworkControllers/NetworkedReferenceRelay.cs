using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerReferenceRelay : MonoBehaviour
{
    [Header("Global Scene References")]
    public SpellEffects spellEffects;
    public ScoreManager scoreManager;

    [Header("Player-Specific References")]
    public Transform hostAimTarget;
    public Transform clientAimTarget;

    public TwoHandIKController_Opponent hostIKRig;
    public TwoHandIKController_Opponent clientIKRig;

    public GameObject hostOpponent;
    public GameObject clientOpponent;

    public GameObject hostBarriers;
    public GameObject clientBarriers;

    [Header("Shared Audio")]
    public AudioSource audioSource; // assign in inspector

    private static PlayerReferenceRelay instance;
    public static PlayerReferenceRelay Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    // Core method: applies correct references based on who owns this NetworkedBall
    public void ApplyTo(NetworkedBall ball)
    {
        // Defensive guard
        if (ball == null)
        {
            Debug.LogWarning("[Relay] Tried to apply to a null NetworkedBall!");
            return;
        }

        ulong ownerId = ball.OwnerClientId;
        bool isHost = ownerId == NetworkManager.Singleton.LocalClientId && NetworkManager.Singleton.IsHost;

        // Assign shared references
        ball.SpellEffects = spellEffects;
        ball.scoreManagerComponent = scoreManager;
        ball.audioSourceComponent = audioSource;

        // Assign per-player references
        if (NetworkManager.Singleton.IsHost)
        {
            // Host’s local player
            ball.aimTarget = hostAimTarget;
            ball.OppIKRig = hostIKRig;
            ball.Opponent = hostOpponent;
            ball.servingBarriers = hostBarriers;
        }
        else
        {
            // Client’s local player
            ball.aimTarget = clientAimTarget;
            ball.OppIKRig = clientIKRig;
            ball.Opponent = clientOpponent;
            ball.servingBarriers = clientBarriers;

        }

        Debug.Log($"[Relay] Applied references to NetworkedBall ({(isHost ? "Host" : "Client")})");
    }
}
