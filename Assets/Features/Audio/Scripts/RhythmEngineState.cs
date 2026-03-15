using System;

[Serializable]
public struct RhythmEngineState
{
    public float bpm;
    public float pitchSemitones;
    public float reverbAmount;
    public bool[] activeLoops;
}
