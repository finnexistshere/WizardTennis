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
    private SpellEffects spellEffects;

    [Header("Scoring Settings")]
    public int winningScore = 5;

    // NetworkVariables for synced scores
    private NetworkVariable<int> hostScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> clientScore = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int greenPoints;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            spellEffects = FindObjectOfType<SpellEffects>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to score changes
        hostScore.OnValueChanged += OnHostScoreChanged;
        clientScore.OnValueChanged += OnClientScoreChanged;

        // Initialize UI with current values
        UpdateScoreUI();

        Debug.Log($"[NetworkedScoreManager] Spawned - Host: {hostScore.Value}, Client: {clientScore.Value}");
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from score changes
        hostScore.OnValueChanged -= OnHostScoreChanged;
        clientScore.OnValueChanged -= OnClientScoreChanged;
    }

    private void OnHostScoreChanged(int previousValue, int newValue)
    {
        UpdateScoreUI();
        Debug.Log($"[NetworkedScoreManager] Host score changed: {previousValue} -> {newValue}");
    }

    private void OnClientScoreChanged(int previousValue, int newValue)
    {
        UpdateScoreUI();
        Debug.Log($"[NetworkedScoreManager] Client score changed: {previousValue} -> {newValue}");
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
    }

    /// <summary>
    /// Call this from collision detection scripts to add a point
    /// </summary>
    public void AddPoint(string scorer)
    {
        if (!IsServer)
        {
            // If not server, request via ServerRpc
            AddPointServerRpc(scorer);
            return;
        }

        // Server processes the point
        ServerAddPoint(scorer);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPointServerRpc(string scorer, ServerRpcParams rpcParams = default)
    {
        ServerAddPoint(scorer);
    }

    private void ServerAddPoint(string scorer)
    {
        if (!IsServer) return;

        // Determine which score to increment based on scorer
        // "Player" = host side, "Opponent" = client side
        if (scorer == "Host" || scorer == "Player")
        {
            hostScore.Value = hostScore.Value + 1 + greenPoints;
            Debug.Log($"[NetworkedScoreManager] Host scored! New score: {hostScore.Value}");
        }
        else if (scorer == "Client" || scorer == "Opponent")
        {
            clientScore.Value = clientScore.Value + 1 + greenPoints;
            Debug.Log($"[NetworkedScoreManager] Client scored! New score: {clientScore.Value}");
        }

        // Trigger spell effects
        TriggerSpellEffectsClientRpc(scorer);

        // Check win conditions
        if (hostScore.Value >= winningScore)
        {
            GameOverClientRpc("Host Wins the Match!");
        }
        else if (clientScore.Value >= winningScore)
        {
            GameOverClientRpc("Client Wins the Match!");
        }
        else
        {
            if (scorer == "Host" || scorer == "Player")
                RoundOverClientRpc("Point for Host!");
            else
                RoundOverClientRpc("Point for Client!");
        }
    }

    [ClientRpc]
    private void TriggerSpellEffectsClientRpc(string scorer)
    {
        if (spellEffects == null)
        {
            spellEffects = FindObjectOfType<SpellEffects>();
        }

        if (spellEffects != null)
        {
            if (scorer == "Host" || scorer == "Player")
            {
                spellEffects.OnPointWon();
            }
            else if (scorer == "Client" || scorer == "Opponent")
            {
                spellEffects.OnPointLost();
            }
        }
    }

    [ClientRpc]
    private void RoundOverClientRpc(string message)
    {
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.GameOverRound(message);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOverRound(message);
        }
    }

    [ClientRpc]
    private void GameOverClientRpc(string message)
    {
        if (NetworkedGameManager.Instance != null)
        {
            NetworkedGameManager.Instance.GameOverFinal(message);
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOverFinal(message);
        }
    }

    public void UpdateScoreUI()
    {
        // Display scores based on perspective
        // If you're the host, you see your score on left, client on right
        // If you're the client, you see your score on left, host on right

        if (IsServer || NetworkManager.Singleton == null)
        {
            // Host perspective or standalone
            if (playerScoreText != null)
                playerScoreText.text = hostScore.Value.ToString();
            if (opponentScoreText != null)
                opponentScoreText.text = clientScore.Value.ToString();
        }
        else
        {
            // Client perspective - swap the scores
            if (playerScoreText != null)
                playerScoreText.text = clientScore.Value.ToString();
            if (opponentScoreText != null)
                opponentScoreText.text = hostScore.Value.ToString();
        }
    }

    public void LoadSavedScores()
    {
        // In networked mode, scores are managed by the server
        // This is kept for compatibility but doesn't do anything in multiplayer
        UpdateScoreUI();
    }

    public void ResetScores()
    {
        if (!IsServer)
        {
            ResetScoresServerRpc();
            return;
        }

        hostScore.Value = 0;
        clientScore.Value = 0;
        UpdateScoreUI();
        Debug.Log("[NetworkedScoreManager] Scores reset.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetScoresServerRpc()
    {
        hostScore.Value = 0;
        clientScore.Value = 0;
    }

    // Helper method for collision scripts to determine scorer
    public void AddPointForClient(ulong clientId)
    {
        if (clientId == 0)
            AddPoint("Host");
        else
            AddPoint("Client");
    }
}