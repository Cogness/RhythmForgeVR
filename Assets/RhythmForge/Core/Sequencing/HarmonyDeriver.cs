using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
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
        public static HarmonyDerivationResult Derive(
            List<Vector2> points, StrokeMetrics metrics,
            string keyName, string groupId,
            ShapeProfile sp, SoundProfile sound)
        {
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.Harmony, sp);
            int bars = GuidedDefaults.Bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.GetDefault(PatternType.Harmony);

            var progression = HarmonyShapeModulator.Modulate(
                metrics,
                sp,
                GuidedDefaults.CreateDefaultProgression(),
                keyName,
                "electronic");

            var firstChord = progression.GetSlotForBar(0);
            string flavorLabel = firstChord != null && !string.IsNullOrEmpty(firstChord.flavor) ? firstChord.flavor : "triad";

            string bloomWord = sound.reverbBias > 0.56f ? "bloom" : "dry bed";
            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavorLabel, bloomWord, "8-bar progression" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    chordEvents = progression.Clone().chords
                },
                summary = $"{sizeWord} {flavorLabel} progression, 8 bars, voicing {Mathf.Round(sp.horizontalSpan * 100f)}%, bloom {Mathf.Round(sound.reverbBias * 100f)}%.",
                details = "Tilt selects the harmonic color, horizontal span picks a shared inversion, centroid height shifts the register, and symmetry can strengthen the cadences in bars 4 and 8."
            };
        }
    }
}
