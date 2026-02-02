using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class NetworkedScoreManager : NetworkBehaviour
{
    public static NetworkedScoreManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI playerScoreText;
    public TextMeshProUGUI opponentScoreText;

    [Header("Scoring Settings")]
    public int winningScore = 5;

    // Network synced scores
    private NetworkVariable<int> playerScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> opponentScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Green spell tracking - server authoritative
    private NetworkVariable<int> currentRallyCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> greenPointsAccumulated = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Green Spell Settings")]
    [SerializeField] private int ralliesPerGreenPoint = 3; // Every 3 rallies = 1 green point

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribe to value changes
        playerScore.OnValueChanged += OnPlayerScoreChanged;
        opponentScore.OnValueChanged += OnOpponentScoreChanged;
        currentRallyCount.OnValueChanged += OnRallyCountChanged;
        greenPointsAccumulated.OnValueChanged += OnGreenPointsChanged;

        // Update UI immediately
        UpdateScoreUI();
        UpdateGreenPointsUI();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Unsubscribe
        playerScore.OnValueChanged -= OnPlayerScoreChanged;
        opponentScore.OnValueChanged -= OnOpponentScoreChanged;
        currentRallyCount.OnValueChanged -= OnRallyCountChanged;
        greenPointsAccumulated.OnValueChanged -= OnGreenPointsChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reconnect UI after scene reload
        var playerText = GameObject.Find("PlayerScoreText");
        var opponentText = GameObject.Find("OpponentScoreText");

        if (playerText)
            playerScoreText = playerText.GetComponent<TextMeshProUGUI>();
        if (opponentText)
            opponentScoreText = opponentText.GetComponent<TextMeshProUGUI>();

        UpdateScoreUI();
        UpdateGreenPointsUI();
    }

    /// <summary>
    /// Called when a player hits the ball - increments rally count
    /// Should be called from CollisionTrackerBall on the server
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void IncrementRallyCountServerRpc()
    {
        if (!IsServer) return;

        currentRallyCount.Value++;

        Debug.Log($"[ScoreManager-Server] Rally count: {currentRallyCount.Value}");

        // Check if Green spell is active
        bool greenActive = IsGreenSpellActive();

        if (greenActive)
        {
            // Every 3 rallies = 1 green point
            if (currentRallyCount.Value % ralliesPerGreenPoint == 0)
            {
                greenPointsAccumulated.Value++;
                Debug.Log($"[ScoreManager-Server] Green point earned! Total: {greenPointsAccumulated.Value}");
            }
        }

        // Update UI for all clients
        UpdateRallyCountClientRpc(currentRallyCount.Value);
    }

    /// <summary>
    /// Checks if Green spell is currently active
    /// </summary>
    private bool IsGreenSpellActive()
    {
        // Check if any player has Green spell active
        var spellEffects = NetworkedSpellEffects.Instance;
        if (spellEffects != null)
        {
            // Check if spellName is "Green"
            // Check if ANY player has Green spell active
            foreach (var clientId in new ulong[] { 0, 1 }) // Assuming max 2 players
            {
                string activeSpell = spellEffects.GetActiveSpellName(clientId);
                if (activeSpell == "Green")
                {
                    Debug.Log($"[ScoreManager] Green spell detected for client {clientId}");
                    return true;
                }
            }
            return false;
        }

        // Fallback: check NetworkedBall components
        var balls = FindObjectsOfType<NetworkedBall>();
        foreach (var ball in balls)
        {
            if (ball.green)
            {
                Debug.Log("[ScoreManager] Green spell detected via NetworkedBall");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Awards a point to the winner, including any accumulated green points
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AddPointServerRpc(string scorer)
    {
        if (!IsServer) return;

        Debug.Log($"[ScoreManager-Server] AddPoint - scorer: {scorer}, greenPoints: {greenPointsAccumulated.Value}");

        int pointsToAdd = 1 + greenPointsAccumulated.Value;

        if (scorer == "Player")
        {
            playerScore.Value += pointsToAdd;
            Debug.Log($"[ScoreManager-Server] Player scored {pointsToAdd} points (base 1 + {greenPointsAccumulated.Value} green)");
        }
        else if (scorer == "Opponent")
        {
            opponentScore.Value += pointsToAdd;
            Debug.Log($"[ScoreManager-Server] Opponent scored {pointsToAdd} points (base 1 + {greenPointsAccumulated.Value} green)");
        }

        // Reset rally count and green points for next round
        currentRallyCount.Value = 0;
        greenPointsAccumulated.Value = 0;

        // Check win condition
        CheckWinConditionClientRpc(scorer);
    }

    [ClientRpc]
    private void CheckWinConditionClientRpc(string scorer)
    {
        // Play sound effects
        var spellEffects = NetworkedSpellEffects.Instance;
        if (spellEffects != null)
        {
            if (scorer == "Player")
                spellEffects.OnPointWon();
            else if (scorer == "Opponent")
                spellEffects.OnPointLost();
        }

        // Only the server/host should handle game over logic
        if (!IsServer) return;

        if (playerScore.Value >= winningScore)
        {
            NetworkedGameManager.Instance?.GameOverFinal("You Win the Match!");
        }
        else if (opponentScore.Value >= winningScore)
        {
            NetworkedGameManager.Instance?.GameOverFinal("Opponent Wins the Match!");
        }
        else
        {
            if (scorer == "Player")
                NetworkedGameManager.Instance?.GameOverRound("Point for Player!");
            else
                NetworkedGameManager.Instance?.GameOverRound("Point for Opponent!");
        }
    }

    /// <summary>
    /// Updates rally count UI on all clients
    /// </summary>
    [ClientRpc]
    private void UpdateRallyCountClientRpc(int rallyCount)
    {
        // Update local player's UI
        var uiManager = NetworkedUIManager.GetLocalPlayerUI();
        if (uiManager != null)
        {
            uiManager.UpdateRallyCount(rallyCount);
        }
    }

    // Callback when values change
    private void OnPlayerScoreChanged(int oldValue, int newValue)
    {
        UpdateScoreUI();
    }

    private void OnOpponentScoreChanged(int oldValue, int newValue)
    {
        UpdateScoreUI();
    }

    private void OnRallyCountChanged(int oldValue, int newValue)
    {
        Debug.Log($"[ScoreManager-Client] Rally count changed: {oldValue} -> {newValue}");
        UpdateRallyCountClientRpc(newValue);
    }

    private void OnGreenPointsChanged(int oldValue, int newValue)
    {
        Debug.Log($"[ScoreManager-Client] Green points changed: {oldValue} -> {newValue}");
        UpdateGreenPointsUI();
    }

    /// <summary>
    /// Updates the main score UI
    /// </summary>
    public void UpdateScoreUI()
    {
        if (playerScoreText != null)
            playerScoreText.text = playerScore.Value.ToString();

        if (opponentScoreText != null)
            opponentScoreText.text = opponentScore.Value.ToString();
    }

    /// <summary>
    /// Updates green points UI for local player
    /// </summary>
    private void UpdateGreenPointsUI()
    {
        var uiManager = NetworkedUIManager.GetLocalPlayerUI();
        if (uiManager != null)
        {
            uiManager.UpdateGreenPoints(greenPointsAccumulated.Value);
        }
    }

    /// <summary>
    /// Resets rally count (call at start of each round)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ResetRallyCountServerRpc()
    {
        if (!IsServer) return;

        currentRallyCount.Value = 0;
        greenPointsAccumulated.Value = 0;

        Debug.Log("[ScoreManager-Server] Rally count and green points reset");
    }

    /// <summary>
    /// Resets all scores (call at start of new match)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ResetScoresServerRpc()
    {
        if (!IsServer) return;

        playerScore.Value = 0;
        opponentScore.Value = 0;
        currentRallyCount.Value = 0;
        greenPointsAccumulated.Value = 0;

        Debug.Log("[ScoreManager-Server] All scores reset");
    }

    // Public getters
    public int GetPlayerScore() => playerScore.Value;
    public int GetOpponentScore() => opponentScore.Value;
    public int GetRallyCount() => currentRallyCount.Value;
    public int GetGreenPoints() => greenPointsAccumulated.Value;

    /// <summary>
    /// Helper method to add points from legacy code
    /// </summary>
    public void AddPoint(string scorer)
    {
        if (IsServer)
        {
            AddPointServerRpc(scorer);
        }
        else
        {
            Debug.LogWarning("[ScoreManager] AddPoint called on client - use AddPointServerRpc instead");
        }
    }
}