using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerReferenceRelay : MonoBehaviour
{
    public SpellEffects spellEffects;
    public ScoreManager scoreManager;

    // These are name-based and found automatically
    public Transform hostAimTarget;
    public Transform clientAimTarget;

    public TwoHandIKController_Opponent hostIKRig;
    public TwoHandIKController_Opponent clientIKRig;

    public GameObject hostOpponent;
    public GameObject clientOpponent;

    public GameObject hostBarriers;
    public GameObject clientBarriers;

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

        // AUTO-FIND ALL NEEDED OBJECTS
        hostAimTarget = GameObject.Find("PlayerAim")?.transform;
        clientAimTarget = GameObject.Find("OppAim")?.transform;

        hostIKRig = GameObject.Find("PlayerIKRig")?.GetComponent<TwoHandIKController_Opponent>();
        clientIKRig = GameObject.Find("OppIKRig")?.GetComponent<TwoHandIKController_Opponent>();

        hostOpponent = GameObject.Find("PlayerOpponent");
        clientOpponent = GameObject.Find("OppOpponent");

        hostBarriers = GameObject.Find("PlayerBarriers");
        clientBarriers = GameObject.Find("OppBarriers");

        Debug.Log("[Relay] Auto-located all reference objects.");
    }

    // CORE: Apply references to a newly spawned player
    public void ApplyTo(NetworkedPlayerHitting player)
    {
        if (player == null)
        {
            Debug.LogWarning("[Relay] Tried to apply to a NULL player!");
            return;
        }

        ulong ownerId = player.OwnerClientId;
        bool isHostPlayer = (NetworkManager.Singleton.IsHost &&
                             ownerId == NetworkManager.Singleton.LocalClientId);

        // Shared references
        // player.spellEffects = spellEffects;
        //  player.scoreManager = scoreManager;
        //  player.audioSource = audioSource;

        // Host player setup
        if (isHostPlayer)
        {
            player.aimTarget = hostAimTarget;
           // player.OppIKRig = hostIKRig;
           // player.opponent = hostOpponent;
          //  player.servingBarriers = hostBarriers;

            Debug.Log("[Relay] Assigned HOST references to player with owner ID: " + ownerId);
        }
        else // Client player setup
        {
            player.aimTarget = clientAimTarget;
         //   player.OppIKRig = clientIKRig;
          //  player.opponent = clientOpponent;
          //  player.servingBarriers = clientBarriers;

            Debug.Log("[Relay] Assigned CLIENT references to player with owner ID: " + ownerId);
        }
    }
}
