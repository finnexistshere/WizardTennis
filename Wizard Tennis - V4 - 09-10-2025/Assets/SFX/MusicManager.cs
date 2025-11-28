using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;
    public AudioSource musicSource;
    public AudioClip musicTrack;

    private int lastSceneIndex;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Configure AudioSource if not assigned
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
        }

        if (GameObject.Find("PlayerAim") != null)
        {
            if (OptionsManager.Instance != null)
            {
                musicTrack = OptionsManager.Instance.bgm;
                musicSource.clip = musicTrack;
                musicSource.Play();
            }
        }

        if (musicTrack != null)
        {
            musicSource.clip = musicTrack;
            musicSource.Play();
        }

        // Store the starting scene index
        lastSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // Subscribe to scene change event
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        // If scene changed to a new one (not reload)
        if (newScene.buildIndex != lastSceneIndex)
        {
            Destroy(gameObject); // Stop music and reset manager
        }
        else
        {
            // Scene reloaded, do nothing — music continues
        }

        lastSceneIndex = newScene.buildIndex;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }
}
