using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// One chord slot inside the <see cref="HarmonicFabric"/> progression.
    /// </summary>
    [Serializable]
    public class ChordPlacement
    {
        public int rootMidi;
        public List<int> tones = new List<int>();
        public string flavor = "minor";
        public int sourceShapeRole;

        public ChordPlacement Clone()
        {
            return new ChordPlacement
            {
                rootMidi = rootMidi,
                tones = tones != null ? new List<int>(tones) : new List<int>(),
                flavor = flavor,
                sourceShapeRole = sourceShapeRole
            };
        }
    }

    /// <summary>
    /// Scene-wide chord-per-bar scaffold that future shapes harmonise against.
    /// Unused by the runtime in Phase A — type + API skeleton only. Phase B wires
    /// this into SessionStore so harmony derivations write here and melody/rhythm
    /// derivations read from it. Phase F expands beyond a single-bar progression.
    /// </summary>
    public class HarmonicFabric
    {
        public string key = "A minor";
        public List<ChordPlacement> progression = new List<ChordPlacement>();
        public int nextFreeBar;

        /// <summary>
        /// Reserves and returns the next empty bar index. Grows the progression list.
        /// </summary>
        public int ReserveSlot()
        {
            int bar = nextFreeBar;
            while (progression.Count <= bar)
                progression.Add(null);
            nextFreeBar = bar + 1;
            return bar;
        }

        /// <summary>
        /// Returns the chord active at the given bar, wrapping via modulo if the
        /// progression is shorter than <paramref name="bar"/>. Returns null when empty.
        /// </summary>
        public ChordPlacement ChordAtBar(int bar)
        {
            if (progression == null || progression.Count == 0) return null;
            int n = progression.Count;
            int idx = ((bar % n) + n) % n;
            return progression[idx];
        }

        /// <summary>
        /// Writes a chord into the given bar slot, growing the progression as needed.
        /// </summary>
        public void Write(int bar, int rootMidi, List<int> chord, string flavor, int sourceShapeRole = 0)
        {
            if (bar < 0) return;
            while (progression.Count <= bar)
                progression.Add(null);
            progression[bar] = new ChordPlacement
            {
                rootMidi = rootMidi,
                tones = chord != null ? new List<int>(chord) : new List<int>(),
                flavor = flavor ?? "minor",
                sourceShapeRole = sourceShapeRole
            };
        }

        /// <summary>
        /// Phase F will use this to collapse null slots and re-sequence shapes that
        /// overflowed the fixed progression length. Currently a no-op; ChordAtBar
        /// already wraps via modulo.
        /// </summary>
        public void Wrap()
        {
            // Intentionally empty in Phase A.
        }

        public void Clear()
        {
            progression.Clear();
            nextFreeBar = 0;
        }
    }
}
