using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneInitializer : MonoBehaviour
{
    [Header("Menu Camera")]
    public Camera menuCamera;
    public Transform mainMenuCamPos;

    [Header("UI Panels")]
    public GameObject MainMenu;
    public GameObject CreditsMenu;
    public GameObject OptionsMenu;

    [Header("Optional Shader / Ambient References")]
    public Behaviour[] ambientShaders; // assign your scene-specific shader scripts here

    [Header("Ambient Shader Objects")]
    public Renderer ambientRenderer; // assign in Inspector

    private void ResetShaders()
    {
        if (ambientRenderer != null)
        {
            // Assign a new instance to avoid stale references
            ambientRenderer.material = new Material(ambientRenderer.sharedMaterial);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindReferences();
        ResetCamera();
        ResetUI();
        ResetShaders();
    }

    private void RebindReferences()
    {
        // Camera
        if (menuCamera == null)
            menuCamera = Camera.main;

        // Camera target positions
        if (mainMenuCamPos == null)
        {
            GameObject posObj = GameObject.Find("MainCamPos");
            if (posObj != null) mainMenuCamPos = posObj.transform;
        }

        // UI Panels
        if (MainMenu == null) MainMenu = GameObject.Find("MainMenu");
        if (CreditsMenu == null) CreditsMenu = GameObject.Find("CreditsMenu");
        if (OptionsMenu == null) OptionsMenu = GameObject.Find("OptionsMenu");
    }

    private void ResetCamera()
    {
        if (menuCamera != null && mainMenuCamPos != null)
        {
            menuCamera.transform.position = mainMenuCamPos.position;
            menuCamera.transform.rotation = mainMenuCamPos.rotation;
        }
    }

    private void ResetUI()
    {
        if (MainMenu != null) MainMenu.SetActive(true);
        if (CreditsMenu != null) CreditsMenu.SetActive(false);
        if (OptionsMenu != null) OptionsMenu.SetActive(false);
    }
}
