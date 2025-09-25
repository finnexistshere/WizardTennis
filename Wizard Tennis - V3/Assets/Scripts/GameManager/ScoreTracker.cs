using UnityEngine;
using TMPro;

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
            DontDestroyOnLoad(gameObject); // Preserve across scene reload
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateScoreUI();
    }

    public void AddPoint(string scorer)
    {
        if (scorer == "Player") playerScore++;
        else if (scorer == "Opponent") opponentScore++;

        UpdateScoreUI();

        // Check match win condition
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
            // Just show end-of-rally game over UI
            if (scorer == "Player")
                GameManager.Instance.GameOverRound("Point for Player!");
            else
                GameManager.Instance.GameOverRound("Point for Opponent!");
        }
    }

    private void UpdateScoreUI()
    {
        if (playerScoreText != null)
            playerScoreText.text = playerScore.ToString();

        if (opponentScoreText != null)
            opponentScoreText.text = opponentScore.ToString();
    }

    public void ResetScores()
    {
        playerScore = 0;
        opponentScore = 0;
        UpdateScoreUI();
    }
}
