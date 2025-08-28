using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

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

    [Header("UI Panels")]
    public GameObject pauseMenuUI;
    public GameObject gameOverUI;
    public TextMeshProUGUI WinLoseText;

    private bool isPaused = false;

    private void Awake()
    {
        
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        spawnCenter = GameObject.Find("SpawnCenter").transform;
        pauseMenuUI = GameObject.Find("PauseMenu");
        pauseMenuUI.SetActive(false);
        gameOverUI = GameObject.Find("GameOverMenu");
        gameOverUI.SetActive(false);
    }

    private void Start()
    {
        spawnTimer = spawnInterval;

        //setup buttonz
        foreach (var btn in pauseMenuUI.GetComponentsInChildren<Button>())
        {
            string name = btn.name;
            btn.onClick.AddListener(() => buttonClick(name));
        }
    }

    private void Update()
    {
        // Pause toggle
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused) PauseGame();
            else ResumeGame();
        }

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
        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);
        activePickups.Add(newPickup);
    }

    // ----- Pause / Resume -----
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        pauseMenuUI?.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        pauseMenuUI?.SetActive(false);
    }

    // ----- Pause menu buttons -----

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

    // ----- Game Over -----
    public void GameOver(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        if (WinLoseText != null)
            WinLoseText.text = message;
    }

    // ----- Restart -----
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ----- Quit to menu -----
    public void QuitMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    // ----- Quit -----
    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    // ----- Pause menu buttons -----

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

    // ----- Quit to menu -----
    public void QuitMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }

    private void OnDrawGizmos()
    {
        if (spawnCenter != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(spawnCenter.position, spawnRadius);
        }
    }
}
