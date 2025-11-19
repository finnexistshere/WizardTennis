using UnityEngine;
using Unity.Netcode;
using System.Collections;
using Unity.Netcode.Components;

public class ClientSyncUnfreeze : NetworkBehaviour
{
    private Rigidbody rb;
    private NetworkTransform netTransform;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            StartCoroutine(UnfreezeAfterSync());
        else
        {
            // Other clients don’t move their own physics
            if (rb != null)
                rb.isKinematic = true;
        }
    }

    private IEnumerator UnfreezeAfterSync()
    {
        // Wait until NetworkTransform updates the client position at least once
        yield return new WaitUntil(() => netTransform != null && netTransform.IsSpawned);

        // Small delay to ensure the first authoritative position is applied
        yield return null;

        if (rb != null)
            rb.isKinematic = false;

        Destroy(this); // cleanup
    }
}
