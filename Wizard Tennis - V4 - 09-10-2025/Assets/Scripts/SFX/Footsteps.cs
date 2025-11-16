using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;          // The audio source for footsteps
    public AudioClip[] footstepSounds;       // Array of footstep sounds

    [Header("Timing")]
    public float stepDistance = 2f;          // Distance between steps
    public float minInterval = 0.08f;        // Prevents ultra rapid repeats

    [Header("Variation")]
    [Range(0f, 0.5f)] public float pitchJitter = 0.09f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.06f;

    private Vector3 lastPosition;
    private float distanceMoved;
    private float lastPlayTime = -999f;
    private int lastIndex = -1;

    void Start()
    {
        lastPosition = transform.position;

        if (audioSource == null)
        {
            var audioGO = GameObject.Find("audiosource");
            if (audioGO != null)
                audioSource = audioGO.GetComponent<AudioSource>();

            if (audioSource != null)
                Debug.Log("[Spellcasting] Found and linked AudioSource.");
            else
                Debug.LogWarning("[Spellcasting] AudioSource missing!");
        }
    }

    void Update()
    {
        // Calculate how far the player has moved
        distanceMoved += Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;

        // If they've moved far enough, play a step
        if (distanceMoved >= stepDistance)
        {
            PlayFootstep();
            distanceMoved = 0f;
        }
    }

    void PlayFootstep()
    {
        if (audioSource == null || footstepSounds == null || footstepSounds.Length == 0) return;
        if (Time.time - lastPlayTime < minInterval) return;

        // choose a clip; avoid immediate repeat if >1 clips
        int index = (footstepSounds.Length == 1) ? 0 : Random.Range(0, footstepSounds.Length);
        if (footstepSounds.Length > 1 && index == lastIndex)
            index = (index + 1) % footstepSounds.Length;
        lastIndex = index;

        // apply pitch BEFORE PlayOneShot (it uses current source pitch)
        float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);

        // PlayOneShot volume argument is a SCALE of audioSource.volume
        float volScale = 1f + Random.Range(-volumeJitter, volumeJitter);
        volScale = Mathf.Clamp01(volScale);

        audioSource.PlayOneShot(footstepSounds[index], volScale);
        lastPlayTime = Time.time;

        // Debug (optional): confirm changing values
        // Debug.Log($"Step -> clip {index}, pitch {audioSource.pitch:F2}, volScale {volScale:F2}");
    }
}
