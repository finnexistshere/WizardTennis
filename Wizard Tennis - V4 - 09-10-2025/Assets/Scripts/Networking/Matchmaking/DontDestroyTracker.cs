using UnityEngine;
using UnityEngine.SceneManagement;
using System.Diagnostics;

public class DontDestroyTracker : MonoBehaviour
{
    [TextArea(4, 10)]
    public string dontDestroyCaller;

    private Scene startupScene;

    private void Awake()
    {
        startupScene = gameObject.scene;
    }

    private void Update()
    {
        // Check if the object was moved into the DDOL scene
        if (gameObject.scene.name == "DontDestroyOnLoad")
        {
            if (string.IsNullOrEmpty(dontDestroyCaller))
            {
                // Capture stack trace
                var trace = new StackTrace();
                dontDestroyCaller = trace.ToString();

                UnityEngine.Debug.LogWarning(
                    $"[DontDestroyTracker] {gameObject.name} was marked DontDestroyOnLoad by:\n{dontDestroyCaller}"
                );
            }

            // Stop checking once known
            enabled = false;
        }
    }
}
