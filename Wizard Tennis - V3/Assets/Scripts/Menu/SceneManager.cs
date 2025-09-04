using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class SceneChanger : MonoBehaviour
{
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
}