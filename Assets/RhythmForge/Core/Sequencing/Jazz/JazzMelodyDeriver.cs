using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz melody deriver (guided): delegates to <see cref="MelodyDeriver.DeriveGuided"/>
    /// so the guided invariants (phrase anchors at step 0 / step 64, strong-beat chord-tone
    /// lock, bars 5–8 answer lift, cadence hold) hold, while overriding the in-key scale
    /// with blues (dark tilt or smooth stroke) or jazz-major (bright/angular), and clamping
    /// to the jazz melody register.
    /// </summary>
    public sealed class JazzMelodyDeriver : IMelodyDeriver
    {
        // Jazz blues scale intervals (relative to root): includes b3, b5, b7
        private static readonly int[] BluesIntervals = { 0, 3, 5, 6, 7, 10 };
        // Jazz major scale (Ionian) for bright passages
        private static readonly int[] JazzMajorIntervals = { 0, 2, 4, 5, 7, 9, 11 };

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

            var policy = GuidedPolicy.Get("jazz");
            string effectiveKey = !string.IsNullOrEmpty(keyName)
                && MusicalKeys.All.ContainsKey(keyName)
                ? keyName
                : policy.keyName;

            var progression = HarmonicContextProvider.CurrentProgression;
            if (progression == null || progression.chords == null || progression.chords.Count == 0)
                progression = policy.CreateDefaultProgression();

            bool useBluesy = sp.tiltSigned < -0.1f || sp.angularity < 0.35f;
            var options = new MelodyDerivationOptions
            {
                scaleIntervals = useBluesy ? BluesIntervals : JazzMajorIntervals,
                genreId = "jazz",
                presetId = genre != null ? genre.GetDefaultPresetId(PatternType.MelodyLine) : "jazz-rhodes",
                styleTag = useBluesy ? "blues" : "bop"
            };

            var result = MelodyDeriver.DeriveGuided(
                points, metrics, effectiveKey, groupId: "jazz", sp, sound, progression, options);

            string styleWord = useBluesy ? "blues" : "bop";
            string articWord = sound.transientSharpness > 0.5f ? "crisp" : "smooth";
            result.tags = new List<string> { styleWord, articWord, progression.bars + " bars" };
            result.summary = $"{ShapeProfileSizing.DescribeSize(PatternType.MelodyLine, sp)} {styleWord} line, {progression.bars} bars in {effectiveKey}, {articWord} articulation.";
            result.details = "Blues scale for dark tilt or smooth strokes, jazz major for bright/angular. Strong beats lock to the current bar's chord tones, phrase anchors guaranteed on bars 1 and 5, positive tilt lifts bars 5–8.";
            return result;
        }
    }
}
