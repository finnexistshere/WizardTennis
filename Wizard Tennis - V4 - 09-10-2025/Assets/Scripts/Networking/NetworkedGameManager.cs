using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Netcode;

public class NetworkedGameManager : NetworkBehaviour
{
    public static NetworkedGameManager Instance;

    [Header("Pickup Settings")]
    public List<GameObject> pickupPrefabs;
    public float spawnInterval = 7f;
    public float spawnRadius = 25f;
    public int maxActivePickupsPerPlayer = 4;

    [Header("Spawn Settings - Host Side")]
    public Transform hostSpawnCenter;
    private float hostSpawnTimer;
    private List<GameObject> hostActivePickups = new List<GameObject>();

    [Header("Spawn Settings - Client Side")]
    public Transform clientSpawnCenter;
    private float clientSpawnTimer;
    private List<GameObject> clientActivePickups = new List<GameObject>();

    [Header("UI Panels")]
    public GameObject pauseMenuUI;
    public GameObject gameOverUI;
    public TextMeshProUGUI WinLoseText;
    public GameObject tutorialPanel;

    private bool isPaused = false;
    public PlayerInput playerInput;

    private bool pickupsUnlocked = false;

    // References to both players' spellcasting
    private NetworkedSpellcasting hostSpellcasting;
    private NetworkedSpellcasting clientSpellcasting;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LockPickupSpawning();
        Time.timeScale = 1f;

        // Resolve spawn centers
        if (hostSpawnCenter == null)
            hostSpawnCenter = GameObject.Find("HostSpawnCenter")?.transform;
        if (clientSpawnCenter == null)
            clientSpawnCenter = GameObject.Find("ClientSpawnCenter")?.transform;

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

