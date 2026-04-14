using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age harmony deriver: open, spacious voicings (sus2/sus4/5ths), long sustained drones.
    /// Tilt → modal color (light vs. dark). Horizontal span → voicing width. Size → bars.
    /// </summary>
    public sealed class NewAgeHarmonyDeriver : IHarmonyDeriver
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
            // New Age always holds longer — 4 or 8 bars
            int bars = metrics.length > 0.70f ? 8 : 4;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre.GetDefaultPresetId(PatternType.HarmonyPad);

            int rootMidi = PitchUtils.PitchFromRelative(1f - sp.centroidHeight, keyName) - 12;

            // Open, airy voicings — avoid thirds, prefer 5ths, 4ths, octaves
            string flavor;
            int[] chordIntervals;
            if (sp.tiltSigned > 0.22f)
            {
                flavor = "sus2";
                chordIntervals = new[] { 0, 2, 7, 12 }; // root, maj2nd, 5th, octave
            }
            else if (sp.tiltSigned < -0.18f)
            {
                flavor = "sus4";
                chordIntervals = new[] { 0, 5, 7, 12 }; // root, 4th, 5th, octave
            }
            else
            {
                flavor = "drone5";
                chordIntervals = new[] { 0, 7, 12, 19 }; // root, 5th, octave, 5th above
            }

            int spread = Mathf.RoundToInt(sp.horizontalSpan * 8f + sizeFactor * 4f);
            var chord = new List<int>();

            for (int i = 0; i < chordIntervals.Length; i++)
            {
                int note = rootMidi + chordIntervals[i];
                if (i == chordIntervals.Length - 1)
                    note += Mathf.RoundToInt(spread * 0.3f);
                chord.Add(note);
            }

            // Add bass root for depth on large shapes
            if (sizeFactor > 0.55f || sp.horizontalSpan > 0.6f)
                chord.Insert(0, rootMidi - 12);

            string depthWord = sound.reverbBias > 0.5f ? "deep bloom" : "open space";
            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavor, depthWord, "meditative" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    flavor = flavor,
                    rootMidi = rootMidi,
                    chord = chord
                },
                summary = $"{sizeWord} {flavor} drone, {bars} bars, open {Mathf.Round(sp.horizontalSpan * 100f)}% spread.",
                details = "Open voicings avoid dissonance. Tilt steers modal flavor, size gives breath length, and horizontal span opens the overtone space."
            };
        }
    }
}
