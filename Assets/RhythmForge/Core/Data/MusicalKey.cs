using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class MusicalKey
    {
        public string name;
        public int rootMidi;
        public int[] scale;

        public MusicalKey(string name, int rootMidi, int[] scale)
        {
            this.name = name;
            this.rootMidi = rootMidi;
            this.scale = scale;
        }
    }

    public static class MusicalKeys
    {
        private static readonly int[] MinorScale = { 0, 2, 3, 5, 7, 8, 10 };
        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11 };

        private static Dictionary<string, MusicalKey> _keys;

        public static Dictionary<string, MusicalKey> All
        {
            get
            {
                if (_keys == null)
                    Initialize();
                return _keys;
            }
        }

        private static void Initialize()
        {
            _keys = new Dictionary<string, MusicalKey>
            {
                { "A minor", new MusicalKey("A minor", 57, MinorScale) },
                { "C major", new MusicalKey("C major", 60, MajorScale) },
                { "D minor", new MusicalKey("D minor", 62, MinorScale) },
                { "E minor", new MusicalKey("E minor", 64, MinorScale) },
                { "F major", new MusicalKey("F major", 65, MajorScale) },
                { "G major", new MusicalKey("G major", 67, MajorScale) },
                { "B minor", new MusicalKey("B minor", 59, MinorScale) },
                { "C minor", new MusicalKey("C minor", 60, MinorScale) },
                { "D major", new MusicalKey("D major", 62, MajorScale) },
                { "E major", new MusicalKey("E major", 64, MajorScale) },
                { "F minor", new MusicalKey("F minor", 65, MinorScale) },
                { "G minor", new MusicalKey("G minor", 67, MinorScale) },
            };
        }

        public static MusicalKey Get(string keyName)
        {
            if (All.TryGetValue(keyName, out var key))
                return key;
            return All["A minor"];
        }

        /// <summary>
        /// Snaps <paramref name="midi"/> to the nearest pitch that belongs to the key's scale.
        /// Preserves the octave register (picks the closest in-key pitch, up or down).
        /// </summary>
        public static int QuantizeToKey(int midi, string keyName)
        {
            var key = Get(keyName);
            // Build the set of in-key chromatic pitch classes (0-11 relative to C).
            int rootClass = key.rootMidi % 12;
            var inKeyClasses = new int[key.scale.Length];
            for (int i = 0; i < key.scale.Length; i++)
                inKeyClasses[i] = (rootClass + key.scale[i]) % 12;

            int midiClass = ((midi % 12) + 12) % 12;

            // Find the in-key pitch class closest (wrapping within the octave).
            int bestClass = inKeyClasses[0];
            int bestDist = int.MaxValue;
            foreach (int kc in inKeyClasses)
            {
                int dist = System.Math.Abs(kc - midiClass);
                if (dist > 6) dist = 12 - dist; // wrap-around distance
                if (dist < bestDist) { bestDist = dist; bestClass = kc; }
            }

            // Reconstruct MIDI: same octave region, snapped pitch class.
            int octaveBase = midi - midiClass; // e.g. for midi=63 (Eb4): 63-3=60
            int candidate = octaveBase + bestClass;
            // Ensure we pick the nearest octave of that class.
            if (candidate - midi > 6)  candidate -= 12;
            if (midi - candidate > 6)  candidate += 12;
            return candidate;
        }

        /// <summary>
        /// Builds a diatonic chord starting on <paramref name="rootMidi"/> by walking
        /// <paramref name="scaleDegreeSteps"/> steps through the key's scale (1 step = next scale note).
        /// All returned pitches are guaranteed in-key.
        /// Example: scaleDegreeSteps = {0,2,4} gives root, 3rd, 5th (a triad).
        /// </summary>
        public static List<int> BuildScaleChord(int rootMidi, string keyName, int[] scaleDegreeSteps)
        {
            var key = Get(keyName);
            int[] scale = key.scale;

            // Find which scale degree index the rootMidi lands on (or nearest)
            int pitchClass = ((rootMidi - key.rootMidi) % 12 + 12) % 12;
            int rootDegreeIdx = 0;
            int minDist = int.MaxValue;
            for (int i = 0; i < scale.Length; i++)
            {
                int d = System.Math.Abs(scale[i] - pitchClass);
                if (d < minDist) { minDist = d; rootDegreeIdx = i; }
            }

            var chord = new List<int>();
            foreach (int step in scaleDegreeSteps)
            {
                int degIdx  = (rootDegreeIdx + step) % scale.Length;
                int octaves = (rootDegreeIdx + step) / scale.Length;
                int midi    = key.rootMidi + scale[degIdx] + octaves * 12
                            + (rootMidi - key.rootMidi) / 12 * 12;
                chord.Add(midi);
            }
            return chord;
        }
    }
}
