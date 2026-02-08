using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public interface ISpellcasting
{
    Dictionary<string, string> spellBook { get; }
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Pickup Settings")]
    public List<GameObject> allPickupPrefabs;
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
    public GameObject tutorialPanel;

    private ISpellcasting spellcastingReference;

    private bool isPaused = false;
    public PlayerInput playerInput;

    private BallSpawner ballSpawner;

    private bool pickupsUnlocked = false;

    /// <summary>
    /// Locks all pickup spawning (used by default at game start).
    /// </summary>
    public void LockPickupSpawning()
    {
        pickupsUnlocked = false;
    }

    /// <summary>
    /// Unlocks pickup spawning so new pickups can appear.
    /// Call this from another script when you’re ready.
    /// </summary>
    public void UnlockPickupSpawning()
    {
        pickupsUnlocked = true;
        spawnTimer = spawnInterval; // reset timer so first spawn happens normally
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LockPickupSpawning();
        Time.timeScale = 1f;

        // Resolve spawnCenter
        if (spawnCenter == null)
            spawnCenter = GameObject.Find("SpawnCenter")?.transform;

        // Resolve UI panels
        if (pauseMenuUI == null)
            pauseMenuUI = GameObject.Find("PauseMenu");
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (gameOverUI == null)
            gameOverUI = GameObject.Find("GameOverMenu");
        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");

        if (OptionsManager.Instance != null)
        {
            if (OptionsManager.Instance.FireballBool) pickupPrefabs.Add(allPickupPrefabs[0]);
            if (OptionsManager.Instance.IceBool) pickupPrefabs.Add(allPickupPrefabs[1]);
            if (OptionsManager.Instance.LightningBool) pickupPrefabs.Add(allPickupPrefabs[2]);
            if (OptionsManager.Instance.ShadowBool) pickupPrefabs.Add(allPickupPrefabs[3]);
            if (OptionsManager.Instance.GreenBool) pickupPrefabs.Add(allPickupPrefabs[4]);
            if (OptionsManager.Instance.StoneBool) pickupPrefabs.Add(allPickupPrefabs[5]);
            if (OptionsManager.Instance.ChronosBool) pickupPrefabs.Add(allPickupPrefabs[6]);
            if (OptionsManager.Instance.GeminiBool) pickupPrefabs.Add(allPickupPrefabs[7]);
            if (OptionsManager.Instance.PiscesBool) pickupPrefabs.Add(allPickupPrefabs[8]);
            if (OptionsManager.Instance.JollyBool) pickupPrefabs.Add(allPickupPrefabs[9]);
            if (OptionsManager.Instance.BlinkBool) pickupPrefabs.Add(allPickupPrefabs[10]);
            if (OptionsManager.Instance.WarpBool) pickupPrefabs.Add(allPickupPrefabs[11]);
            if (OptionsManager.Instance.TetherBool) pickupPrefabs.Add(allPickupPrefabs[12]);
            if (OptionsManager.Instance.MudBool) pickupPrefabs.Add(allPickupPrefabs[13]);
            if (OptionsManager.Instance.GambitBool) pickupPrefabs.Add(allPickupPrefabs[14]);
            if (OptionsManager.Instance.GorbinoBool) pickupPrefabs.Add(allPickupPrefabs[15]);
        } else
        {
            pickupPrefabs = allPickupPrefabs;
        }

            ScoreManager.Instance.LoadSavedScores();
    }

    private void Start()
    {
        spawnTimer = spawnInterval;

        // --- Resolve the correct spellcasting reference ---
        // If networked, find the local player's NetworkedSpellcasting
        NetworkedSpellcasting netSpell = FindObjectOfType<NetworkedSpellcasting>();
        if (netSpell != null && netSpell.IsOwner)
        {
            spellcastingReference = (ISpellcasting)netSpell; // explicit cast
        }

        // Otherwise fall back to normal single-player Spellcasting
        if (spellcastingReference == null && spellcasting != null)
            spellcastingReference = (ISpellcasting)spellcasting; // explicit cast

        // Setup UI buttons
        if (pauseMenuUI != null)
        {
            foreach (var btn in pauseMenuUI.GetComponentsInChildren<Button>())
            {
                string name = btn.name;
                btn.onClick.AddListener(() => buttonClick(name));
            }
        }

        ResetRound();
    }

    private void Update()
    {
        if (tutorialPanel.gameObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.E))
                    {
                tutorialPanel.gameObject.SetActive(false);
                    }
        }

        // Clean up destroyed pickups
        activePickups.RemoveAll(p => p == null);

        // Spawn new pickups if timer elapsed
        // Only spawn if pickups are unlocked
        if (pickupsUnlocked)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && activePickups.Count < maxActivePickups)
            {
                SpawnPickup();
                spawnTimer = spawnInterval;
            }
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

    // --- Updated SpawnPickup with null check and network-aware spellcasting ---
    private void SpawnPickup()
    {
        if (spawnCenter == null)
        {
            Debug.LogWarning("[PickupSpawner] spawnCenter not assigned.");
            return;
        }

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

        GameObject prefab = GetWeightedPickup();
        if (prefab == null) return;

        var pickupEffect = prefab.GetComponent<PickupEffect>();
        if (pickupEffect == null) return;

        // If it can't find Spellcasting in the scene, It'll try and track down a Networked Spellcasting. Is this a bad way of doing this?
        // Yes.
        if (spellcastingReference == null)
        {
            NetworkedSpellcasting netSpell = FindObjectOfType<NetworkedSpellcasting>();
            if (netSpell != null && netSpell.IsOwner)
                spellcastingReference = netSpell;
            else
                spellcastingReference = (ISpellcasting)FindObjectOfType<Spellcasting>();

            if (spellcastingReference == null)
                Debug.LogWarning("[PickupSpawner] No spellcasting reference found!");
        }

        bool alreadyOwned = spellcastingReference.spellBook.ContainsKey(pickupEffect.SpellAddress);
        if (!alreadyOwned)
        {
            GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);
            activePickups.Add(newPickup);
        }
    }

    // --- GetWeightedPickup unchanged ---
    private GameObject GetWeightedPickup()
    {
        float totalWeight = 0f;
        foreach (GameObject prefab in pickupPrefabs)
        {
            var effect = prefab.GetComponent<PickupEffect>();
            if (effect != null)
                totalWeight += Mathf.Max(0, effect.spawnWeight);
        }

        if (totalWeight <= 0f)
            return null;

        float randomPoint = Random.value * totalWeight;

        foreach (GameObject prefab in pickupPrefabs)
        {
            var effect = prefab.GetComponent<PickupEffect>();
            if (effect == null) continue;

            float weight = Mathf.Max(0, effect.spawnWeight);
            if (randomPoint < weight)
                return prefab;

            randomPoint -= weight;
        }

        return pickupPrefabs[Random.Range(0, pickupPrefabs.Count)];
    }

    // ----- Pause / Resume -----
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        else
            Debug.LogError("[GameManager] pauseMenuUI is NULL in ResumeGame!");
        Debug.Log("Resuming Game");
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

        ScoreManager.Instance.LoadSavedScores();


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
        // Keep the existing score manager and scores alive
        Time.timeScale = 1f;

        // Reload scene but keep persistent managers
        SceneManager.sceneLoaded += OnSceneReloaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        ScoreManager.Instance.LoadSavedScores();

    }

    public void RestartGame()
    {
        // Full reset (scores too)
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScores();

        Time.timeScale = 1f;
        SceneManager.sceneLoaded += OnSceneReloaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        ScoreManager.Instance.LoadSavedScores();
    }

    private void OnSceneReloaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneReloaded;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        Time.timeScale = 1f;
        ResetRound();

        // Refresh the score display
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.LoadSavedScores();
        ScoreManager.Instance.UpdateScoreUI();

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

    public void QuitMenu()
    {
        ScoreManager.Instance.ResetScores();
        SceneManager.LoadScene("Main Menu");
        Time.timeScale = 1f;
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
