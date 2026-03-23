using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class ProceduralMusicalShapeVoice : MonoBehaviour, IMusicalShapeVoice
{
    private const int SampleRate = 44100;

    private AudioSource _audioSource;

    public float LoopDurationSeconds => Definition != null ? Definition.LoopDurationSeconds : 0f;
    public MusicalLoopDefinition Definition { get; private set; }

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void Initialize(MusicalLoopDefinition definition, AudioSource audioSource)
    {
        Definition = definition;
        _audioSource = audioSource;

        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0.92f;
        _audioSource.minDistance = 0.5f;
        _audioSource.maxDistance = 12f;
        _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _audioSource.dopplerLevel = 0f;
        _audioSource.volume = Mathf.Clamp(definition.Volume, 0.05f, 0.85f);
        _audioSource.clip = BuildClip(definition);
        _audioSource.Play();
    }

    public void Stop()
    {
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
    }

    private static AudioClip BuildClip(MusicalLoopDefinition definition)
    {
        int sampleCount = Mathf.CeilToInt(definition.LoopDurationSeconds * SampleRate);
        float[] samples = new float[sampleCount];
        float peak = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float sample = 0f;

            if (definition.ContinuousDroneMix > 0f)
            {
                float drone = Mathf.Sin(t * definition.BaseFrequency * Mathf.PI * 2f);
                drone += Mathf.Sin(t * definition.BaseFrequency * 0.5f * Mathf.PI * 2f) * 0.33f;
                drone += Mathf.Sin(t * definition.BaseFrequency * 1.5f * Mathf.PI * 2f) * 0.18f;
                sample += drone * definition.ContinuousDroneMix * 0.14f;
            }

            for (int pulseIndex = 0; pulseIndex < definition.Pulses.Count; pulseIndex++)
            {
                MusicalPulse pulse = definition.Pulses[pulseIndex];
                float dt = t - pulse.TimeSeconds;
                if (dt < 0f || dt > pulse.DurationSeconds)
                {
                    continue;
                }

                float normalized = dt / Mathf.Max(0.0001f, pulse.DurationSeconds);
                float envelope = 1f - normalized;
                float freq = pulse.Frequency;
                float tone = definition.VoiceType switch
                {
                    ShapeVoiceType.Percussive => BuildPercussiveSample(freq, dt, envelope),
                    ShapeVoiceType.Arpeggio => BuildArpSample(freq, dt, envelope),
                    ShapeVoiceType.Drone => BuildDroneAccent(freq, dt, envelope),
                    _ => BuildPluckedSample(freq, dt, envelope)
                };

                sample += tone * pulse.Amplitude * 0.32f;
            }

            samples[i] = sample;
            peak = Mathf.Max(peak, Mathf.Abs(sample));
        }

        if (peak > 0.98f)
        {
            float scale = 0.98f / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }

        AudioClip clip = AudioClip.Create($"MusicalShape_{definition.VoiceType}", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float BuildPluckedSample(float frequency, float time, float envelope)
    {
        float sample = Mathf.Sin(time * frequency * Mathf.PI * 2f);
        sample += Mathf.Sin(time * frequency * 2f * Mathf.PI * 2f) * 0.18f;
        return sample * envelope * envelope;
    }

    private static float BuildArpSample(float frequency, float time, float envelope)
    {
        float vibrato = Mathf.Sin(time * 5.4f * Mathf.PI * 2f) * 0.01f;
        float sample = Mathf.Sin(time * frequency * (1f + vibrato) * Mathf.PI * 2f);
        sample += Mathf.Sin(time * frequency * 0.5f * Mathf.PI * 2f) * 0.22f;
        return sample * envelope;
    }

    private static float BuildDroneAccent(float frequency, float time, float envelope)
    {
        float sample = Mathf.Sin(time * frequency * Mathf.PI * 2f);
        sample += Mathf.Sin(time * frequency * 1.5f * Mathf.PI * 2f) * 0.14f;
        return sample * Mathf.Sqrt(envelope);
    }

    private static float BuildPercussiveSample(float frequency, float time, float envelope)
    {
        float tone = Mathf.Sin(time * frequency * Mathf.PI * 2f) * 0.35f;
        float noise = HashNoise(time * 3100f + frequency) * 0.85f;
        return (tone + noise) * envelope * envelope;
    }

    private static float HashNoise(float seed)
    {
        return Mathf.Repeat(Mathf.Sin(seed * 91.17f) * 43758.5453f, 1f) * 2f - 1f;
    }
}
