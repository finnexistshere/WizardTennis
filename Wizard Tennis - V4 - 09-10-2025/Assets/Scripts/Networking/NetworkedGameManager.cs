using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;

public class NetworkedGameManager : NetworkBehaviour
{
    public static NetworkedGameManager Instance;

    [Header("Pickup Settings")]
    public List<GameObject> pickupPrefabs;
    public float spawnInterval = 7f;
    public float spawnRadius = 25f;
    public int maxActivePickupsPerPlayer = 4;
    [System.Serializable]
    public struct WeightEntry
    {
        public GameObject prefab;
        public int weight;
    }
    [Tooltip("List of pickup prefabs and their spawn weights.")]
    public List<WeightEntry> pickupWeights = new List<WeightEntry>();

    // --------------------------------------------------------------------

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

    private bool playersFound = false;
    private float findPlayerTimeout = 10f; // safety
    private float findPlayerTimer = 0f;

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
            else if (spell.OwnerClientId != 0) // Any non-host player
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
    /// Resets both players to their spawn positions (server only)
    /// </summary>
    public void ResetPlayersToSpawn()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkedGameManager] ResetPlayersToSpawn called on client - only server can reset.");
            return;
        }

        Debug.Log("[NetworkedGameManager] Starting player reset...");

        // Try to find players if references are null
        if (hostPlayer == null || clientPlayer == null)
        {
            Debug.Log("[NetworkedGameManager] Player references null, attempting to find...");
            TryFindPlayers();
        }

        if (hostPlayer != null && hostPlayerSpawn != null)
        {
            Debug.Log($"[NetworkedGameManager] Teleporting host to {hostPlayerSpawn.position}");
            TeleportHostClientRpc(hostPlayerSpawn.position, hostPlayerSpawn.rotation);
        }
        else
        {
            Debug.LogWarning($"[NetworkedGameManager] Cannot teleport host - player: {hostPlayer != null}, spawn: {hostPlayerSpawn != null}");
        }

        if (clientPlayer != null && clientPlayerSpawn != null)
        {
            Debug.Log($"[NetworkedGameManager] Teleporting client to {clientPlayerSpawn.position}");
            TeleportClientClientRpc(clientPlayerSpawn.position, clientPlayerSpawn.rotation);
        }
        else
        {
            Debug.LogWarning($"[NetworkedGameManager] Cannot teleport client - player: {clientPlayer != null}, spawn: {clientPlayerSpawn != null}");
        }
    }

    [ClientRpc]
    private void TeleportHostClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[NetworkedGameManager-Client] TeleportHost RPC received - IsHost: {IsHost}");

        if (!IsHost) return; // only execute on host

        GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject?.gameObject;

        if (player == null)
        {
            Debug.LogError("[NetworkedGameManager-Client] Host player object is null!");
            return;
        }

        Debug.Log($"[NetworkedGameManager-Client] Teleporting host player to {position}");

        // Disable character controller
        CharacterController cc = player.GetComponent<CharacterController>();
        MainCharacterMovement mcm = player.GetComponent<MainCharacterMovement>();

        if (cc != null) cc.enabled = false;
        if (mcm != null) mcm.enabled = false;

        // Teleport
        player.transform.position = position;
        player.transform.rotation = rotation;

        // Reset physics
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Re-enable
        if (cc != null) cc.enabled = true;
        if (mcm != null)
        {
            mcm.enabled = true;
            mcm.ForceMovementRefresh();
        }

        Debug.Log($"[NetworkedGameManager-Client] Host teleport complete");
    }

    [ClientRpc]
    private void TeleportClientClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[NetworkedGameManager-Client] TeleportClient RPC received - IsHost: {IsHost}");

        if (IsHost) return; // only execute on non-host clients

        GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject?.gameObject;

        if (player == null)
        {
            Debug.LogError("[NetworkedGameManager-Client] Client player object is null!");
            return;
        }

        Debug.Log($"[NetworkedGameManager-Client] Teleporting client player to {position}");

        // Disable character controller
        CharacterController cc = player.GetComponent<CharacterController>();
        MainCharacterMovement mcm = player.GetComponent<MainCharacterMovement>();

        if (cc != null) cc.enabled = false;
        if (mcm != null) mcm.enabled = false;

        // Teleport
        player.transform.position = position;
        player.transform.rotation = rotation;

        // Reset physics
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Re-enable
        if (cc != null) cc.enabled = true;
        if (mcm != null)
        {
            mcm.enabled = true;
            mcm.ForceMovementRefresh();
        }

        Debug.Log($"[NetworkedGameManager-Client] Client teleport complete");
    }

    private IEnumerator ResetPlayersAfterDelay(float delay)
    {
        Debug.Log($"[NetworkedGameManager] Waiting {delay}s before reset...");

        // Use realtime so it works even if timeScale was 0
        yield return new WaitForSecondsRealtime(delay);

        Debug.Log("[NetworkedGameManager] Delay complete, resetting players...");

        if (IsServer)
        {
            ResetPlayersToSpawn();
            Debug.Log("[NetworkedGameManager] Players reset complete");
        }

        // Hide UI for all clients
        HideGameOverUIClientRpc();
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
        if (IsServer && !playersFound)
        {
            TryFindPlayers();
        }

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

    private void TryFindPlayers()
    {
        findPlayerTimer += Time.deltaTime;

        var allSpellcasting = FindObjectsOfType<NetworkedSpellcasting>();

        foreach (var spell in allSpellcasting)
        {
            if (spell.OwnerClientId == 0 && hostSpellcasting == null)
            {
                hostSpellcasting = spell;
                hostPlayer = spell.gameObject;
                Debug.Log("[NetworkedGameManager] Host spellcasting found (Update).");
            }
            else if (spell.OwnerClientId != 0 && clientSpellcasting == null)
            {
                clientSpellcasting = spell;
                clientPlayer = spell.gameObject;
                Debug.Log("[NetworkedGameManager] Client spellcasting found (Update).");
            }
        }

        // When both are found ? lock and stop running forever
        if (hostSpellcasting != null && clientSpellcasting != null)
        {
            playersFound = true;
            Debug.Log("[NetworkedGameManager] All player references locked.");
        }

        // Optional: failsafe (to avoid infinite loop in broken cases)
        if (findPlayerTimer > findPlayerTimeout)
        {
            playersFound = true;
            Debug.LogWarning("[NetworkedGameManager] Timeout while searching for players. Locking search.");
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

    private void SpawnPickupForPlayer
    (
        Transform spawnCenter,
        List<GameObject> activePickups,
        NetworkedSpellcasting spellcasting
    )
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

        // -------------------------------------------------------
        // STEP 1 � Find valid spawn position
        // -------------------------------------------------------
        Vector3 spawnPos = Vector3.zero;
        bool validPos = false;
        int attempts = 0;

        while (!validPos && attempts < 20)
        {
            attempts++;

            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            spawnPos = spawnCenter.position +
                       new Vector3(randomCircle.x, 0f, randomCircle.y);

            bool tooClose = false;

            foreach (GameObject p in activePickups)
            {
                if (p != null && Vector3.Distance(p.transform.position, spawnPos) < 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                validPos = true;
        }

        if (!validPos) return;

        // -------------------------------------------------------
        // STEP 2 � Choose a pickup *that is allowed*
        // -------------------------------------------------------
        GameObject prefab = GetWeightedPickupFiltered(spellcasting, activePickups);
        if (prefab == null)
        {
            Debug.Log("[NetworkedGameManager] No valid pickup to spawn (all spells already known or active).");
            return;
        }

        // -------------------------------------------------------
        // STEP 3 � Instantiate & network spawn
        // -------------------------------------------------------
        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);

        var netObj = newPickup.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[NetworkedGameManager] Pickup prefab missing NetworkObject!");
            Destroy(newPickup);
            return;
        }

        netObj.Spawn();
        activePickups.Add(newPickup);

        Debug.Log($"[NetworkedGameManager] Spawned pickup '{prefab.name}' for player {spellcasting.OwnerClientId}");
    }


    private GameObject GetWeightedPickupFiltered(
        NetworkedSpellcasting spellcasting,
        List<GameObject> activePickups
    )
    {
        // Build a working weight list. If pickupWeights is empty, fall back to pickupPrefabs with weight from PickupEffect.SpawnWeight.
        List<WeightEntry> workingWeights = new List<WeightEntry>();

        if (pickupWeights != null && pickupWeights.Count > 0)
        {
            // copy only entries that have a valid prefab and weight > 0
            foreach (var e in pickupWeights)
            {
                if (e.prefab != null && e.weight > 0)
                    workingWeights.Add(e);
            }
        }
        else if (pickupPrefabs != null && pickupPrefabs.Count > 0)
        {
            // fallback: use prefab's PickupEffect.SpawnWeight (converted to integer weight)
            foreach (var prefab in pickupPrefabs)
            {
                if (prefab == null) continue;
                var effect = prefab.GetComponent<PickupEffect>();
                if (effect == null) continue;

                // Attempts to read SpawnWeight property; requires PickupEffect to expose it as public float SpawnWeight.
                float spawnW = 0.0f;
                try
                {
                    spawnW = effect.SpawnWeight;
                }
                catch
                {
                    spawnW = 0.2f; // fallback default
                }

                int intWeight = Mathf.RoundToInt(Mathf.Clamp01(spawnW) * 10f); // scale 0..1 to 0..10
                if (intWeight <= 0) intWeight = 1; // ensure minimum weight for fallback prefabs

                WeightEntry we = new WeightEntry { prefab = prefab, weight = intWeight };
                workingWeights.Add(we);
            }
        }

        if (workingWeights.Count == 0)
        {
            Debug.LogWarning("[NetworkedGameManager] No pickup prefabs / weights available to choose from.");
            return null;
        }

        // Build weighted pool of prefabs that pass filters
        List<GameObject> pool = new List<GameObject>();

        foreach (var entry in workingWeights)
        {
            GameObject prefab = entry.prefab;
            if (prefab == null) continue;

            var effect = prefab.GetComponent<PickupEffect>();
            if (effect == null) continue;

            string spellAddress = effect.SpellAddress;
            if (string.IsNullOrEmpty(spellAddress)) continue;

            // Skip if player already owns this spell
            if (spellcasting != null && spellcasting.spellBook != null && spellcasting.spellBook.ContainsKey(spellAddress))
                continue;

            // Skip if there's already an active pickup on this side with same spell
            bool alreadyActive = false;
            foreach (var p in activePickups)
            {
                if (p == null) continue;
                var existing = p.GetComponent<PickupEffect>();
                if (existing != null && existing.SpellAddress == spellAddress)
                {
                    alreadyActive = true;
                    break;
                }
            }
            if (alreadyActive) continue;

            // Add to pool according to weight (treat <=0 as skipped)
            int w = Mathf.Max(0, entry.weight);
            for (int i = 0; i < w; i++)
                pool.Add(prefab);
        }

        if (pool.Count == 0)
        {
            // Helpful log to show why pool is empty
            Debug.Log("[NetworkedGameManager] No valid pickup prefabs after filtering (owned or already active).");
            return null;
        }

        int idx = Random.Range(0, pool.Count);
        return pool[idx];
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
    }

    private IEnumerator StartNextRoundAfterDelay(float delay)
    {
        Debug.Log($"[NetworkedGameManager] Waiting {delay}s before starting next round...");

        yield return new WaitForSecondsRealtime(delay);

        Debug.Log("[NetworkedGameManager] Delay complete, calling StartNextRound");

        if (IsServer)
        {
            StartNextRound();
        }
    }

    public void StartNextRound()
    {
        Debug.Log("[NetworkedGameManager] StartNextRound called");

        // CRITICAL: Only server should execute the main logic
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkedGameManager] StartNextRound called on client - ignoring");
            return;
        }

        // Resume time FIRST (so coroutines work)
        Time.timeScale = 1f;
        isPaused = false;

        Debug.Log("[NetworkedGameManager] Time scale restored to 1");

        // Tell all clients to resume their time
        ResumeTimeClientRpc();

        // Remove all balls (server only)
        RemoveAllBalls();

        // Reset players after short delay
        StartCoroutine(ResetPlayersAfterDelay(0.1f));

        // Set all players' balls back to serving
        SetPlayersToServingStateClientRpc();

        Debug.Log("[NetworkedGameManager] Next round started.");
    }

    [ClientRpc]
    private void ResumeTimeClientRpc()
    {
        Time.timeScale = 1f;
        isPaused = false;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");

        Debug.Log("[NetworkedGameManager-Client] Time resumed");
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

    public void GameOverRound(string message)
    {
        Debug.Log($"[NetworkedGameManager] GameOverRound: {message}");

        // Show UI on all clients
        ShowGameOverUIClientRpc(message);

        // After 3 seconds, start next round (server only)
        if (IsServer)
        {
            StartCoroutine(StartNextRoundAfterDelay(3f));
        }
    }

    public void GameOverFinal(string message)
    {
        Debug.Log($"[NetworkedGameManager] GameOverFinal: {message}");

        // Show UI on all clients
        ShowGameOverUIClientRpc(message);

        // Don't auto-restart - wait for player input
    }

    // Add this new ClientRpc to show game over UI
    [ClientRpc]
    private void ShowGameOverUIClientRpc(string message)
    {
        Time.timeScale = 0f;
        isPaused = true;

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        if (WinLoseText != null)
            WinLoseText.text = message;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");

        Debug.Log($"[NetworkedGameManager-Client] Game over UI shown: {message}");
    }

    [ClientRpc]
    private void SetPlayersToServingStateClientRpc()
    {
        Debug.Log($"[NetworkedGameManager-Client {NetworkManager.Singleton.LocalClientId}] Setting local balls to serving");

        // Each client finds THEIR OWN NetworkedBall components
        NetworkedBall[] localBalls = FindObjectsOfType<NetworkedBall>();

        foreach (NetworkedBall ball in localBalls)
        {
            // Only modify balls we own
            if (ball.IsOwner)
            {
                ball.SetToServingState();
                Debug.Log($"[NetworkedGameManager-Client] Set ball to SERVING state");
            }
        }
    }
}
