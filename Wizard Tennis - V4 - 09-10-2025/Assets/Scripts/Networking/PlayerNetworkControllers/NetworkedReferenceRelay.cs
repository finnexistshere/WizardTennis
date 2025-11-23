using UnityEngine;
using Unity.Netcode;

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
    public AudioSource audioSource;

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

    public void ApplyTo(NetworkedBall ball)
    {
        if (ball == null)
        {
            Debug.LogWarning("[Relay] Tried to apply to a null NetworkedBall!");
            return;
        }

        // Assign shared references
        ball.spellEffects = spellEffects;
        ball.scoreManager = scoreManager;
        ball.audioSource = audioSource;

        // Determine which player this is (host = client 0, client = client 1)
        ulong ownerId = ball.OwnerClientId;
        bool isHostPlayer = (ownerId == 0);

        // Assign player-specific references
        if (isHostPlayer)
        {
            ball.aimTarget = hostAimTarget;
            ball.OppIKRig = hostIKRig;
            ball.opponent = clientOpponent; // Host's opponent is the client
            ball.servingBarriers = hostBarriers;
            Debug.Log($"[Relay] Applied HOST references to NetworkedBall (OwnerClientId: {ownerId})");
        }
        else
        {
            ball.aimTarget = clientAimTarget;
            ball.OppIKRig = clientIKRig;
            ball.opponent = hostOpponent; // Client's opponent is the host
            ball.servingBarriers = clientBarriers;
            Debug.Log($"[Relay] Applied CLIENT references to NetworkedBall (OwnerClientId: {ownerId})");
        }
    }
}