using UnityEngine;
using Unity.Netcode;

public class ServingBarrierController : NetworkBehaviour
{
    [Header("Barrier Colliders")]
    [Tooltip("All colliders that should block the ball during serve")]
    [SerializeField] private Collider[] barrierColliders;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;

    private void Awake()
    {
        // Auto-collect colliders if none assigned
        if (barrierColliders == null || barrierColliders.Length == 0)
        {
            barrierColliders = GetComponentsInChildren<Collider>(true);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Ensure barriers start ENABLED
            SetBarriersEnabled(true);
        }
    }

    // ===================== PUBLIC API =====================

    /// <summary>
    /// Called by any client to request barrier deactivation
    /// </summary>
    public void RequestDisableBarriers()
    {
        if (IsServer)
        {
            DisableBarriersClientRpc();
            Debug.Log("REQUESTING BARRIER DISABLE - SERVER");
        }
        else
        {
            RequestDisableBarriersServerRpc();
            Debug.Log("REQUESTING BARRIER DISABLE");
        }
    }

    /// <summary>
    /// Called by server or clients to request barrier enable
    /// </summary>
    public void RequestEnableBarriers()
    {
        if (IsServer)
        {
            EnableBarriersClientRpc();
        }
        else
        {
            RequestEnableBarriersServerRpc();
        }
    }

    // ===================== SERVER RPCs =====================

    [ServerRpc(RequireOwnership = false)]
    private void RequestDisableBarriersServerRpc()
    {
        DisableBarriersClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestEnableBarriersServerRpc()
    {
        EnableBarriersClientRpc();
    }

    // ===================== CLIENT RPCs =====================

    [ClientRpc]
    private void DisableBarriersClientRpc()
    {
        SetBarriersEnabled(false);
    }

    [ClientRpc]
    private void EnableBarriersClientRpc()
    {
        SetBarriersEnabled(true);
    }

    // ===================== INTERNAL =====================

    private void SetBarriersEnabled(bool enabled)
    {
        foreach (var col in barrierColliders)
        {
            if (col != null)
                col.enabled = enabled;
        }

        if (logStateChanges)
        {
            Debug.Log($"[ServingBarrierController] Barriers {(enabled ? "ENABLED" : "DISABLED")}");
        }
    }
}
