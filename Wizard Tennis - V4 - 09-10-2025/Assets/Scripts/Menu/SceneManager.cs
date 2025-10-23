using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class SceneChanger : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject MainMenu;
    public GameObject CreditsMenu;
    public GameObject OptionsMenu;

    public void ChangeScene(string sceneName)
    {
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
    }

    public void BacktoMainFromOptions()
    {
        MainMenu.SetActive(true);
        OptionsMenu.SetActive(false);
    }
}