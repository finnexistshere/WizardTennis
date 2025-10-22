using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI playerScoreText;
    public TextMeshProUGUI opponentScoreText;

    [Header("Scoring Settings")]
    public int winningScore = 5;

    private int playerScore = 0;
    private int opponentScore = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep manager alive
            SceneManager.sceneLoaded += OnSceneLoaded; // Reconnect after reload
        }
        else
        {
            Destroy(gameObject);
        }
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

    public void AddPoint(string scorer)
    {
        if (scorer == "Player") playerScore++;
        else if (scorer == "Opponent") opponentScore++;

        // Save current scores
        GameData.PlayerScore = playerScore;
        GameData.OpponentScore = opponentScore;

        UpdateScoreUI();

        if (playerScore >= winningScore)
        {
            GameManager.Instance.GameOverFinal("You Win the Match!");
        }
        else if (opponentScore >= winningScore)
        {
            GameManager.Instance.GameOverFinal("Opponent Wins the Match!");
        }
        else
        {
            if (scorer == "Player")
                GameManager.Instance.GameOverRound("Point for Player!");
            else
                GameManager.Instance.GameOverRound("Point for Opponent!");
        }
    }

    public void UpdateScoreUI()
    {
        if (playerScoreText != null)
            playerScoreText.text = playerScore.ToString();

        if (opponentScoreText != null)
            opponentScoreText.text = opponentScore.ToString();
    }

    public void LoadSavedScores()
    {
        playerScore = GameData.PlayerScore;
        opponentScore = GameData.OpponentScore;
        UpdateScoreUI();
    }

    public void ResetScores()
    {
        playerScore = 0;
        opponentScore = 0;
        GameData.PlayerScore = 0;
        GameData.OpponentScore = 0;
        UpdateScoreUI();
    }
}
