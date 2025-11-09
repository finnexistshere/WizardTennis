using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuUI;

    [Header("Settings")]
    [SerializeField] private PlayerInput playerInput;

    public bool IsPaused { get; private set; }

    [Header("Timing")]
    [SerializeField] private float pauseCooldown = 0.3f; // Slightly shorter feels snappier
    private float cooldownTimer = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (pauseMenuUI == null)
            pauseMenuUI = GameObject.Find("PauseMenu");

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();
    }

    private void OnEnable()
    {
        // Always listen to Pause, regardless of current action map
        if (playerInput != null && playerInput.actions != null)
        {
            var pauseAction = playerInput.actions["Pause"];
            if (pauseAction != null)
                pauseAction.performed += OnPause;
        }
    }

    private void OnDisable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            var pauseAction = playerInput.actions["Pause"];
            if (pauseAction != null)
                pauseAction.performed -= OnPause;
        }
    }

    private void Update()
    {
        // Cooldown uses unscaled time (so it still counts while paused)
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.unscaledDeltaTime;
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // Debug helps you confirm which map is active
        Debug.Log($"Pause pressed! (current map: {playerInput.currentActionMap.name})");

        if (cooldownTimer > 0f) return;

        TogglePause();
        cooldownTimer = pauseCooldown;
    }

    public void TogglePause()
    {
        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");

        Debug.Log("Game paused — switched to UI map.");
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");

        Debug.Log("Game resumed — switched to Player map.");
    }
}
