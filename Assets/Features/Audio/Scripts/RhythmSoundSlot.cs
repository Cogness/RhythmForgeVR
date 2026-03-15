using System;
using UnityEngine;

[Serializable]
public class RhythmSoundSlot
{
    public string name;
    [Range(0.1f, 2f)] public float volumeMultiplier = 1f;
    [Range(-12f, 12f)] public float pitchOffsetSemitones = 0f;
    public AudioClip[] sampleClips = Array.Empty<AudioClip>();
    public RhythmVoiceDefinition proceduralVoice;
    public bool[] defaultLoopSteps = new bool[16];
}
