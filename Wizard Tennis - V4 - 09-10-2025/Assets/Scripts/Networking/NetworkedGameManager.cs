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

    [Header("DEV TOOLS")]
    public bool enableDevTools = true;
    public KeyCode spawnAllSpellsKey = KeyCode.F9;
    public KeyCode respawnAllSpellsKey = KeyCode.F10;
    public float devSpawnSpacing = 2.5f;

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
    public List<NetworkVariable<bool>> hostPickupBools;
    public NetworkList<bool> hostPickupBools2 = new NetworkList<bool>();

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

    // Network-synced pause state
    private NetworkVariable<bool> isPausedNetwork = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool localPauseMenuVisible = false;

    public PlayerInput playerInput;

    private bool pickupsUnlocked = false;

    // References to both players' spellcasting
    private NetworkedSpellcasting hostSpellcasting;
    private NetworkedSpellcasting clientSpellcasting;

    // References to player GameObjects
    private GameObject hostPlayer;
    private GameObject clientPlayer;

    public List<bool> boolsTest;

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

        // Subscribe to pause state changes
        isPausedNetwork.OnValueChanged += OnPauseStateChanged;

        // Find both players' spellcasting components and player objects
        StartCoroutine(FindSpellcastingReferences());
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from pause state changes
        isPausedNetwork.OnValueChanged -= OnPauseStateChanged;
    }

    private void OnPauseStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"[NetworkedGameManager] Pause state changed: {previousValue} -> {newValue}");

        if (newValue)
        {
            // Game is now paused
            ApplyPauseLocally();
        }
        else
        {
            // Game is now unpaused
            ApplyUnpauseLocally();
        }
    }

    private void ApplyPauseLocally()
    {
        localPauseMenuVisible = true;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");

        Debug.Log("[NetworkedGameManager] Local pause applied");
    }

    private void ApplyUnpauseLocally()
    {
        localPauseMenuVisible = false;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");

        Debug.Log("[NetworkedGameManager] Local unpause applied");
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
        if (!IsServer) return;

        pickupsUnlocked = false;
        Debug.Log("[NetworkedGameManager] Pickup spawning LOCKED (SERVER)");
    }

    /// <summary>
    /// Unlocks pickup spawning so new pickups can appear.
    /// </summary>
    public void RequestUnlockPickupSpawning()
    {
        if (IsServer)
        {
            UnlockPickupSpawningInternal();
        }
        else
        {
            UnlockPickupSpawningServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UnlockPickupSpawningServerRpc()
    {
        UnlockPickupSpawningInternal();
    }

    private void UnlockPickupSpawningInternal()
    {
        pickupsUnlocked = true;
        hostSpawnTimer = spawnInterval;
        clientSpawnTimer = spawnInterval;

        Debug.Log("[NetworkedGameManager] Pickup spawning UNLOCKED (SERVER)");
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

        // PAUSE INPUT - Available to ALL clients (host and non-host)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (enableDevTools && IsServer && Input.GetKeyDown(spawnAllSpellsKey))
        {
            Debug.Log("[DEV] Spawn ALL spell pickups triggered");
            SpawnAllPickupsForBothPlayers();
        }
        if (enableDevTools && IsServer && Input.GetKeyDown(respawnAllSpellsKey))
        {
            RemoveAllPickups();
            SpawnAllPickupsForBothPlayers();
        }

        if (tutorialPanel != null && tutorialPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                tutorialPanel.SetActive(false);
                RequestUnlockPickupSpawning();
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

    [ContextMenu("DEV / Spawn All Pickups")]
    private void DevSpawnAllPickups()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[DEV] Must be server to spawn pickups");
            return;
        }

        SpawnAllPickupsForBothPlayers();
    }

    private void SpawnAllPickupsForBothPlayers()
    {
        if (!IsServer) return;

        SpawnAllPickupsForSide(
            hostSpawnCenter,
            hostActivePickups,
            hostSpellcasting
        );

        SpawnAllPickupsForSide(
            clientSpawnCenter,
            clientActivePickups,
            clientSpellcasting
        );
    }

    private void SpawnAllPickupsForSide(
    Transform spawnCenter,
    List<GameObject> activePickups,
    NetworkedSpellcasting spellcasting
)
    {
        if (spawnCenter == null)
        {
            Debug.LogWarning("[DEV] Spawn center missing");
            return;
        }

        int index = 0;

        foreach (var entry in pickupWeights)
        {
            if (entry.prefab == null)
                continue;

            Vector3 offset = new Vector3(
                (index % 5) * devSpawnSpacing,
                0f,
                (index / 5) * devSpawnSpacing
            );

            Vector3 spawnPos = spawnCenter.position + offset;

            SpawnPickupDirect(entry.prefab, spawnPos, activePickups);
            index++;
        }

        Debug.Log($"[DEV] Spawned {index} pickups at {spawnCenter.name}");
    }

    private void SpawnPickupDirect(
    GameObject prefab,
    Vector3 position,
    List<GameObject> activePickups
)
    {
        if (prefab == null) return;

        var netObjPrefab = prefab.GetComponent<NetworkObject>();
        if (netObjPrefab == null)
        {
            Debug.LogError($"[DEV] Prefab '{prefab.name}' has no NetworkObject");
            return;
        }

        // Ensure prefab is registered
        bool registered = false;
        foreach (var p in NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs)
        {
            if (p.Prefab == prefab)
            {
                registered = true;
                break;
            }
        }

        if (!registered)
        {
            Debug.LogError($"[DEV] Prefab '{prefab.name}' is NOT registered");
            return;
        }

        GameObject pickup = Instantiate(prefab, position, Quaternion.identity);
        NetworkObject netObj = pickup.GetComponent<NetworkObject>();

        netObj.Spawn();
        activePickups.Add(pickup);
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

                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().fireball);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().ice);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().lightning);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().shadow);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().green);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().stone);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().chronos);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().gemini);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().pisces);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().jolly);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().blink);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().warp);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().tether);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().mud);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().gambit);
                //hostPickupBools.Add(hostPlayer.GetComponent<NetworkedPlayerCustomisation>().gorbino);

                hostPickupBools2 = hostPlayer.GetComponent<NetworkedPlayerCustomisation>().playerBools;
                for (int i = 0; i < hostPickupBools2.Count; i++)
                {
                    boolsTest.Add(hostPickupBools2[i]);
                    //Debug.Log("FUCK YOU NETCODE YOU SUCK I HATE YOU");
                }
            }
            else if (spell.OwnerClientId != 0 && clientSpellcasting == null)
            {
                clientSpellcasting = spell;
                clientPlayer = spell.gameObject;
                Debug.Log("[NetworkedGameManager] Client spellcasting found (Update).");
            }
        }

        // When both are found → lock and stop running forever
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

    /// <summary>
    /// Toggle pause - called locally by any client
    /// </summary>
    private void TogglePause()
    {
        if (isPausedNetwork.Value)
        {
            RequestResumeGame();
        }
        else
        {
            RequestPauseGame();
        }
    }

    /// <summary>
    /// Request pause from any client
    /// </summary>
    private void RequestPauseGame()
    {
        if (IsServer)
        {
            PauseGameInternal();
        }
        else
        {
            PauseGameServerRpc();
        }
    }

    /// <summary>
    /// Request resume from any client
    /// </summary>
    private void RequestResumeGame()
    {
        if (IsServer)
        {
            ResumeGameInternal();
        }
        else
        {
            ResumeGameServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PauseGameServerRpc()
    {
        PauseGameInternal();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResumeGameServerRpc()
    {
        ResumeGameInternal();
    }

    private void PauseGameInternal()
    {
        if (!IsServer) return;

        isPausedNetwork.Value = true;
        Debug.Log("[NetworkedGameManager] Game PAUSED (server)");
    }

    private void ResumeGameInternal()
    {
        if (!IsServer) return;

        isPausedNetwork.Value = false;
        Debug.Log("[NetworkedGameManager] Game RESUMED (server)");
    }

    void buttonClick(string buttonName)
    {
        switch (buttonName)
        {
            case "Resume":
                RequestResumeGame();
                break;
            case "Restart":
                RestartGame();
                break;
            case "Menu":
                QuitMenu();
                break;
            case "OS":
                QuitGame();
                break;
        }
    }

    // ----- Legacy methods for compatibility -----
    public void PauseGame()
    {
        RequestPauseGame();
    }

    public void ResumeGame()
    {
        RequestResumeGame();
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
        // STEP 1 – Find valid spawn position
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

        if (!validPos)
        {
            Debug.LogWarning("[NetworkedGameManager] Could not find valid spawn position after 20 attempts.");
            return;
        }

        // -------------------------------------------------------
        // STEP 2 – Choose a pickup *that is allowed*
        // -------------------------------------------------------
        GameObject prefab = GetWeightedPickupFiltered(spellcasting, activePickups);
        if (prefab == null)
        {
            Debug.Log("[NetworkedGameManager] No valid pickup to spawn (all spells already known or active).");
            return;
        }

        Debug.Log($"[NetworkedGameManager] Selected prefab: {prefab.name}");

        // -------------------------------------------------------
        // CRITICAL: Check if prefab is registered in NetworkManager
        // -------------------------------------------------------
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[NetworkedGameManager] NetworkManager.Singleton is null!");
            return;
        }

        var networkPrefab = prefab.GetComponent<NetworkObject>();
        if (networkPrefab == null)
        {
            Debug.LogError($"[NetworkedGameManager] Prefab '{prefab.name}' has no NetworkObject component!");
            return;
        }

        // Check if prefab is registered
        bool isRegistered = false;
        foreach (var registeredPrefab in networkManager.NetworkConfig.Prefabs.Prefabs)
        {
            if (registeredPrefab.Prefab == prefab)
            {
                isRegistered = true;
                break;
            }
        }

        if (!isRegistered)
        {
            Debug.LogError($"[NetworkedGameManager] ⚠️ PREFAB NOT REGISTERED: '{prefab.name}' is not in NetworkManager's Network Prefabs List!");
            Debug.LogError("[NetworkedGameManager] Add this prefab to NetworkManager -> NetworkConfig -> Network Prefabs List");
            return;
        }

        Debug.Log($"[NetworkedGameManager] ✓ Prefab '{prefab.name}' is registered. Attempting spawn...");

        // -------------------------------------------------------
        // STEP 3 – Instantiate & network spawn
        // -------------------------------------------------------
        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);

        var netObj = newPickup.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[NetworkedGameManager] Instantiated pickup missing NetworkObject!");
            Destroy(newPickup);
            return;
        }

        try
        {
            Debug.Log($"[NetworkedGameManager] Calling Spawn() on '{prefab.name}' at {spawnPos}");
            netObj.Spawn();
            activePickups.Add(newPickup);
            Debug.Log($"[NetworkedGameManager] ✓ Successfully spawned pickup '{prefab.name}' for player {spellcasting.OwnerClientId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkedGameManager] ❌ Exception spawning pickup: {e.Message}");
            Debug.LogError($"[NetworkedGameManager] Stack trace: {e.StackTrace}");
            Destroy(newPickup);
        }
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
                if (e.prefab != null && e.weight > 0 && hostPickupBools2[pickupWeights.IndexOf(e)])
                    workingWeights.Add(e);
            }
        }
        else if (pickupPrefabs != null && pickupPrefabs.Count > 0)
        {
            // fallback: use prefab's PickupEffect.SpawnWeight (converted to integer weight)
            foreach (var prefab in pickupPrefabs)
            {
                if (prefab == null || !hostPickupBools2[pickupPrefabs.IndexOf(prefab)]) continue;
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
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkedGameManager] StartNextRound called on client - ignoring");
            return;
        }

        // Start the coroutine version
        StartCoroutine(StartNextRoundCoroutine());
    }

    private IEnumerator StartNextRoundCoroutine()
    {
        Debug.Log("[NetworkedGameManager] StartNextRound called");

        // Resume time FIRST
        Time.timeScale = 1f;

        Debug.Log("[NetworkedGameManager] Time scale restored to 1");

        // Tell all clients to resume their time
        ResumeTimeClientRpc();

        // *** CRITICAL: Reset IK references BEFORE destroying ball ***
        ResetAllIKReferencesClientRpc();

        // Wait for IK reset to complete on all clients
        yield return new WaitForSeconds(0.15f);

        // Remove all balls (now IKs are ready to find the new one)
        RemoveAllBalls();

        // Lock pickup spawning for new round
        LockPickupSpawning();
        Debug.Log("[NetworkedGameManager] Pickup spawning locked for new round");

        // Remove all existing pickups
        RemoveAllPickups();

        // Clear all spells from all players' spellbooks
        ClearAllSpellbooksClientRpc();

        // Force-Unfreeze Players frozen by Ice
        if (NetworkedSpellEffects.Instance != null)
            NetworkedSpellEffects.Instance.ForceUnfreezeAllPlayers();

        // Reset rally count for new round
        if (NetworkedScoreManager.Instance != null)
        {
            NetworkedScoreManager.Instance.ResetRallyCountServerRpc();
            Debug.Log("[NetworkedGameManager] Rally count reset for new round");
        }

        // Reset collision tracker
        NetworkedCollisionTrackerBall tracker = FindObjectOfType<NetworkedCollisionTrackerBall>();
        if (tracker != null)
        {
            tracker.ResetTracking();
            Debug.Log("[NetworkedGameManager] Collision tracker reset");
        }

        // IMPORTANT: Reset barriers BEFORE setting serving state
        ResetBarriersForServingClientRpc();

        // Small delay to ensure barriers are reset before setting serving state
        yield return new WaitForSeconds(0.1f);

        // Set players to serving state
        SetPlayersToServingStateClientRpc();

        // Reset players after short delay
        StartCoroutine(ResetPlayersAfterDelay(0.1f));

        Debug.Log("[NetworkedGameManager] Next round started.");
    }

    /// <summary>
    /// Resets all IK controller ball references so they're ready to find the new ball
    /// </summary>
    [ClientRpc]
    private void ResetAllIKReferencesClientRpc()
    {
        Debug.Log($"[NetworkedGameManager-Client {NetworkManager.Singleton.LocalClientId}] Resetting all IK references");

        // Find all TwoHandIKController components (player IKs)
        TwoHandIKController[] allPlayerIKs = FindObjectsOfType<TwoHandIKController>();

        foreach (TwoHandIKController ik in allPlayerIKs)
        {
            if (ik != null)
            {
                ik.ResetBallReference();
                Debug.Log($"[NetworkedGameManager-Client] Reset IK reference for {ik.name}");
            }
        }

        // Find all TwoHandIKController_Opponent components (opponent IKs - if any)
        TwoHandIKController_Opponent[] allOpponentIKs = FindObjectsOfType<TwoHandIKController_Opponent>();

        foreach (TwoHandIKController_Opponent ik in allOpponentIKs)
        {
            if (ik != null)
            {
                // Opponent IK uses a different method name
                ik.StartAssignBallCoroutine();
                Debug.Log($"[NetworkedGameManager-Client] Reset opponent IK reference for {ik.name}");
            }
        }

        Debug.Log($"[NetworkedGameManager-Client] All IK references reset and ready to find new ball");
    }

    private IEnumerator SetServingStateAfterBarrierReset()
    {
        // Wait a frame for barriers to be reset
        yield return new WaitForSeconds(0.1f);

        // Now set players to serving state
        SetPlayersToServingStateClientRpc();
    }

    /// <summary>
    /// Removes all pickup objects from the scene (server only)
    /// </summary>
    private void RemoveAllPickups()
    {
        if (!IsServer) return;

        int pickupCount = 0;

        // Method 1: Remove from tracked lists
        foreach (GameObject pickup in hostActivePickups)
        {
            if (pickup != null)
            {
                NetworkObject netObj = pickup.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                    pickupCount++;
                }
                else
                {
                    Destroy(pickup);
                    pickupCount++;
                }
            }
        }
        hostActivePickups.Clear();

        foreach (GameObject pickup in clientActivePickups)
        {
            if (pickup != null)
            {
                NetworkObject netObj = pickup.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                    pickupCount++;
                }
                else
                {
                    Destroy(pickup);
                    pickupCount++;
                }
            }
        }
        clientActivePickups.Clear();

        // Method 2: Find any remaining pickups by tag (safety net)
        GameObject[] remainingPickups = GameObject.FindGameObjectsWithTag("Pickup");
        foreach (GameObject pickup in remainingPickups)
        {
            NetworkObject netObj = pickup.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
                pickupCount++;
            }
            else
            {
                Destroy(pickup);
                pickupCount++;
            }
        }

        // Method 3: Find by PickupEffect component (final safety net)
        PickupEffect[] pickupEffects = FindObjectsOfType<PickupEffect>();
        foreach (PickupEffect effect in pickupEffects)
        {
            if (effect != null && effect.gameObject != null)
            {
                NetworkObject netObj = effect.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                    pickupCount++;
                }
                else
                {
                    Destroy(effect.gameObject);
                    pickupCount++;
                }
            }
        }

        Debug.Log($"[NetworkedGameManager] Removed {pickupCount} pickups from scene.");
    }

    /// <summary>
    /// Clears all spells from all players' spellbooks across all clients
    /// </summary>
    [ClientRpc]
    private void ClearAllSpellbooksClientRpc()
    {
        Debug.Log("[NetworkedGameManager-Client] Clearing all spellbooks");

        // Find all NetworkedSpellcasting components
        NetworkedSpellcasting[] allSpellcasters = FindObjectsOfType<NetworkedSpellcasting>();

        foreach (NetworkedSpellcasting spellcaster in allSpellcasters)
        {
            if (spellcaster != null && spellcaster.spellBook != null)
            {
                // Clear all dictionaries
                spellcaster.spellBook.Clear();
                spellcaster.debuffBook.Clear();
                spellcaster.spellVisuals.Clear();
                spellcaster.boolBook.Clear();
                spellcaster.spellColors.Clear();
                spellcaster.spellColors2.Clear();
                spellcaster.spellAudio.Clear();
                spellcaster.wizardAudio.Clear();
                spellcaster.spellDurations.Clear();

                Debug.Log($"[NetworkedGameManager-Client] Cleared spellbook for player {spellcaster.OwnerClientId}");
            }
        }

        Debug.Log("[NetworkedGameManager-Client] All spellbooks cleared");
    }

    [ClientRpc]
    private void ResumeTimeClientRpc()
    {
        Time.timeScale = 1f;

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");

        Debug.Log("[NetworkedGameManager-Client] Time resumed");
    }

    [ClientRpc]
    private void ResetBarriersForServingClientRpc()
    {
        Debug.Log($"[NetworkedGameManager-Client {NetworkManager.Singleton.LocalClientId}] Resetting barriers for serving");

        // Find all NetworkedBall components
        NetworkedBall[] allBalls = FindObjectsOfType<NetworkedBall>();

        foreach (NetworkedBall ball in allBalls)
        {
            // Each client handles their own barriers
            if (ball.IsOwner && ball.servingBarriers != null)
            {
                ball.servingBarriers.SetActive(true);
                Debug.Log($"[NetworkedGameManager-Client] Enabled barriers for owned ball");
            }
        }
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
        StartNextRound();
        ResumeGame();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScores();

        Time.timeScale = 1f;
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

        // Hard disconnect from Steam + Netcode
        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.LeaveLobbyAndShutdownNetwork();
        }
        else
        {
            Debug.LogWarning("SteamLobbyManager missing during QuitMenu");
        }

        // Safety reset
        Time.timeScale = 1f;

        // Scene load LAST
        SceneManager.LoadScene("Main Menu");
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
        Debug.Log($"[NetworkedGameManager-Client {NetworkManager.Singleton.LocalClientId}] Setting local players to serving");

        // Each client finds THEIR OWN NetworkedBall components
        NetworkedBall[] localBalls = FindObjectsOfType<NetworkedBall>();

        foreach (NetworkedBall ball in localBalls)
        {
            // Only modify balls we own
            if (ball.IsOwner)
            {
                ball.SetToServingState();
                Debug.Log($"[NetworkedGameManager-Client] Set ball to SERVING state");

                // CRITICAL: Explicitly enable barriers here too
                if (ball.servingBarriers != null)
                {
                    ball.servingBarriers.SetActive(true);
                    Debug.Log($"[NetworkedGameManager-Client] Explicitly enabled barriers for owned ball");
                }
            }
        }
    }

    // Need to call this Via a button, can't be arsed so it's just here for now until I can be bothered to add more devtools
    [ContextMenu("Debug Barrier State")]
    public void DebugBarrierState()
    {
        NetworkedBall[] allBalls = FindObjectsOfType<NetworkedBall>();

        Debug.Log($"=== BARRIER STATE DEBUG (Client {NetworkManager.Singleton.LocalClientId}) ===");

        foreach (NetworkedBall ball in allBalls)
        {
            Debug.Log($"Ball Owner: {ball.OwnerClientId}");
            Debug.Log($"  - IsOwner: {ball.IsOwner}");
            Debug.Log($"  - servingBarriers: {(ball.servingBarriers != null ? ball.servingBarriers.name : "NULL")}");
            Debug.Log($"  - barriers active: {(ball.servingBarriers != null ? ball.servingBarriers.activeSelf.ToString() : "N/A")}");
            Debug.Log($"  - barriers enabled: {(ball.servingBarriers != null ? ball.servingBarriers.activeInHierarchy.ToString() : "N/A")}");
        }

        Debug.Log($"=== END BARRIER STATE DEBUG ===");
    }

    [ContextMenu("Debug Pause State")]
    public void DebugPauseState()
    {
        Debug.Log($"=== PAUSE STATE DEBUG ===");
        Debug.Log($"  IsServer: {IsServer}");
        Debug.Log($"  IsHost: {IsHost}");
        Debug.Log($"  isPausedNetwork.Value: {isPausedNetwork.Value}");
        Debug.Log($"  localPauseMenuVisible: {localPauseMenuVisible}");
        Debug.Log($"  pauseMenuUI active: {(pauseMenuUI != null ? pauseMenuUI.activeSelf.ToString() : "NULL")}");
        Debug.Log($"  Time.timeScale: {Time.timeScale}");
        Debug.Log($"=== END PAUSE DEBUG ===");
    }
}