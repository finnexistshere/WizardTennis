using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SceneChanger : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject MainMenu;
    public GameObject CreditsMenu;
    public GameObject OptionsMenu;
    public GameObject SpellbookMenu;
    public GameObject MultiplayerMenu;

    [Header("Options Menu UI References")]
    public Slider volumeSlider;
    public Toggle leftHandedToggle;
    public TextMeshProUGUI modeLabel;

    [Header("Camera Movement")]
    public Camera menuCamera;                       // Assign your background camera
    public Transform mainMenuCamPos;                // Camera position for Main Menu
    public Transform creditsCamPos;                 // Camera position for Credits
    public Transform optionsCamPos;                 // Camera position for Options
    public Transform MultiplayerCamPos;
    [Tooltip("Higher values = slower camera movement")]
    public float cameraMoveDuration = 1.5f;

    private Coroutine cameraMoveRoutine;

    public void ChangeScene(string sceneName)
    {
        AudioSource musicManager = GameObject.FindGameObjectWithTag("Music Manager").GetComponent<AudioSource>();
        musicManager.Stop();
        SceneManager.LoadScene(sceneName);
    }

    public void ChangeSceneByIndex(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void ExitApplication()
    {
        Application.Quit();
        Debug.Log("Application Quit Requested");
    }

    public void Credits()
    {
        MainMenu.SetActive(false);
        CreditsMenu.SetActive(true);
        MoveCameraTo(creditsCamPos);
    }

    public void BackToMainFromCredits()
    {
        MainMenu.SetActive(true);
        CreditsMenu.SetActive(false);
        MoveCameraTo(mainMenuCamPos);
    }

    public void Options()
    {
        MainMenu.SetActive(false);
        OptionsMenu.SetActive(true);
        MoveCameraTo(optionsCamPos);

        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnOptionsMenuOpened(volumeSlider, leftHandedToggle, modeLabel);
        }
    }

    public void BackToMainFromOptions()
    {
        MainMenu.SetActive(true);
        OptionsMenu.SetActive(false);
        MoveCameraTo(mainMenuCamPos);
    }

    public void OpenMultiplayerMenu()
    {
        MainMenu.SetActive(false);
        MultiplayerMenu.SetActive(true);
        MoveCameraTo(MultiplayerCamPos);
    }

    public void BackToMainFromMultiplayer()
    {
        MainMenu.SetActive(true);
        MultiplayerMenu.SetActive(false);
        MoveCameraTo(mainMenuCamPos);
    }

    public void OpenSpellBookMenu()
    {
        MainMenu.SetActive(false);
        SpellbookMenu.SetActive(true);
    }

    public void BackToMainFromSpells()
    {
        MainMenu.SetActive(true);
        SpellbookMenu.SetActive(false);
    }

    private void MoveCameraTo(Transform target)
    {
        if (menuCamera == null || target == null)
            return;

        if (cameraMoveRoutine != null)
            StopCoroutine(cameraMoveRoutine);

        cameraMoveRoutine = StartCoroutine(InterpolateCameraPosition(target));
    }

    private IEnumerator InterpolateCameraPosition(Transform target)
    {
        Vector3 startPos = menuCamera.transform.position;
        Quaternion startRot = menuCamera.transform.rotation;

        float elapsed = 0f;
        while (elapsed < cameraMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraMoveDuration);

            // Ease In-Out Cubic (smooth acceleration + deceleration)
            t = t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            menuCamera.transform.position = Vector3.Lerp(startPos, target.position, t);
            menuCamera.transform.rotation = Quaternion.Slerp(startRot, target.rotation, t);

            yield return null;
        }

        menuCamera.transform.position = target.position;
        menuCamera.transform.rotation = target.rotation;
    }


}