        ScoreManager.Instance?.LoadSavedScores();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("[NetworkedGameManager] Server initialized.");
        }

        // Find both players' spellcasting components
        StartCoroutine(FindSpellcastingReferences());
    }

    private IEnumerator FindSpellcastingReferences()
    {
        // Wait a frame for all NetworkObjects to spawn
        yield return new WaitForSeconds(0.5f);

        var allSpellcasting = FindObjectsOfType<NetworkedSpellcasting>();

        foreach (var spell in allSpellcasting)
        {
            if (spell.OwnerClientId == 0) // Host
                hostSpellcasting = spell;
            else if (spell.OwnerClientId == 1) // Client
                clientSpellcasting = spell;
        }

        if (hostSpellcasting != null)
            Debug.Log("[NetworkedGameManager] Found host spellcasting.");
        else
            Debug.LogWarning("[NetworkedGameManager] Host spellcasting not found!");

        if (clientSpellcasting != null)
            Debug.Log("[NetworkedGameManager] Found client spellcasting.");
        else
            Debug.LogWarning("[NetworkedGameManager] Client spellcasting not found!");
    }

    /// <summary>
    /// Locks all pickup spawning (used by default at game start).
    /// </summary>
    public void LockPickupSpawning()
    {
        pickupsUnlocked = false;
    }

    /// <summary>
    /// Unlocks pickup spawning so new pickups can appear.
    /// </summary>
    public void UnlockPickupSpawning()
    {
        pickupsUnlocked = true;
        hostSpawnTimer = spawnInterval;
        clientSpawnTimer = spawnInterval;
        Debug.Log("[NetworkedGameManager] Pickup spawning unlocked.");
    }

    private void Start()
    {
        hostSpawnTimer = spawnInterval;
        clientSpawnTimer = spawnInterval;

        // Setup UI buttons
        if (pauseMenuUI != null)
        {
            foreach (var btn in pauseMenuUI.GetComponentsInChildren<Button>())
            {
                string name = btn.name;
                btn.onClick.AddListener(() => buttonClick(name));
            }
        }
    }

    private void Update()
    {
        // Only owner handles input
        if (!IsOwner) return;

        if (tutorialPanel != null && tutorialPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                tutorialPanel.SetActive(false);
            }
        }

        // Only server spawns pickups
        if (!IsServer || !pickupsUnlocked) return;

        // Clean up destroyed pickups
        hostActivePickups.RemoveAll(p => p == null);
        clientActivePickups.RemoveAll(p => p == null);

        // Spawn pickups for host side
        hostSpawnTimer -= Time.deltaTime;
        if (hostSpawnTimer <= 0f && hostActivePickups.Count < maxActivePickupsPerPlayer)
        {
            SpawnPickupForPlayer(hostSpawnCenter, hostActivePickups, hostSpellcasting);
            hostSpawnTimer = spawnInterval;
        }

        // Spawn pickups for client side
        clientSpawnTimer -= Time.deltaTime;
        if (clientSpawnTimer <= 0f && clientActivePickups.Count < maxActivePickupsPerPlayer)
        {
            SpawnPickupForPlayer(clientSpawnCenter, clientActivePickups, clientSpellcasting);
            clientSpawnTimer = spawnInterval;
        }
    }

    void OnPause(InputAction.CallbackContext context)
    {
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

    private void SpawnPickupForPlayer(Transform spawnCenter, List<GameObject> activePickups, NetworkedSpellcasting spellcasting)
    {
        if (!IsServer) return;
        if (spawnCenter == null)
        {
            Debug.LogWarning("[NetworkedGameManager] spawnCenter not assigned.");
            return;
        }
        if (spellcasting == null)
        {
            Debug.LogWarning("[NetworkedGameManager] Spellcasting reference is null.");
            return;
        }

        Vector3 spawnPos = Vector3.zero;
        bool validPositionFound = false;
        int attempts = 0;

        // Find valid spawn position
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

        GameObject prefab = GetWeightedPickup(spellcasting);
        if (prefab == null) return;

        // Instantiate pickup on server
        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);
        activePickups.Add(newPickup);

        Debug.Log($"[NetworkedGameManager] Spawned pickup at {spawnPos} for player {spellcasting.OwnerClientId}");
    }

    private GameObject GetWeightedPickup(NetworkedSpellcasting spellcasting)
    {
        if (spellcasting == null) return null;

        // Build list of valid pickups (not already owned)
        List<GameObject> validPickups = new List<GameObject>();
        List<float> weights = new List<float>();

        foreach (GameObject prefab in pickupPrefabs)
        {
            var effect = prefab.GetComponent<PickupEffect>();
            if (effect == null) continue;

            bool alreadyOwned = spellcasting.spellBook.ContainsKey(effect.SpellAddress);
            if (!alreadyOwned)
            {
                validPickups.Add(prefab);
                weights.Add(Mathf.Max(0, effect.spawnWeight));
            }
        }

        if (validPickups.Count == 0) return null;

        // Weighted random selection
        float totalWeight = 0f;
        foreach (float w in weights)
            totalWeight += w;

        if (totalWeight <= 0f)
            return validPickups[Random.Range(0, validPickups.Count)];

        float randomPoint = Random.value * totalWeight;
        for (int i = 0; i < validPickups.Count; i++)
        {
            if (randomPoint < weights[i])
                return validPickups[i];
            randomPoint -= weights[i];
        }

        return validPickups[Random.Range(0, validPickups.Count)];
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
        Debug.Log("Resuming Game");
    }

    // ----- Show message (round messages) -----
    public void ShowMessage(string message)
    {
        if (WinLoseText != null)
            WinLoseText.text = message;

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        Invoke(nameof(HideMessage), 2f);
    }

    public void HideMessage()
    {
        if (gameOverUI != null)
            gameOverUI.SetActive(false);
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

    // ----- Restart -----
    public void RestartGame()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScores();

        Time.timeScale = 1f;
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

    public void QuitMenu()
    {
        ScoreManager.Instance?.ResetScores();

        // Disconnect from network
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost)
                NetworkManager.Singleton.Shutdown();
            else if (NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();
        }

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
        if (hostSpawnCenter != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(hostSpawnCenter.position, spawnRadius);
        }

        if (clientSpawnCenter != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(clientSpawnCenter.position, spawnRadius);
        }
    }

    // Round over (after a point)
    public void GameOverRound(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;
    }

    public void GameOverFinal(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;
    }

    // ----------------------------------------------------------
    // PUBLIC: Call this when a round ends (e.g. ball hits ground)
    // ----------------------------------------------------------
    public void ResetRound()
    {
        if (!IsServer) return;   // Only server controls reset

        StartCoroutine(ResetRoundRoutine());
    }

    private IEnumerator ResetRoundRoutine()
    {
        // Use realtime so coroutine continues even if timeScale == 0
        yield return new WaitForSecondsRealtime(1.0f);

        // Make sure game unpauses
        Time.timeScale = 1f;

        Debug.Log("[GameManager] Resetting round...");

        // ------------------------------------------------------
        // 1. REMOVE ALL BALLS (networked OR non-networked)
        // ------------------------------------------------------
        foreach (var ball in GameObject.FindGameObjectsWithTag("Ball"))
        {
            var netObj = ball.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(ball);
            }
        }

        // ------------------------------------------------------
        // 2. RESET PLAYER POSITIONS
        // ------------------------------------------------------
        ResetPlayerPositionsServerRpc();

        // ------------------------------------------------------
        // 3. RESET PICKUPS
        // ------------------------------------------------------
        foreach (var p in hostActivePickups)
            if (p != null) Destroy(p);
        hostActivePickups.Clear();

        foreach (var p in clientActivePickups)
            if (p != null) Destroy(p);
        clientActivePickups.Clear();

        hostSpawnTimer = spawnInterval;
        clientSpawnTimer = spawnInterval;

        pickupsUnlocked = true;


        // ------------------------------------------------------
        // 4. RESET SPELL SYSTEMS
        // ------------------------------------------------------
        if (hostSpellcasting != null) hostSpellcasting.ResetForNewRound();
        if (clientSpellcasting != null) clientSpellcasting.ResetForNewRound();

        // ------------------------------------------------------
        // 5. HIDE ROUND UI
        // ------------------------------------------------------
        // Reset UI + time on both clients
        ResetRoundClientRpc();

        Debug.Log("[GameManager] Round reset complete.");
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetPlayerPositionsServerRpc()
    {
        ResetPlayerPositionsClientRpc();
    }

    [ClientRpc]
    private void ResetRoundClientRpc()
    {
        Time.timeScale = 1f;

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");
    }

    [ClientRpc]
    private void ResetPlayerPositionsClientRpc()
    {
        var spawner = NetworkPlayerSpawner_Better.Instance;
        if (spawner == null)
        {
            Debug.LogError("[GameManager] No NetworkPlayerSpawner_Better in scene!");
            return;
        }

        // Use the public SpawnedPlayers dictionary
        foreach (var kvp in NetworkPlayerSpawner_Better.SpawnedPlayers)
        {
            ulong clientId = kvp.Key;
            GameObject player = kvp.Value;

            if (player == null) continue;

            // Try to get the spawn transform we recorded when that player was spawned
            if (NetworkPlayerSpawner_Better.PlayerSpawnPoints.TryGetValue(clientId, out Transform spawn))
            {
                player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
                // If you have a CharacterController or Rigidbody, zero velocities if necessary:
                if (player.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = false; // if you temporarily use kinematic states, adjust as needed
                }
            }
            else
            {
                Debug.LogWarning($"[GameManager] No spawn point recorded for client {clientId}");
            }
        }
    }
}