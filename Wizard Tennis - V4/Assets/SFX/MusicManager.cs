using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Music Settings")]
    [SerializeField] private AudioClip musicClip;
    [Range(0f, 1f)] public float volume = 0.75f;
    [SerializeField] private bool playOnAwake = true;

    private AudioSource audioSource;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.clip = musicClip;

        if (playOnAwake && musicClip != null)
            audioSource.Play();
    }

    /// <summary>
    /// Start playing a new track (loops automatically)
    /// </summary>
    public void PlayMusic(AudioClip clip, float newVolume = -1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[MusicManager] No clip provided!");
            return;
        }

        if (audioSource.clip == clip && audioSource.isPlaying)
            return; // Already playing this track

        audioSource.clip = clip;
        audioSource.loop = true;

        if (newVolume >= 0f)
            audioSource.volume = Mathf.Clamp01(newVolume);

        audioSource.Play();
    }

    /// <summary>
    /// Stop music
    /// </summary>
    public void StopMusic()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// Pause music
    /// </summary>
    public void PauseMusic()
    {
        audioSource.Pause();
    }

    /// <summary>
    /// Resume paused music
    /// </summary>
    public void ResumeMusic()
    {
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    /// <summary>
    /// Adjust music volume on the fly
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
    }
}
