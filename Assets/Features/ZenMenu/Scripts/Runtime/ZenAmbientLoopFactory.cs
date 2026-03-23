using UnityEngine;

public static class ZenAmbientLoopFactory
{
    public static AudioClip CreatePlaceholderAmbientClip(int sampleRate = 44100)
    {
        const float durationSeconds = 18f;
        int sampleCount = Mathf.RoundToInt(durationSeconds * sampleRate);
        float[] data = new float[sampleCount];

        float[] chord = { 196f, 246.94f, 293.66f, 369.99f };
        float modRate = 0.08f;
        float shimmerRate = 0.17f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float sample = 0f;
            for (int noteIndex = 0; noteIndex < chord.Length; noteIndex++)
            {
                float frequency = chord[noteIndex] * (1f + Mathf.Sin((t * modRate) + noteIndex) * 0.0035f);
                float phase = t * frequency * Mathf.PI * 2f;
                float partial = Mathf.Sin(phase);
                partial += Mathf.Sin(phase * 0.5f) * 0.32f;
                partial += Mathf.Sin((phase * 1.5f) + (noteIndex * 0.6f)) * 0.18f;
                sample += partial * (0.16f / chord.Length);
            }

            float shimmer = Mathf.Sin(t * shimmerRate * Mathf.PI * 2f) * 0.04f;
            float envelope = 0.75f + (Mathf.Sin((t / durationSeconds) * Mathf.PI * 2f) * 0.06f);
            data[i] = Mathf.Clamp(sample * envelope + shimmer, -0.55f, 0.55f);
        }

        AudioClip clip = AudioClip.Create("ZenPlaceholderAmbient", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
