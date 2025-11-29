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

    [Header("Player Spawn Points")]
    public Transform hostPlayerSpawn;
    public Transform clientPlayerSpawn;

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

    // References to player GameObjects
    private GameObject hostPlayer;
    private GameObject clientPlayer;

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

        // Resolve player spawn points (can be same as spawn centers if not set)
        if (hostPlayerSpawn == null)
            hostPlayerSpawn = hostSpawnCenter;
        if (clientPlayerSpawn == null)
            clientPlayerSpawn = clientSpawnCenter;

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

        // Find both players' spellcasting components and player objects
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
            {
                hostSpellcasting = spell;
                hostPlayer = spell.gameObject;
            }
            else if (spell.OwnerClientId == 1) // Client
            {
                clientSpellcasting = spell;
                clientPlayer = spell.gameObject;
            }
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
    /// Resets both players to their spawn positions
    /// </summary>
    public void ResetPlayersToSpawn()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkedGameManager] ResetPlayersToSpawn called on client - should only be called on server!");
            return;
        }

        Debug.Log("[NetworkedGameManager] Starting player reset...");

        // Reset both players on server
        if (hostPlayer != null && hostPlayerSpawn != null)
        {
            Debug.Log($"[NetworkedGameManager] Resetting host from {hostPlayer.transform.position} to {hostPlayerSpawn.position}");
            ResetPlayerPosition(hostPlayer, hostPlayerSpawn);
        }
        else
        {
            Debug.LogWarning($"[NetworkedGameManager] Cannot reset host: hostPlayer={hostPlayer != null}, hostPlayerSpawn={hostPlayerSpawn != null}");
        }

        if (clientPlayer != null && clientPlayerSpawn != null)
        {
            Debug.Log($"[NetworkedGameManager] Resetting client from {clientPlayer.transform.position} to {clientPlayerSpawn.position}");
            ResetPlayerPosition(clientPlayer, clientPlayerSpawn);
        }
        else
        {
            Debug.LogWarning($"[NetworkedGameManager] Cannot reset client: clientPlayer={clientPlayer != null}, clientPlayerSpawn={clientPlayerSpawn != null}");
        }

        Debug.Log("[NetworkedGameManager] Players reset to spawn positions.");
    }

    /// <summary>
    /// Resets a player's position and velocity (server-side only)
    /// </summary>
    private void ResetPlayerPosition(GameObject player, Transform spawnPoint)
    {
        if (!IsServer || player == null || spawnPoint == null) return;

        // Get NetworkObject to ensure proper network synchronization
        NetworkObject netObj = player.GetComponent<NetworkObject>();

        // Disable CharacterController temporarily if present
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // Reset position and rotation
        player.transform.position = spawnPoint.position;
        player.transform.rotation = spawnPoint.rotation;

        // Reset velocity if using Rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Re-enable CharacterController
        if (cc != null)
        {
            cc.enabled = true;
        }

        // Force network transform update
        if (netObj != null)
        {
            // Trigger a ClientRpc to ensure all clients see the teleport
            TeleportPlayerClientRpc(netObj.NetworkObjectId, spawnPoint.position, spawnPoint.rotation);
        }

        Debug.Log($"[NetworkedGameManager] Reset player {netObj?.OwnerClientId} to spawn at {spawnPoint.position}");
    }

    /// <summary>
    /// ClientRpc to ensure position is updated on all clients
    /// </summary>
    [ClientRpc]
    private void TeleportPlayerClientRpc(ulong networkObjectId, Vector3 position, Quaternion rotation)
    {
        // Find the network object
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            GameObject player = netObj.gameObject;

            // Disable CharacterController temporarily if present
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            // Set position and rotation
            player.transform.position = position;
            player.transform.rotation = rotation;

            // Reset velocities
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Re-enable CharacterController
            if (cc != null)
            {
                cc.enabled = true;
            }

            Debug.Log($"[NetworkedGameManager] Client received teleport for player {netObj.OwnerClientId} to {position}");
        }
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

        // Instantiate server-side
        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);

        // MUST network spawn or clients won't see it
        NetworkObject netObj = newPickup.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }
        else
        {
            Debug.LogError("[NetworkedGameManager] Pickup prefab has NO NetworkObject!");
            Destroy(newPickup);
            return;
        }

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

    // ----- Round Over (show message, pause, reset players) -----
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

        // Start next round after brief delay
        StartCoroutine(StartNextRoundAfterDelay(2f));
    }

    private IEnumerator StartNextRoundAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        StartNextRound();
    }

    /// <summary>
    /// Starts the next round: removes balls, resets players, resumes game
    /// </summary>
    public void StartNextRound()
    {
        Debug.Log("[NetworkedGameManager] StartNextRound called");

        // Resume time FIRST so physics can update
        Time.timeScale = 1f;
        isPaused = false;

        if (IsServer)
        {
            // Remove all balls from the scene
            RemoveAllBalls();

            // Reset players after a brief moment
            StartCoroutine(ResetPlayersAfterFrame());
        }

        // Hide UI on all clients
        HideGameOverUIClientRpc();

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");

        Debug.Log("[NetworkedGameManager] Next round started.");
    }

    private IEnumerator ResetPlayersAfterFrame()
    {
        // Wait a frame for time scale to take effect
        yield return null;

        Debug.Log("[NetworkedGameManager] Executing player reset after frame delay");
        ResetPlayersToSpawn();
    }

    /// <summary>
    /// Removes all ball objects from the scene (server only)
    /// </summary>
    private void RemoveAllBalls()
    {
        if (!IsServer) return;

        // Find all objects tagged as "Ball"
        GameObject[] balls = GameObject.FindGameObjectsWithTag("Ball");
        foreach (GameObject ball in balls)
        {
            NetworkObject netObj = ball.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(ball);
            }
        }

        Debug.Log($"[NetworkedGameManager] Removed {balls.Length} balls from scene.");
    }

    [ClientRpc]
    private void HideGameOverUIClientRpc()
    {
        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        Debug.Log("[NetworkedGameManager] Game Over UI hidden on client.");
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

        // Draw player spawn points
        if (hostPlayerSpawn != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(hostPlayerSpawn.position, Vector3.one * 2f);
        }

        if (clientPlayerSpawn != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(clientPlayerSpawn.position, Vector3.one * 2f);
        }
    }

    // Round over (after a point)
    public void GameOverRound(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;

        // Reset players after delay
        StartCoroutine(ResetPlayersAfterDelay(2f));
    }

    private IEnumerator ResetPlayersAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        // Resume time first
        Time.timeScale = 1f;
        yield return null; // Wait one frame

        // Then reset players
        if (IsServer)
        {
            ResetPlayersToSpawn();
        }

        if (gameOverUI != null)
            gameOverUI.SetActive(false);
    }

    public void GameOverFinal(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;
    }
}