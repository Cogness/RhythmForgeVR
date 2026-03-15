using System;
using UnityEngine;

[Serializable]
public class RhythmSoundPreset
{
    public string presetName;
    [Range(0.1f, 1f)] public float masterVolume = 0.8f;
    [Range(40f, 220f)] public float defaultBpm = 120f;
    [Range(-24f, 24f)] public float defaultPitchSemitones = 0f;
    [Range(0f, 1f)] public float defaultReverbAmount = 0.18f;
    public RhythmSoundSlot[] slots = Array.Empty<RhythmSoundSlot>();
}
