using UnityEngine;
using Unity.Netcode;

public class PlayerReferenceRelay : MonoBehaviour
{
    public Transform aimTarget;
    public TwoHandIKController_Opponent oppIKRig;
    public SpellEffects spellEffects;
    public GameObject opponent;
    public GameObject spellBarriers;
    public ScoreManager scoreManager;

    private static PlayerReferenceRelay instance;
    public static PlayerReferenceRelay Instance => instance;

    private void Awake()
    {
        // Ensure only one exists
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    // This is what your networked player or ball calls after spawning
    public void ApplyTo(NetworkedBall target)
    {
        target.aimTarget = aimTarget;
        target.OppIKRig = oppIKRig;
        target.SpellEffects = spellEffects;
        target.Opponent = opponent;
        target.servingBarriers = spellBarriers;
        target.scoreManager = scoreManager;
    }
}
