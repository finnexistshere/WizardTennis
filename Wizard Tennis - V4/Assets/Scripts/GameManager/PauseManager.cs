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

    private float pauseCooldown = 0.2f; // to prevent double-toggling
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

    private void Update()
    {
        // This script runs even when timeScale = 0
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (cooldownTimer <= 0f)
            {
                TogglePause();
                cooldownTimer = pauseCooldown;
            }
        }

        // cooldown uses unscaled time
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.unscaledDeltaTime;
    }

    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("Player");
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        TogglePause();
    }
}
