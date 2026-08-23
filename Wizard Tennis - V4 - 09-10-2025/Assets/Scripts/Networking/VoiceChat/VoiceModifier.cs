using UnityEngine;

public enum VoiceModifierType
{
    Normal,
    Deep,
    High,
    Robot,
    Radio,
    Distorted,
    Whisper
}

[System.Serializable]
public class VoiceModifierSettings
{
    public VoiceModifierType type = VoiceModifierType.Normal;

    [Range(0.5f, 2f)]
    public float pitch = 1f;

    [Range(0f, 2f)]
    public float volume = 1f;

    [Range(0f, 1f)]
    public float distortion = 0f;

    [Range(0f, 1f)]
    public float robotAmount = 0f;

    [Range(0f, 1f)]
    public float radioAmount = 0f;
}

public static class VoiceModifier
{
    public static void Process(
        float[] samples,
        int sampleRate,
        VoiceModifierSettings settings)
    {
        if (settings == null)
            return;

        float pitch = settings.pitch;
        float volume = settings.volume;
        float distortion = settings.distortion;
        float robotAmount = settings.robotAmount;
        float radioAmount = settings.radioAmount;

        switch (settings.type)
        {
            case VoiceModifierType.Deep:
                pitch *= 0.72f;
                break;

            case VoiceModifierType.High:
                pitch *= 1.35f;
                break;

            case VoiceModifierType.Robot:
                robotAmount = Mathf.Max(robotAmount, 0.85f);
                distortion = Mathf.Max(distortion, 0.15f);
                break;

            case VoiceModifierType.Radio:
                radioAmount = Mathf.Max(radioAmount, 0.9f);
                distortion = Mathf.Max(distortion, 0.08f);
                break;

            case VoiceModifierType.Distorted:
                distortion = Mathf.Max(distortion, 0.8f);
                break;

            case VoiceModifierType.Whisper:
                volume *= 0.65f;
                radioAmount = Mathf.Max(radioAmount, 0.25f);
                break;
        }

        if (!Mathf.Approximately(pitch, 1f))
            ApplyPitch(samples, pitch);

        for (int i = 0; i < samples.Length; i++)
        {
            float sample = samples[i];

            if (robotAmount > 0f)
            {
                float time = i / (float)sampleRate;

                float carrier =
                    Mathf.Sin(time * 70f * Mathf.PI * 2f);

                sample =
                    Mathf.Lerp(
                        sample,
                        sample * carrier,
                        robotAmount
                    );
            }

            if (distortion > 0f)
            {
                float drive = Mathf.Lerp(
                    1f,
                    8f,
                    distortion
                );

                sample = (float)System.Math.Tanh(sample * drive);

                sample = Mathf.Lerp(
                    sample,
                    Mathf.Round(sample * 12f) / 12f,
                    distortion * 0.35f
                );
            }

            if (radioAmount > 0f)
            {
                sample = Mathf.Lerp(
                    sample,
                    Mathf.Clamp(sample * 1.5f, -0.7f, 0.7f),
                    radioAmount
                );
            }

            samples[i] = sample * volume;
        }
    }

    private static void ApplyPitch(
        float[] samples,
        float pitch)
    {
        float[] original = new float[samples.Length];

        System.Array.Copy(
            samples,
            original,
            samples.Length
        );

        for (int i = 0; i < samples.Length; i++)
        {
            float sourcePosition =
                i * pitch;

            sourcePosition %= original.Length;

            int indexA =
                Mathf.FloorToInt(sourcePosition);

            int indexB =
                (indexA + 1) % original.Length;

            float t =
                sourcePosition - indexA;

            samples[i] =
                Mathf.Lerp(
                    original[indexA],
                    original[indexB],
                    t
                );
        }
    }
}