using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age harmony deriver (guided): emits an 8-bar floating I-V-vi-IV progression
    /// in C major seeded from <see cref="GuidedPolicy"/>, modulated by shape through
    /// <see cref="HarmonyShapeModulator"/> using the NewAge flavor vocabulary (sus2/sus4/drone5/triad).
    /// Cadence bars (4 and 8) always add a 9 (low symmetry) or 11 (high symmetry).
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
            sp = sp ?? new ShapeProfile();
            sound = sound ?? new SoundProfile();

            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.HarmonyPad, sp);
            var policy = GuidedPolicy.Get("newage");
            int bars = policy.bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre != null
                ? genre.GetDefaultPresetId(PatternType.HarmonyPad)
                : "newage-drone";

            string effectiveKey = !string.IsNullOrEmpty(keyName)
                && MusicalKeys.All.ContainsKey(keyName)
                ? keyName
                : policy.keyName;

            var seed = policy.CreateDefaultProgression();
            var progression = HarmonyShapeModulator.Modulate(
                metrics,
                sp,
                seed,
                effectiveKey,
                "newage",
                HarmonyFlavorPolicies.NewAge);

            var firstChord = progression.GetSlotForBar(0);
            string flavorLabel = firstChord != null && !string.IsNullOrEmpty(firstChord.flavor)
                ? firstChord.flavor
                : "triad";
            string depthWord = sound.reverbBias > 0.5f ? "deep bloom" : "open space";

            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavorLabel, depthWord, "meditative" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    chordEvents = progression.Clone().chords
                },
                summary = $"{sizeWord} {flavorLabel} drone, {bars} bars in {effectiveKey}, open {Mathf.Round(sp.horizontalSpan * 100f)}% spread.",
                details = "Tilt steers sus2/sus4 color. Circular shapes drop to bare 5ths (drone). Symmetry on bars 4 and 8 lifts the voicing with an added 9 or 11."
            };
        }
    }
}
