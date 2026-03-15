using System;
using UnityEngine;

[Serializable]
public struct RhythmVoiceDefinition
{
    public string name;
    public WaveformType waveform;
    [Min(20f)] public float baseFrequency;
    [Range(0f, 1f)] public float gain;
    [Min(0f)] public float attackSeconds;
    [Min(0f)] public float decaySeconds;
    [Range(0f, 1f)] public float sustainLevel;
    [Min(0.01f)] public float holdSeconds;
    [Min(0.01f)] public float releaseSeconds;
    [Range(0f, 1f)] public float noiseMix;
    [Range(-1200f, 1200f)] public float detuneCents;
    [Min(0f)] public float vibratoRate;
    [Range(0f, 0.25f)] public float vibratoDepth;
    [Range(-1f, 1f)] public float pitchDecayAmount;
    public bool[] defaultLoopSteps;

    public static RhythmVoiceDefinition Create(
        string voiceName,
        WaveformType waveformType,
        float frequency,
        float voiceGain,
        float attack,
        float decay,
        float sustain,
        float hold,
        float release,
        float noise,
        float detune,
        float vibratoHz,
        float vibratoAmount,
        float pitchDecay,
        params int[] activeSteps)
    {
        var steps = new bool[16];
        if (activeSteps != null)
        {
            for (int i = 0; i < activeSteps.Length; i++)
            {
                int index = activeSteps[i];
                if (index >= 0 && index < steps.Length)
                {
                    steps[index] = true;
                }
            }
        }

        return new RhythmVoiceDefinition
        {
            name = voiceName,
            waveform = waveformType,
            baseFrequency = frequency,
            gain = voiceGain,
            attackSeconds = attack,
            decaySeconds = decay,
            sustainLevel = sustain,
            holdSeconds = hold,
            releaseSeconds = release,
            noiseMix = noise,
            detuneCents = detune,
            vibratoRate = vibratoHz,
            vibratoDepth = vibratoAmount,
            pitchDecayAmount = pitchDecay,
            defaultLoopSteps = steps
        };
    }
}
