using System;

public static class VoiceAdpcm
{
    private static readonly int[] IndexTable =
    {
        -1, -1, -1, -1, 2, 4, 6, 8,
        -1, -1, -1, -1, 2, 4, 6, 8
    };

    private static readonly int[] StepTable =
    {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
        19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
        50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
        130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
        337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
        876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
        2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
        5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
        15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    };

    public static byte[] Encode(
        short[] samples,
        out short predictor,
        out byte stepIndex)
    {
        predictor = samples.Length > 0 ? samples[0] : (short)0;

        stepIndex = 0;

        int outputLength = (samples.Length + 1) / 2;

        byte[] output = new byte[outputLength];

        int outputIndex = 0;
        int nibble = 0;

        for (int i = 0; i < samples.Length; i++)
        {
            int code = EncodeSample(
                samples[i],
                ref predictor,
                ref stepIndex
            );

            if (nibble == 0)
            {
                output[outputIndex] = (byte)code;
                nibble = 1;
            }
            else
            {
                output[outputIndex] |= (byte)(code << 4);
                outputIndex++;
                nibble = 0;
            }
        }

        return output;
    }

    public static short[] Decode(
        byte[] data,
        int sampleCount,
        short predictor,
        byte stepIndex)
    {
        short[] samples = new short[sampleCount];

        if (sampleCount == 0)
            return samples;

        samples[0] = predictor;

        int sampleIndex = 1;

        for (int i = 0; i < data.Length && sampleIndex < sampleCount; i++)
        {
            int low = data[i] & 0x0F;

            predictor = DecodeSample(
                low,
                ref predictor,
                ref stepIndex
            );

            samples[sampleIndex++] = predictor;

            if (sampleIndex >= sampleCount)
                break;

            int high = (data[i] >> 4) & 0x0F;

            predictor = DecodeSample(
                high,
                ref predictor,
                ref stepIndex
            );

            samples[sampleIndex++] = predictor;
        }

        return samples;
    }

    private static int EncodeSample(
        short sample,
        ref short predictor,
        ref byte stepIndex)
    {
        int step = StepTable[stepIndex];

        int difference = sample - predictor;

        int sign = 0;

        if (difference < 0)
        {
            sign = 8;
            difference = -difference;
        }

        int code = 0;
        int delta = step >> 3;

        if (difference >= step)
        {
            code |= 4;
            difference -= step;
            delta += step;
        }

        if (difference >= step >> 1)
        {
            code |= 2;
            difference -= step >> 1;
            delta += step >> 1;
        }

        if (difference >= step >> 2)
        {
            code |= 1;
            delta += step >> 2;
        }

        code |= sign;

        if ((code & 8) != 0)
            predictor -= (short)delta;
        else
            predictor += (short)delta;

        predictor = Clamp16(predictor);

        stepIndex += (byte)IndexTable[code];

        if (stepIndex > 88)
            stepIndex = 88;

        return code;
    }

    private static short DecodeSample(
        int code,
        ref short predictor,
        ref byte stepIndex)
    {
        int step = StepTable[stepIndex];

        int difference = step >> 3;

        if ((code & 4) != 0)
            difference += step;

        if ((code & 2) != 0)
            difference += step >> 1;

        if ((code & 1) != 0)
            difference += step >> 2;

        if ((code & 8) != 0)
            predictor -= (short)difference;
        else
            predictor += (short)difference;

        predictor = Clamp16(predictor);

        stepIndex += (byte)IndexTable[code];

        if (stepIndex > 88)
            stepIndex = 88;

        return predictor;
    }

    private static short Clamp16(int value)
    {
        if (value > short.MaxValue)
            return short.MaxValue;

        if (value < short.MinValue)
            return short.MinValue;

        return (short)value;
    }
}