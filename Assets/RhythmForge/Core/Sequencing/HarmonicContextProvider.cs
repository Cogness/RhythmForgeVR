using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    /// <summary>
    /// Lightweight static bridge that makes the current HarmonicContext available to all
    /// melody derivers without changing the IMelodyDeriver interface.
    /// Unity's main-thread single-threaded dispatch makes this safe.
    /// DraftBuilder.BuildFromStroke sets this before calling Derive; derivers read it.
    /// </summary>
    public static class HarmonicContextProvider
    {
        [System.ThreadStatic]
        private static HarmonicContext _current = new HarmonicContext();

        [System.ThreadStatic]
        private static ChordProgression _currentProgression;

        /// <summary>Set by DraftBuilder before every derivation call.</summary>
        public static void Set(HarmonicContext ctx)
        {
            _current = ctx?.Clone() ?? new HarmonicContext();
        }

        public static void SetProgression(ChordProgression progression)
        {
            _currentProgression = progression?.Clone();
        }

        public static HarmonicContext FromProgression(ChordProgression progression, int barIndex)
        {
            return progression?.ToHarmonicContext(barIndex) ?? new HarmonicContext();
        }

        public static void SetFromProgression(ChordProgression progression, int barIndex)
        {
            SetProgression(progression);
            Set(FromProgression(progression, barIndex));
        }

        /// <summary>Returns the current shared context (never null).</summary>
        public static HarmonicContext Current => _current ?? (_current = new HarmonicContext());

        public static ChordProgression CurrentProgression => _currentProgression?.Clone();

        public static void Clear()
        {
            _current = new HarmonicContext();
            _currentProgression = null;
        }
    }
}
