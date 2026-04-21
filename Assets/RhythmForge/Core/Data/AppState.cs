using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// Shared harmonic context updated whenever a HarmonyPad is committed.
    /// Melody and bass derivers use it to constrain strong-beat notes to chord tones.
    /// </summary>
    [Serializable]
    public class HarmonicContext
    {
        public int rootMidi = 67;                       // G4 — guided default root
        public List<int> chordTones = new List<int>();  // in-key pitches of the current chord
        public string flavor = "major";

        public bool HasChord => chordTones != null && chordTones.Count > 0;

        /// <summary>Returns the chord tone (any octave) nearest to <paramref name="targetMidi"/>.</summary>
        public int NearestChordTone(int targetMidi)
        {
            if (!HasChord) return targetMidi;
            int best = chordTones[0];
            int bestDist = int.MaxValue;
            foreach (int ct in chordTones)
            {
                // compare pitch classes across octaves
                int pitchClass = ((ct % 12) + 12) % 12;
                int targetClass = ((targetMidi % 12) + 12) % 12;
                int dist = System.Math.Abs(pitchClass - targetClass);
                if (dist > 6) dist = 12 - dist; // wrap
                if (dist < bestDist) { bestDist = dist; best = ct; }
            }
            // Return the chord tone transposed to match the octave of targetMidi
            int bestClass = ((best % 12) + 12) % 12;
            int targetOctaveBase = targetMidi - (((targetMidi % 12) + 12) % 12);
            int candidate = targetOctaveBase + bestClass;
            // Pick nearest octave
            if (candidate - targetMidi > 6)  candidate -= 12;
            if (targetMidi - candidate > 6)  candidate += 12;
            return candidate;
        }

        public HarmonicContext Clone()
        {
            return new HarmonicContext
            {
                rootMidi = rootMidi,
                chordTones = chordTones != null ? new List<int>(chordTones) : new List<int>(),
                flavor = flavor
            };
        }
    }

    [Serializable]
    public class AppState
    {
        public int version = 7;
        public float tempo = GuidedDefaults.Tempo;
        public string key = GuidedDefaults.Key;
        public string activeGroupId = "lofi"; // kept for migration; use activeGenreId at runtime
        public string activeGenreId = GuidedDefaults.ActiveGenreId;
        public string drawMode = "Percussion";
        public string activeSceneId = "scene-a";
        public string selectedInstanceId;
        public string selectedPatternId;
        public string queuedSceneId;
        public HarmonicContext harmonicContext = new HarmonicContext();
        public bool guidedMode = true;
        public Composition composition = GuidedDefaults.Create();
        public List<PatternDefinition> patterns = new List<PatternDefinition>();
        public List<PatternInstance> instances = new List<PatternInstance>();
        public List<SceneData> scenes = new List<SceneData>();
        public List<ArrangementSlot> arrangement = new List<ArrangementSlot>();
        public DraftCounters counters = new DraftCounters();
    }

    [Serializable]
    public class DraftCounters
    {
        public int rhythm = 1;
        public int melody = 1;
        public int harmony = 1;
        public int bass = 1;
        public int groove = 1;

        public int GetCount(PatternType type)
        {
            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion: return rhythm;
                case PatternType.Melody:     return melody;
                case PatternType.Harmony:    return harmony;
                case PatternType.Bass:       return bass;
                case PatternType.Groove:     return groove;
                default:                     return rhythm;
            }
        }

        public void Increment(PatternType type)
        {
            switch (PatternTypeCompatibility.Canonicalize(type))
            {
                case PatternType.Percussion: rhythm++;  break;
                case PatternType.Melody:     melody++;  break;
                case PatternType.Harmony:    harmony++; break;
                case PatternType.Bass:       bass++;    break;
                case PatternType.Groove:     groove++;  break;
            }
        }
    }

    public static class AppStateFactory
    {
        public const int BarSteps = 16;
        public const int MaxArrangementSlots = 8;

        public static AppState CreateEmpty()
        {
            var state = new AppState();
            state.guidedMode = true;
            state.composition = GuidedDefaults.Create();
            state.tempo = state.composition.tempo;
            state.key = state.composition.key;
            state.activeGenreId = GuidedDefaults.ActiveGenreId;
            state.harmonicContext = state.composition.progression.ToHarmonicContext(0);
            state.scenes = new List<SceneData>
            {
                new SceneData("scene-a", "Scene A"),
                new SceneData("scene-b", "Scene B"),
                new SceneData("scene-c", "Scene C"),
                new SceneData("scene-d", "Scene D")
            };
            state.arrangement = new List<ArrangementSlot>();
            for (int i = 0; i < MaxArrangementSlots; i++)
            {
                state.arrangement.Add(new ArrangementSlot($"slot-{i + 1}"));
            }
            return state;
        }
    }
}
