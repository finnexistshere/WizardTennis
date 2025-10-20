using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    public AudioSource audioSource;          // The audio source for footsteps
    public AudioClip[] footstepSounds;       // Array of footstep sounds
    public float stepDistance = 2f;          // Distance between steps

    private Vector3 lastPosition;
    private float distanceMoved;

    void Start()
    {
        lastPosition = transform.position;
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
        if (footstepSounds.Length > 0)
        {
            int index = Random.Range(0, footstepSounds.Length);
            audioSource.PlayOneShot(footstepSounds[index]);
        }
    }
}
