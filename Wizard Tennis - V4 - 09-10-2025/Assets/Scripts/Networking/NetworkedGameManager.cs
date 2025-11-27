using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

    private NetworkedSpellcasting hostSpellcasting;
    private NetworkedSpellcasting clientSpellcasting;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LockPickupSpawning();
        Time.timeScale = 1f;

        hostSpawnCenter ??= GameObject.Find("HostSpawnCenter")?.transform;
        clientSpawnCenter ??= GameObject.Find("ClientSpawnCenter")?.transform;

        pauseMenuUI ??= GameObject.Find("PauseMenu");
        pauseMenuUI?.SetActive(false);

        gameOverUI ??= GameObject.Find("GameOverMenu");
        gameOverUI?.SetActive(false);

        playerInput?.SwitchCurrentActionMap("Player");

        ScoreManager.Instance?.LoadSavedScores();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer) Debug.Log("[NetworkedGameManager] Server initialized.");
        StartCoroutine(FindSpellcastingReferences());
    }

    private IEnumerator FindSpellcastingReferences()
    {
        yield return new WaitForSeconds(0.5f);

        var allSpellcasting = FindObjectsOfType<NetworkedSpellcasting>();
        foreach (var spell in allSpellcasting)
        {
            if (spell.OwnerClientId == 0) hostSpellcasting = spell;
            else if (spell.OwnerClientId == 1) clientSpellcasting = spell;
        }

        Debug.Log(hostSpellcasting != null ? "[GameManager] Found host spellcasting." : "[GameManager] Host spellcasting not found!");
        Debug.Log(clientSpellcasting != null ? "[GameManager] Found client spellcasting." : "[GameManager] Client spellcasting not found!");
    }

    private void Start()
    {
        hostSpawnTimer = spawnInterval;
        clientSpawnTimer = spawnInterval;

        if (pauseMenuUI != null)
        {
            foreach (var btn in pauseMenuUI.GetComponentsInChildren<Button>())
            {
                string name = btn.name;
                btn.onClick.AddListener(() => ButtonClick(name));
            }
        }
    }

    private void Update()
    {
        if (tutorialPanel != null && tutorialPanel.activeSelf && Input.GetKeyDown(KeyCode.E))
            tutorialPanel.SetActive(false);

        if (!IsServer || !pickupsUnlocked) return;

        hostActivePickups.RemoveAll(p => p == null);
        clientActivePickups.RemoveAll(p => p == null);

        hostSpawnTimer -= Time.deltaTime;
        if (hostSpawnTimer <= 0f && hostActivePickups.Count < maxActivePickupsPerPlayer)
        {
            SpawnPickupForPlayer(hostSpawnCenter, hostActivePickups, hostSpellcasting);
            hostSpawnTimer = spawnInterval;
        }

        clientSpawnTimer -= Time.deltaTime;
        if (clientSpawnTimer <= 0f && clientActivePickups.Count < maxActivePickupsPerPlayer)
        {
            SpawnPickupForPlayer(clientSpawnCenter, clientActivePickups, clientSpellcasting);
            clientSpawnTimer = spawnInterval;
        }
    }

    private void ButtonClick(string buttonName)
    {
        switch (buttonName)
        {
            case "Resume": ResumeGame(); break;
            case "Restart": RestartGame(); break;
            case "Menu": QuitMenu(); break;
            case "OS": QuitGame(); break;
        }
    }

    public void LockPickupSpawning() => pickupsUnlocked = false;
    public void UnlockPickupSpawning() { pickupsUnlocked = true; hostSpawnTimer = spawnInterval; clientSpawnTimer = spawnInterval; }

    private void SpawnPickupForPlayer(Transform spawnCenter, List<GameObject> activePickups, NetworkedSpellcasting spellcasting)
    {
        if (!IsServer || spawnCenter == null || spellcasting == null) return;

        Vector3 spawnPos = Vector3.zero;
        bool valid = false;
        int attempts = 0;

        while (!valid && attempts < 20)
        {
            attempts++;
            Vector2 rnd = Random.insideUnitCircle * spawnRadius;
            spawnPos = spawnCenter.position + new Vector3(rnd.x, 0f, rnd.y);

            bool tooClose = false;
            foreach (GameObject pickup in activePickups)
            {
                if (pickup != null && Vector3.Distance(pickup.transform.position, spawnPos) < 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) valid = true;
        }

        if (!valid) return;

        GameObject prefab = GetWeightedPickup(spellcasting);
        if (prefab == null) return;

        GameObject newPickup = Instantiate(prefab, spawnPos, Quaternion.identity);
        activePickups.Add(newPickup);

        Debug.Log($"[GameManager] Spawned pickup at {spawnPos} for player {spellcasting.OwnerClientId}");
    }

    private GameObject GetWeightedPickup(NetworkedSpellcasting spellcasting)
    {
        if (spellcasting == null) return null;
        List<GameObject> valid = new List<GameObject>();
        List<float> weights = new List<float>();

        foreach (var prefab in pickupPrefabs)
        {
            var effect = prefab.GetComponent<PickupEffect>();
            if (effect == null) continue;
            if (!spellcasting.spellBook.ContainsKey(effect.SpellAddress))
            {
                valid.Add(prefab);
                weights.Add(Mathf.Max(0, effect.spawnWeight));
            }
        }

        if (valid.Count == 0) return null;

        float total = 0f;
        foreach (float w in weights) total += w;

        if (total <= 0f) return valid[Random.Range(0, valid.Count)];

        float point = Random.value * total;
        for (int i = 0; i < valid.Count; i++)
        {
            if (point < weights[i]) return valid[i];
            point -= weights[i];
        }
        return valid[Random.Range(0, valid.Count)];
    }

    // ------------------------- PAUSE/RESUME -------------------------
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        playerInput?.SwitchCurrentActionMap("UI");
        pauseMenuUI?.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        playerInput?.SwitchCurrentActionMap("Player");
        pauseMenuUI?.SetActive(false);
    }

    // ------------------------- ROUND RESET -------------------------
    public void ResetRound()
    {
        if (!IsServer) return;
        StartCoroutine(ResetRoundRoutine());
    }

    // Called by NetworkedCollisionTracker when a round ends
    public void RoundOver(string message)
    {
        // You can reuse ResetRoundClientRpc() or implement custom logic if needed
        GameOverRound(message);
    }

    // Called by NetworkedScoreManager for a round-ending message
    public void GameOverRound(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;
    }

    // Called by NetworkedScoreManager for the final game over message
    public void GameOverFinal(string message)
    {
        Time.timeScale = 0f;
        if (gameOverUI != null) gameOverUI.SetActive(true);
        if (WinLoseText != null) WinLoseText.text = message;
    }

    private IEnumerator ResetRoundRoutine()
    {
        yield return new WaitForSecondsRealtime(1f);
        Time.timeScale = 1f;

        // Remove all balls
        foreach (var ball in GameObject.FindGameObjectsWithTag("Ball"))
        {
            var netObj = ball.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
            else Destroy(ball);
        }

        // Reset players
        ResetAllPlayerPositionsServerRpc();

        // Set both players to serving
        SetPlayersToServingServerRpc();


        // Reset pickups
        foreach (var p in hostActivePickups) if (p != null) Destroy(p);
        hostActivePickups.Clear();
        foreach (var p in clientActivePickups) if (p != null) Destroy(p);
        clientActivePickups.Clear();

        hostSpawnTimer = spawnInterval;
        clientSpawnTimer = spawnInterval;
        pickupsUnlocked = true;

        // Reset spells
        hostSpellcasting?.ResetForNewRound();
        clientSpellcasting?.ResetForNewRound();

        // Reset UI
        ResetRoundClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayersToServingServerRpc()
    {
        SetPlayersToServingClientRpc();
    }

    [ClientRpc]
    private void SetPlayersToServingClientRpc()
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(localId))
            return;

        var netObj = NetworkManager.Singleton.ConnectedClients[localId].PlayerObject;
        if (netObj == null) return;

        var ball = netObj.GetComponent<NetworkedBall>();
        if (ball == null) return;

        // Force serving state
        ball.SetToServingState();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetAllPlayerPositionsServerRpc()
    {
        foreach (var kvp in NetworkPlayerSpawner_Better.SpawnedPlayers)
        {
            ulong clientId = kvp.Key;
            if (!NetworkPlayerSpawner_Better.PlayerSpawnPoints.TryGetValue(clientId, out Transform spawn)) continue;

            ResetPlayerPositionClientRpc(spawn.position, spawn.rotation, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
            });
        }
    }

    [ClientRpc]
    public void ResetPlayerPositionClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        // Identify which player this RPC was sent TO
        ulong playerId = NetworkManager.Singleton.LocalClientId;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(playerId))
            return;

        NetworkObject netObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
        if (netObj == null) return;

        GameObject player = netObj.gameObject;

        player.transform.SetPositionAndRotation(position, rotation);

        if (player.TryGetComponent<CharacterController>(out var controller))
        {
            controller.enabled = false;
            controller.enabled = true;
            controller.Move(Vector3.zero);
        }

        if (player.TryGetComponent<MainCharacterMovement>(out var movement))
        {
            movement.ForceMovementRefresh();
            movement.Nudge(Vector3.zero);
        }

        Debug.Log($"[RPC] Reset position for player {playerId} ? {position}");
    }

    [ClientRpc]
    private void ResetRoundClientRpc()
    {
        Time.timeScale = 1f;
        gameOverUI?.SetActive(false);
        pauseMenuUI?.SetActive(false);
        playerInput?.SwitchCurrentActionMap("Player");
    }

    // ------------------------- UI / SCENE -------------------------
    public void RestartGame()
    {
        ScoreManager.Instance?.ResetScores();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitMenu()
    {
        ScoreManager.Instance?.ResetScores();
        NetworkManager.Singleton?.Shutdown();
        SceneManager.LoadScene("Main Menu");
        Time.timeScale = 1f;
    }

    public void QuitGame() => Application.Quit();
}
