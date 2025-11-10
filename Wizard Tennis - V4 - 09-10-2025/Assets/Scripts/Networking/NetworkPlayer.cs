using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayer : NetworkBehaviour
{
    private MainCharacterMovement movement;
    private Ball ballHandler;
    private Spellcasting spellcasting;
    private UIManager uiManager;

    private Camera playerCamera;

    private void Awake()
    {
        movement = GetComponent<MainCharacterMovement>();
        ballHandler = GetComponent<Ball>();
        spellcasting = GetComponent<Spellcasting>();
        uiManager = FindObjectOfType<UIManager>(); // or attach via inspector
        playerCamera = Camera.main; // or assign child camera
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Disable local-only systems on non-owned players
            if (movement) movement.enabled = false;
            if (ballHandler) ballHandler.enabled = false;
            if (spellcasting) spellcasting.enabled = false;

            // Optional: Disable the player’s camera if each player has their own
            if (playerCamera) playerCamera.enabled = false;
            return;
        }

        // Local player setup
        if (uiManager != null)
            uiManager.UpdateSpellStatus("None");

        Debug.Log("Local player networked and active.");
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Any local-input logic you want to route manually
    }
}
