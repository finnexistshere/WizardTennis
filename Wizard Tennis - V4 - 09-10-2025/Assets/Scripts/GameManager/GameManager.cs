using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Pickup Settings")]
    public List<GameObject> pickupPrefabs;
    public float spawnInterval = 7f;
    public float spawnRadius = 25f;
    public int maxActivePickups = 4;

    [Header("Spawn Settings")]
    public Transform spawnCenter;
    private float spawnTimer;
    private List<GameObject> activePickups = new List<GameObject>();
    public Spellcasting spellcasting;

    [Header("Ball Settings")]
    public GameObject ballPrefab;
    public Transform ballSpawnPoint;
    private GameObject currentBall;

    [Header("UI Panels")]
    public GameObject pauseMenuUI;
    public GameObject gameOverUI;
    public TextMeshProUGUI WinLoseText;

    private bool isPaused = false;
    public PlayerInput playerInput;

    private BallSpawner ballSpawner;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Time.timeScale = 1f;

        ballSpawner = FindObjectOfType<BallSpawner>();

        if (spawnCenter == null)
            spawnCenter = GameObject.Find("SpawnCenter").transform;

        pauseMenuUI = GameObject.Find("PauseMenu");
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        gameOverUI = GameObject.Find("GameOverMenu");
        if (gameOverUI != null) gameOverUI.SetActive(false);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");
    }

    private void Start()
    {
        spawnTimer = spawnInterval;

        // Setup UI buttons
        if (pauseMenuUI != null)
        {
            foreach (var btn in pauseMenuUI.GetComponentsInChildren<Button>())
            {
                string name = btn.name;
                btn.onClick.AddListener(() => buttonClick(name));
            }
        }

        ResetRound(); // start with a ball
    }

    private void Update()
    {

        // Clean up destroyed pickups
        activePickups.RemoveAll(p => p == null);

        // Spawn new pickups if timer elapsed
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && activePickups.Count < maxActivePickups)
        {
            SpawnPickup();
            spawnTimer = spawnInterval;
        }
    }

    void OnPause(InputAction.CallbackContext context)
    {
        // Only respond when the input is actually performed (not started or canceled)
        if (!context.performed) return;

        TogglePause();
    }

    private void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }


    void buttonClick(string buttonName)
    {
        switch (buttonName)
        {
            case "Resume": ResumeGame(); break;
            case "Restart": RestartGame(); break;
            case "Menu": QuitMenu(); break;
            case "OS": QuitGame(); break;
        }
    }

    private void SpawnPickup()
    {
        Vector3 spawnPos = Vector3.zero;
        bool validPositionFound = false;
        int attempts = 0;

        while (!validPositionFound && attempts < 20)
        {
            attempts++;
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            spawnPos = spawnCenter.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            bool tooClose = false;
            foreach (GameObject pickup in activePickups)
            {
                if (pickup != null && Vector3.Distance(pickup.transform.position, spawnPos) < 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                validPositionFound = true;
        }

        if (!validPositionFound) return;

        GameObject prefab = pickupPrefabs[Random.Range(0, pickupPrefabs.Count)];
        if (!spellcasting.spellBook.ContainsKey(prefab.GetComponent<PickupEffect>().SpellAddress))
        {
            GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);
            activePickups.Add(newPickup);
        }
    }

    // ----- Pause / Resume -----
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
        pauseMenuUI?.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");
        pauseMenuUI?.SetActive(false);
    }

    // ----- Show message (round messages) -----
    public void ShowMessage(string message)
    {
        if (WinLoseText != null)
        {
            WinLoseText.text = message;
        }

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        // Automatically reset round after 2 seconds (real time)
        Invoke(nameof(ResetRound), 2f);
    }

    public void HideMessage()
    {
        if (gameOverUI != null)
            gameOverUI.SetActive(false);
    }

    // ----- Reset round / respawn ball -----
    public void ResetRound()
    {
        HideMessage();

        if (currentBall != null)
            Destroy(currentBall);

        if (ballPrefab != null && ballSpawnPoint != null)
        {
            currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);

            // Set the ball to serve mode
            Ball ballScript = currentBall.GetComponent<Ball>();
            if (ballScript != null)
                ballScript.serving = true;
        }

        // Resume time in case it was paused
        Time.timeScale = 1f;
    }

    // ----- Round Over (show message, pause) -----
    public void RoundOver(string message)
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        if (WinLoseText != null)
            WinLoseText.text = message;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
    }

    // ----- Next Round (button-triggered) -----
    public void NextRound()
    {
        // Reload scene to reset positions, physics, etc.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ----- Final Game Over -----
    public void GameOver(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        if (WinLoseText != null)
            WinLoseText.text = message;
    }

    // ----- Restart / Quit -----
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    private void OnDrawGizmos()
    {
        if (spawnCenter != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(spawnCenter.position, spawnRadius);
        }
    }

    // Round over (after a point)
    public void GameOverRound(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;

        // Wait for player to press "Next Round" button
    }

    public void GameOverFinal(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;

        // Match over, can restart scene or go to menu
    }

}
