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
    [Range(0f, 1f)] public float subOscillatorMix;
    [Range(0f, 1f)] public float overtoneMix;
    [Min(0.5f)] public float overtoneRatio;
    [Range(0f, 1f)] public float brightness;
    [Range(-1f, 1f)] public float brightnessDecay;
    [Range(0f, 1f)] public float transientMix;
    [Min(0.001f)] public float transientDecaySeconds;
    [Range(0f, 1f)] public float saturation;
    public bool[] defaultLoopSteps;
}
