using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class RhythmEvent
    {
        public int step;
        public string lane; // "kick", "snare", "hat", "perc"
        public float velocity;
        public float microShift;
    }

    [Serializable]
    public class RhythmSequence
    {
        public string kind = "rhythm";
        public int totalSteps;
        public float swing;
        public List<RhythmEvent> events = new List<RhythmEvent>();
    }

    [Serializable]
    public class MelodyNote
    {
        public int step;
        public int midi;
        public int durationSteps;
        public float velocity;
        public float glide;
    }

    [Serializable]
    public class MelodySequence
    {
        public string kind = "melody";
        public int totalSteps;
        public List<MelodyNote> notes = new List<MelodyNote>();
    }

    [Serializable]
    public class HarmonySequence
    {
        public string kind = "harmony";
        public int totalSteps;
        public string flavor; // "maj7", "sus", "minor"
        public int rootMidi;
        public List<int> chord = new List<int>();
    }

    [Serializable]
    public class DerivedSequence
    {
        public string kind;
        public int totalSteps;

        // Rhythm-specific
        public float swing;
        public List<RhythmEvent> events;

        // Melody-specific
        public List<MelodyNote> notes;

        // Harmony-specific
        public string flavor;
        public int rootMidi;
        public List<int> chord;
    }
}
