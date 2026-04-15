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
            rootMidi = MusicalKeys.QuantizeToKey(rootMidi, keyName);

            // Open, airy voicings — use diatonic scale steps for in-key notes.
            // Sus-style: skip 3rd (degree 2), favour 2nd (degree 1) and 5th (degree 4).
            string flavor;
            int[] scaleDegreeSteps;
            if (sp.tiltSigned > 0.22f)
            {
                flavor = "sus2";
                scaleDegreeSteps = new[] { 0, 1, 4, 7 }; // root, 2nd, 5th, octave-2nd
            }
            else if (sp.tiltSigned < -0.18f)
            {
                flavor = "sus4";
                scaleDegreeSteps = new[] { 0, 3, 4, 7 }; // root, 4th, 5th, octave-2nd
            }
            else
            {
                flavor = "drone5";
                scaleDegreeSteps = new[] { 0, 4, 7, 11 }; // root, 5th, octave, 5th-above
            }

            var chord = MusicalKeys.BuildScaleChord(rootMidi, keyName, scaleDegreeSteps);

            // Wide spread for spacious New Age voicing
            int spread = Mathf.RoundToInt(sp.horizontalSpan * 8f + sizeFactor * 4f);
            if (chord.Count > 0)
                chord[chord.Count - 1] += Mathf.RoundToInt(spread * 0.3f) / 12 * 12;

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
