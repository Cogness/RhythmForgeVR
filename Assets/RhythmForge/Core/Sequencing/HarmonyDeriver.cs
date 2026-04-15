using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing
{
    public struct HarmonyDerivationResult
    {
        public int bars;
        public string presetId;
        public List<string> tags;
        public DerivedSequence derivedSequence;
        public string summary;
        public string details;
    }

    public static class HarmonyDeriver
    {
        // World-space thresholds (pilot: length > 1200 → 8 bars, > 700 → 4 bars)
        private const float EightBarThreshold = 1.20f;
        private const float FourBarThreshold = 0.70f;

        public static HarmonyDerivationResult Derive(
            List<Vector2> points, StrokeMetrics metrics,
            string keyName, string groupId,
            ShapeProfile sp, SoundProfile sound)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.HarmonyPad, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.HarmonyPad, sp);
            int bars = metrics.length > EightBarThreshold ? 8 : metrics.length > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.HarmonyPad;

            int rootMidi = PitchUtils.PitchFromRelative(1f - sp.centroidHeight, keyName) - 12;
            // Ensure root is in-key
            rootMidi = MusicalKeys.QuantizeToKey(rootMidi, keyName);

            // Chord flavor from tilt — voiced with diatonic scale steps (not chromatic semitones)
            string flavor;
            int[] scaleDegreeSteps;
            if (sp.tiltSigned > 0.28f)
            {
                flavor = "maj7";
                scaleDegreeSteps = new[] { 0, 2, 4, 6 }; // root, 3rd, 5th, 7th
            }
            else if (sp.tiltSigned < -0.22f)
            {
                flavor = "sus";
                scaleDegreeSteps = new[] { 0, 3, 4, 6 }; // root, 4th, 5th, 7th
            }
            else
            {
                flavor = "minor";
                scaleDegreeSteps = new[] { 0, 2, 4, 6 }; // root, 3rd, 5th, 7th (all diatonic)
            }

            var chord = MusicalKeys.BuildScaleChord(rootMidi, keyName, scaleDegreeSteps);

            // Open voicing: spread upper tones by octave based on horizontal span
            int spread = Mathf.RoundToInt(sp.horizontalSpan * 10f + sizeFactor * 6f);
            if (chord.Count > 2)
                chord[2] += Mathf.RoundToInt(spread * 0.5f) / 12 * 12; // whole octave nudge only
            if (chord.Count > 3)
                chord[3] += (spread > 6 ? 12 : 0);

            // Bass doubling for wide/large shapes
            if (sp.horizontalSpan > 0.72f || sizeFactor > 0.66f)
                chord.Insert(0, rootMidi - 12);

            string bloomWord = sound.reverbBias > 0.56f ? "bloom" : "dry bed";
            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavor, bloomWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    flavor = flavor,
                    rootMidi = rootMidi,
                    chord = chord
                },
                summary = $"{sizeWord} pad, {bars} bars, {flavor} chord, voicing {Mathf.Round(sp.horizontalSpan * 100f)}%, filter motion {Mathf.Round(sound.filterMotion * 100f)}%.",
                details = "Size pushes voicing width, bloom, detune, and movement, while tilt controls chord family and filter direction."
            };
        }
    }
}
