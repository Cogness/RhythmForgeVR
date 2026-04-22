using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    /// <summary>
    /// Per-genre configuration for the guided composition flow: key, tempo, bars,
    /// default chord progression, and default bass preset. Centralises what used
    /// to be hardcoded "electronic" / "G major" / "trap-bass" literals scattered
    /// across the derivers.
    /// </summary>
    public sealed class GuidedPolicySnapshot
    {
        public string genreId;
        public string keyName;
        public float tempo;
        public int bars;
        public string defaultBassPresetId;

        private readonly Func<ChordProgression> _createDefaultProgression;

        internal GuidedPolicySnapshot(
            string genreId,
            string keyName,
            float tempo,
            int bars,
            string defaultBassPresetId,
            Func<ChordProgression> createDefaultProgression)
        {
            this.genreId = genreId;
            this.keyName = keyName;
            this.tempo = tempo;
            this.bars = bars;
            this.defaultBassPresetId = defaultBassPresetId;
            _createDefaultProgression = createDefaultProgression;
        }

        public ChordProgression CreateDefaultProgression()
        {
            return _createDefaultProgression != null ? _createDefaultProgression() : new ChordProgression();
        }
    }

    public static class GuidedPolicy
    {
        public const int DefaultBars = 8;

        private static readonly GuidedPolicySnapshot ElectronicPolicy = new GuidedPolicySnapshot(
            genreId: "electronic",
            keyName: "G major",
            tempo: 100f,
            bars: DefaultBars,
            defaultBassPresetId: "trap-bass",
            createDefaultProgression: CreateElectronicProgression);

        private static readonly GuidedPolicySnapshot JazzPolicy = new GuidedPolicySnapshot(
            genreId: "jazz",
            keyName: "D minor",
            tempo: 110f,
            bars: DefaultBars,
            defaultBassPresetId: "jazz-upright",
            createDefaultProgression: CreateJazzProgression);

        private static readonly GuidedPolicySnapshot NewAgePolicy = new GuidedPolicySnapshot(
            genreId: "newage",
            keyName: "C major",
            tempo: 68f,
            bars: DefaultBars,
            defaultBassPresetId: "newage-subbass",
            createDefaultProgression: CreateNewAgeProgression);

        /// <summary>
        /// Snapshot for the currently-active genre (as reported by <see cref="GenreRegistry"/>).
        /// </summary>
        public static GuidedPolicySnapshot Active
        {
            get
            {
                var active = GenreRegistry.GetActive();
                return Get(active != null ? active.Id : "electronic");
            }
        }

        public static GuidedPolicySnapshot Get(string genreId)
        {
            switch (genreId)
            {
                case "jazz":   return JazzPolicy;
                case "newage": return NewAgePolicy;
                default:       return ElectronicPolicy;
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  Default progressions
        // ───────────────────────────────────────────────────────────────

        private static ChordProgression CreateElectronicProgression()
        {
            // G major I – vi – IV – V loop (unchanged from the original guided default).
            const string key = "G major";
            return BuildProgression(
                key,
                new[] { 67, 64, 60, 62, 67, 64, 60, 62 },
                new[] { "major", "minor", "major", "major", "major", "minor", "major", "major" });
        }

        private static ChordProgression CreateJazzProgression()
        {
            // D minor ii – V – i – i, repeated:
            //   Em7♭5 (64) | A7 (57) | Dm7 (62) | Dm7 (62) × 2
            // Roots land on diatonic positions of D natural minor so BuildScaleChord
            // seeds sensible triads. HarmonyShapeModulator (Phase R) replaces the
            // voicing with the genre-appropriate 7th/9th/13th flavor.
            const string key = "D minor";
            return BuildProgression(
                key,
                new[] { 64, 57, 62, 62, 64, 57, 62, 62 },
                new[] { "min7b5", "dom7", "min7", "min7", "min7b5", "dom7", "min7", "min7" });
        }

        private static ChordProgression CreateNewAgeProgression()
        {
            // C major floating I – V – vi – IV loop:
            //   C (60) | G (67) | Am (57) | F (65) × 2
            const string key = "C major";
            return BuildProgression(
                key,
                new[] { 60, 67, 57, 65, 60, 67, 57, 65 },
                new[] { "major", "major", "minor", "major", "major", "major", "minor", "major" });
        }

        private static ChordProgression BuildProgression(string keyName, int[] roots, string[] flavors)
        {
            var chords = new List<ChordSlot>(roots.Length);
            for (int i = 0; i < roots.Length; i++)
            {
                chords.Add(new ChordSlot
                {
                    barIndex = i,
                    rootMidi = roots[i],
                    flavor = flavors[i],
                    voicing = MusicalKeys.BuildScaleChord(roots[i], keyName, new[] { 0, 2, 4 })
                });
            }

            return new ChordProgression
            {
                bars = roots.Length,
                chords = chords
            };
        }
    }
}
