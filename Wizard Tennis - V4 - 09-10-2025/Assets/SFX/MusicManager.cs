using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    public AudioSource musicSource;
    public AudioClip musicTrack;

    private int initialSceneIndex;

    private void Awake()
    {
        // --- SINGLETON CHECK FIRST (BEFORE ANY AUDIO) ---
        if (Instance != null && Instance != this)
        {
            // A manager already exists ? kill this one immediately
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        initialSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // Create audio source if needed
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        // Do not overwrite inspector value
        if (musicTrack == null && OptionsManager.Instance != null)
        {
            musicTrack = OptionsManager.Instance.bgm;
        }

        if (musicTrack != null)
        {
            musicSource.clip = musicTrack;
            musicSource.Play();
        }

        // Keep track of scene changes
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        // If the scene truly changed (not just reloaded)
        if (newScene.buildIndex != initialSceneIndex)
        {
            // Stop immediately so new scene's music doesn't overlap
            if (musicSource != null)
                musicSource.Stop();

            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }
}
