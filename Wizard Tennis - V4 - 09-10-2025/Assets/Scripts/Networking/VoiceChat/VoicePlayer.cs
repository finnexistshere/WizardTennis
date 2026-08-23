using System.Collections.Concurrent;
using UnityEngine;

public class VoicePlayer : MonoBehaviour
{
    private const int SampleRate = 16000;

    private AudioSource audioSource;
    private AudioClip audioClip;

    private readonly ConcurrentQueue<float[]> sampleQueue =
        new ConcurrentQueue<float[]>();

    private float volume = 1f;

    private bool started;

    public ulong ClientId { get; private set; }

    public bool IsSpeaking { get; private set; }

    public void Initialize(ulong clientId)
    {
        ClientId = clientId;

        audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = volume;

        audioClip = AudioClip.Create(
            "Voice_" + clientId,
            SampleRate,
            1,
            SampleRate,
            true,
            OnAudioRead
        );

        audioSource.clip = audioClip;

        audioSource.Play();

        started = true;
    }

    public void PushSamples(short[] samples)
    {
        if (!started)
            return;

        float[] converted =
            new float[samples.Length];

        float peak = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float value =
                samples[i] / 32768f;

            converted[i] = value;

            float abs = Mathf.Abs(value);

            if (abs > peak)
                peak = abs;
        }

        IsSpeaking = peak > 0.025f;

        sampleQueue.Enqueue(converted);
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);

        if (audioSource != null)
            audioSource.volume = volume;
    }

    public void Set3D(
        bool enabled,
        float minDistance,
        float maxDistance)
    {
        if (audioSource == null)
            return;

        audioSource.spatialBlend =
            enabled ? 1f : 0f;

        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
    }

    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }

    private void OnAudioRead(float[] data)
    {
        int written = 0;

        while (
            written < data.Length &&
            sampleQueue.TryDequeue(out float[] chunk))
        {
            int amount =
                Mathf.Min(
                    chunk.Length,
                    data.Length - written
                );

            System.Array.Copy(
                chunk,
                0,
                data,
                written,
                amount
            );

            written += amount;

            if (amount < chunk.Length)
            {
                float[] remainder =
                    new float[chunk.Length - amount];

                System.Array.Copy(
                    chunk,
                    amount,
                    remainder,
                    0,
                    remainder.Length
                );

                sampleQueue.Enqueue(remainder);
            }
        }

        while (written < data.Length)
        {
            data[written++] = 0f;
        }
    }

    private void OnDestroy()
    {
        if (audioSource != null)
            audioSource.Stop();

        if (audioClip != null)
            Destroy(audioClip);
    }
}