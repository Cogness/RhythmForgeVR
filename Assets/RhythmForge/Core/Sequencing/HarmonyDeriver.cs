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
            int bars = metrics.length > EightBarThreshold ? 8 : metrics.length > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.HarmonyPad;

            int rootMidi = PitchUtils.PitchFromRelative(1f - sp.centroidHeight, keyName) - 12;

            // Chord flavor from tilt
            string flavor;
            int[] chordIntervals;
            if (sp.tiltSigned > 0.28f)
            {
                flavor = "maj7";
                chordIntervals = new[] { 0, 4, 7, 11 };
            }
            else if (sp.tiltSigned < -0.22f)
            {
                flavor = "sus";
                chordIntervals = new[] { 0, 5, 7, 10 };
            }
            else
            {
                flavor = "minor";
                chordIntervals = new[] { 0, 3, 7, 10 };
            }

            int spread = Mathf.RoundToInt(sp.horizontalSpan * 10f);
            var chord = new List<int>();

            for (int i = 0; i < chordIntervals.Length; i++)
            {
                int note = rootMidi + chordIntervals[i];

                if (i == 0 && sp.horizontalSpan > 0.72f)
                    note -= 12;
                else if (i == 2)
                    note += Mathf.RoundToInt(spread * 0.45f);
                else if (i == 3)
                    note += spread;

                chord.Add(note);
            }

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
                summary = $"{bars} bars, {flavor} chord, wide voicing {Mathf.Round(sp.horizontalSpan * 100f)}%, filter motion {Mathf.Round(sound.filterMotion * 100f)}%.",
                details = "Tilt controls chord family and filter movement, width opens the voicing, and path length pushes bloom and tail size."
            };
        }
    }
}
