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
    public class DerivedSequence
    {
        public string kind;
        public int totalSteps;

        // Rhythm-specific
        public float swing;
        public List<RhythmEvent> events;

        // Melody-specific
        public List<MelodyNote> notes;
        public GrooveProfile grooveProfile;

        // Harmony-specific
        public List<ChordSlot> chordEvents;
    }
}
