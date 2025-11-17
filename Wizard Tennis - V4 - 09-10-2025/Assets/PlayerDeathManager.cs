using UnityEngine;

public class PlayerDeathManager : MonoBehaviour
{
    private void OnEnable()
    {
        PlayerDeathNotifier.OnPlayerDestroyed += HandlePlayerDestroyed;
    }

    private void OnDisable()
    {
        PlayerDeathNotifier.OnPlayerDestroyed -= HandlePlayerDestroyed;
    }

    private void HandlePlayerDestroyed(GameObject player, GameObject cause)
    {
        string causeName = cause != null ? cause.name : "Unknown";
        Debug.Log($"Player '{player.name}' was destroyed by '{causeName}'");
    }
}
