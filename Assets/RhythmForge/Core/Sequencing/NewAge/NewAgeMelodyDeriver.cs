using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age melody deriver (guided): delegates to <see cref="MelodyDeriver.DeriveGuided"/>
    /// so the guided invariants (phrase anchors, chord-tone strong beats, bars 5–8 lift,
    /// cadence hold) hold while restricting the non-strong-beat pitch pool to the major
    /// pentatonic scale for harmonic safety. Register is clamped to the NewAge melody range.
    /// </summary>
    public sealed class NewAgeMelodyDeriver : IMelodyDeriver
    {
        // Major pentatonic intervals (relative to the key root).
        private static readonly int[] PentatonicIntervals = { 0, 2, 4, 7, 9 };

        public MelodyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            sp = sp ?? new ShapeProfile();
            sound = sound ?? new SoundProfile();

            var policy = GuidedPolicy.Get("newage");
            string effectiveKey = !string.IsNullOrEmpty(keyName)
                && MusicalKeys.All.ContainsKey(keyName)
                ? keyName
                : policy.keyName;

            var progression = HarmonicContextProvider.CurrentProgression;
            if (progression == null || progression.chords == null || progression.chords.Count == 0)
                progression = policy.CreateDefaultProgression();

            var options = new MelodyDerivationOptions
            {
                scaleIntervals = PentatonicIntervals,
                genreId = "newage",
                presetId = genre != null ? genre.GetDefaultPresetId(PatternType.MelodyLine) : "newage-kalimba",
                styleTag = "pentatonic"
            };

            var result = MelodyDeriver.DeriveGuided(
                points, metrics, effectiveKey, groupId: "newage", sp, sound, progression, options);

            string flowWord = sp.angularity < 0.4f ? "flowing" : "stepping";
            result.tags = new List<string> { "pentatonic", "lead", flowWord };
            result.summary = $"{ShapeProfileSizing.DescribeSize(PatternType.MelodyLine, sp)} kalimba lead, {progression.bars} bars in {effectiveKey}, {flowWord} phrasing.";
            result.details = "Major pentatonic restricts non-chord-tone pitches for harmonic safety. Strong beats snap to the current bar's chord, phrase anchors hold on bars 1 and 5, positive tilt lifts bars 5–8.";
            return result;
        }
    }
}
