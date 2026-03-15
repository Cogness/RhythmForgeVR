using System;

[Serializable]
public struct RhythmEngineState
{
    public int presetIndex;
    public string presetName;
    public float bpm;
    public float pitchSemitones;
    public float reverbAmount;
    public bool[] activeLoops;
}
