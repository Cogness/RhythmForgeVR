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
        public int rootMidi = 57;                       // A3 — default A minor root
        public List<int> chordTones = new List<int>();  // in-key pitches of the current chord
        public string flavor = "minor";

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
        public int version = 10;
        public float tempo = 85f;
        public string key = "A minor";
        public string activeGroupId = "lofi"; // kept for migration; use activeGenreId at runtime
        public string activeGenreId = "electronic";
        public string drawMode = "RhythmLoop";
        public string drawShapeMode = nameof(ShapeFacetMode.Free);
        public string activeSceneId = "scene-a";
        public string selectedInstanceId;
        public string selectedPatternId;
        public string queuedSceneId;
        public HarmonicContext harmonicContext = new HarmonicContext();
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

        public int GetCount(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return rhythm;
                case PatternType.MelodyLine: return melody;
                default:                     return harmony;
            }
        }

        public void Increment(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: rhythm++;  break;
                case PatternType.MelodyLine: melody++;  break;
                default:                     harmony++; break;
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
