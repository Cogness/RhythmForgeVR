using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz harmony deriver: 7th, 9th, 13th chord voicings with ii-V-I flavors.
    /// Tilt → chord quality (maj7/min7/dom7/dim7). Horizontal span → voicing spread.
    /// Circularity → smoother block chord vs. angular for sus/altered.
    /// </summary>
    public sealed class JazzHarmonyDeriver : IHarmonyDeriver
    {
        public HarmonyDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.HarmonyPad, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.HarmonyPad, sp);
            int bars = metrics.length > 1.20f ? 8 : metrics.length > 0.70f ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre.GetDefaultPresetId(PatternType.HarmonyPad);

            int rootMidi = PitchUtils.PitchFromRelative(1f - sp.centroidHeight, keyName) - 12;

            // Jazz chord flavors determined by tilt and circularity
            string flavor;
            int[] chordIntervals;

            if (sp.tiltSigned > 0.35f)
            {
                // Major 7th — bright, Imaj7
                flavor = "maj7";
                chordIntervals = new[] { 0, 4, 7, 11 };
            }
            else if (sp.tiltSigned > 0.1f)
            {
                // Dominant 7th — V7, tension
                flavor = "dom7";
                chordIntervals = new[] { 0, 4, 7, 10 };
            }
            else if (sp.tiltSigned < -0.3f)
            {
                // Diminished 7th — altered, dark tension
                flavor = "dim7";
                chordIntervals = new[] { 0, 3, 6, 9 };
            }
            else if (sp.circularity > 0.65f)
            {
                // Minor 7th — smooth, iim7
                flavor = "min7";
                chordIntervals = new[] { 0, 3, 7, 10 };
            }
            else
            {
                // Minor 9th — richer iim9
                flavor = "min9";
                chordIntervals = new[] { 0, 3, 7, 10, 14 };
            }

            // Spread voicing based on horizontal span
            int spread = Mathf.RoundToInt(sp.horizontalSpan * 12f + sizeFactor * 6f);
            var chord = new List<int>();

            for (int i = 0; i < chordIntervals.Length; i++)
            {
                int note = rootMidi + chordIntervals[i];
                // Open up upper voicing
                if (i == 2)
                    note += Mathf.RoundToInt(spread * 0.5f);
                else if (i >= 3)
                    note += spread + (i - 3) * 4;

                chord.Add(note);
            }

            // Walking bass root for wide shapes
            if (sp.horizontalSpan > 0.6f || sizeFactor > 0.58f)
                chord.Insert(0, rootMidi - 12);

            string tensionWord = flavor == "dim7" || flavor == "dom7" ? "tension" : "stable";
            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavor, tensionWord, "jazz voicing" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    flavor = flavor,
                    rootMidi = rootMidi,
                    chord = chord
                },
                summary = $"{sizeWord} {flavor} voicing, {bars} bars, {Mathf.Round(sp.horizontalSpan * 100f)}% spread.",
                details = "Tilt controls chord family (maj7→dom7→min7→dim7). Horizontal span opens the voicing, size pushes the comping duration."
            };
        }
    }
}
