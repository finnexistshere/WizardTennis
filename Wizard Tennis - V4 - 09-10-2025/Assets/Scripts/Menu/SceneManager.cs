using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SceneChanger : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject MainMenu;
    public GameObject CreditsMenu;
    public GameObject OptionsMenu;

    [Header("Options Menu UI References")]
    public Slider volumeSlider;
    public Toggle leftHandedToggle;
    public TextMeshProUGUI modeLabel;

    private AudioSource musicManager;

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
    }

    public void BackToMainFromCredits()
    {
        MainMenu.SetActive(true);
        CreditsMenu.SetActive(false);
    }

    public void Options()
    {
        MainMenu.SetActive(false);
        OptionsMenu.SetActive(true);

        // Hook the UI to OptionsManager
        if (OptionsManager.Instance != null)
        {
            OptionsManager.Instance.OnOptionsMenuOpened(volumeSlider, leftHandedToggle, modeLabel);
        }
    }

    public void BackToMainFromOptions()
    {
        MainMenu.SetActive(true);
        OptionsMenu.SetActive(false);

        // You can optionally re-hook or just leave the references
    }
}
