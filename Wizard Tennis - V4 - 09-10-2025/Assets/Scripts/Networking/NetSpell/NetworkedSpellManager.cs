using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Universal manager that tracks all networked players and provides robust context for spell effects.
/// Ensures reliable caster/opponent identification across the network.
/// </summary>
public class NetworkedSpellManager : NetworkBehaviour
{
    public static NetworkedSpellManager Instance { get; private set; }

    [Header("Player Tracking")]
    private Dictionary<ulong, NetworkedSpellcasting> playersByClientId = new Dictionary<ulong, NetworkedSpellcasting>();
    private Dictionary<ulong, NetworkedSpellcasting> playersByNetworkId = new Dictionary<ulong, NetworkedSpellcasting>();

    [Header("Ball Tracking")]
    private GameObject currentBall;
    private float ballCheckInterval = 0.5f;
    private float nextBallCheckTime = 0f;

    [Header("References")]
    public NetworkedSpellEffects spellEffects;   // UPDATED
    public TennisAI tennisAI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (spellEffects == null)
            spellEffects = FindObjectOfType<NetworkedSpellEffects>();   // UPDATED

        if (tennisAI == null)
            tennisAI = FindObjectOfType<TennisAI>();
    }

    private void Update()
    {
        // Periodic ball check
        if (Time.time >= nextBallCheckTime)
        {
            nextBallCheckTime = Time.time + ballCheckInterval;
            RefreshBall();
        }
    }

    /// <summary>
    /// Register a player when they spawn
    /// </summary>
    public void RegisterPlayer(NetworkedSpellcasting player)
    {
        if (player == null) return;

        ulong clientId = player.OwnerClientId;
        ulong networkId = player.GetComponent<NetworkObject>().NetworkObjectId;

        playersByClientId[clientId] = player;
        playersByNetworkId[networkId] = player;

        Debug.Log($"[SpellManager] Registered player - ClientId: {clientId}, NetworkId: {networkId}");
    }

    /// <summary>
    /// Unregister a player when they disconnect or despawn
    /// </summary>
    public void UnregisterPlayer(NetworkedSpellcasting player)
    {
        if (player == null) return;

        ulong clientId = player.OwnerClientId;
        ulong networkId = player.GetComponent<NetworkObject>().NetworkObjectId;

        playersByClientId.Remove(clientId);
        playersByNetworkId.Remove(networkId);

        Debug.Log($"[SpellManager] Unregistered player - ClientId: {clientId}");
    }

    /// <summary>
    /// Get player by client ID
    /// </summary>
    public NetworkedSpellcasting GetPlayerByClientId(ulong clientId)
    {
        return playersByClientId.TryGetValue(clientId, out var player) ? player : null;
    }

    /// <summary>
    /// Get player by network object ID
    /// </summary>
    public NetworkedSpellcasting GetPlayerByNetworkId(ulong networkId)
    {
        return playersByNetworkId.TryGetValue(networkId, out var player) ? player : null;
    }

    /// <summary>
    /// Get opponent for a given player
    /// </summary>
    public NetworkedSpellcasting GetOpponent(NetworkedSpellcasting player)
    {
        if (player == null) return null;

        foreach (var kvp in playersByClientId)
        {
            if (kvp.Value != player)
                return kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// Get opponent by client ID
    /// </summary>
    public NetworkedSpellcasting GetOpponentByClientId(ulong casterClientId)
    {
        var caster = GetPlayerByClientId(casterClientId);
        return GetOpponent(caster);
    }

    /// <summary>
    /// Get the current ball in the scene
    /// </summary>
    public GameObject GetBall()
    {
        if (currentBall != null && currentBall.activeInHierarchy)
            return currentBall;

        RefreshBall();
        return currentBall;
    }

    /// <summary>
    /// Force refresh the ball reference
    /// </summary>
    public void RefreshBall()
    {
        // Check if current ball is still valid
        if (currentBall != null && currentBall.activeInHierarchy)
            return;

        // Try multiple methods to find the ball
        currentBall = GameObject.FindWithTag("Ball");

        if (currentBall == null)
            currentBall = GameObject.Find("Ball");

        if (currentBall == null)
        {
            Ball ballComponent = FindObjectOfType<Ball>();
            if (ballComponent != null)
                currentBall = ballComponent.gameObject;
        }

        if (currentBall == null)
        {
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.gameObject.name.ToLower().Contains("ball"))
                {
                    currentBall = netObj.gameObject;
                    break;
                }
            }
        }

        if (currentBall != null)
            Debug.Log($"[SpellManager] Ball refreshed: {currentBall.name}");
    }

    /// <summary>
    /// Set context for NetworkedSpellEffects based on caster client ID
    /// </summary>
    public void SetSpellContext(ulong casterClientId)
    {
        if (spellEffects == null) return;

        var caster = GetPlayerByClientId(casterClientId);
        var opponent = GetOpponentByClientId(casterClientId);

        if (caster != null)
        {
            spellEffects.SetContext(
                caster.gameObject,
                opponent?.gameObject,
                tennisAI
            );

            Debug.Log($"[SpellManager] Context set - Caster: {caster.gameObject.name}, Opponent: {opponent?.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[SpellManager] Could not set context for client {casterClientId}");
        }
    }

    /// <summary>
    /// Get all registered players
    /// </summary>
    public List<NetworkedSpellcasting> GetAllPlayers()
    {
        return new List<NetworkedSpellcasting>(playersByClientId.Values);
    }

    /// <summary>
    /// Get player count
    /// </summary>
    public int GetPlayerCount()
    {
        return playersByClientId.Count;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
