using UnityEngine;
using Unity.Netcode;

public class PlayerDeathNotifier : NetworkBehaviour
{
    public delegate void PlayerDestroyedHandler(GameObject player, GameObject cause);
    public static event PlayerDestroyedHandler OnPlayerDestroyed;

    /// <summary>
    /// Call this when the player dies or is destroyed.
    /// </summary>
    /// <param name="cause">The GameObject that caused the destruction (optional).</param>
    public void NotifyDeath(GameObject cause = null)
    {
        OnPlayerDestroyed?.Invoke(gameObject, cause);
    }

    private void OnDestroy()
    {
        // Optional: automatically notify if destroyed without explicit call
        OnPlayerDestroyed?.Invoke(gameObject, null);
    }
}
