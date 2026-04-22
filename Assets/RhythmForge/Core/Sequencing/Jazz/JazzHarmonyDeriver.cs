using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Sequencing;

namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Jazz harmony deriver (guided): emits an 8-bar ii-V-i progression in D minor
    /// seeded from <see cref="GuidedPolicy"/>, modulated by shape through
    /// <see cref="HarmonyShapeModulator"/> using the jazz flavor vocabulary.
    /// Shape tilt steers maj7 / min7 / dom7 / min7b5. Cadence bars (4 and 8)
    /// always add a 9 (low symmetry) or 13 (high symmetry).
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
            sp = sp ?? new ShapeProfile();
            sound = sound ?? new SoundProfile();

            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.HarmonyPad, sp);
            var policy = GuidedPolicy.Get("jazz");
            int bars = policy.bars;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre != null
                ? genre.GetDefaultPresetId(PatternType.HarmonyPad)
                : "jazz-comp";

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
                "jazz",
                HarmonyFlavorPolicies.Jazz);

            var firstChord = progression.GetSlotForBar(0);
            string flavorLabel = firstChord != null && !string.IsNullOrEmpty(firstChord.flavor)
                ? firstChord.flavor
                : "min7";
            string tensionWord = flavorLabel.Contains("dom7") || flavorLabel.Contains("min7b5")
                ? "tension"
                : "stable";

            return new HarmonyDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { flavorLabel, tensionWord, "ii-V-i" },
                derivedSequence = new DerivedSequence
                {
                    kind = "harmony",
                    totalSteps = totalSteps,
                    chordEvents = progression.Clone().chords
                },
                summary = $"{sizeWord} {flavorLabel} voicing, {bars} bars, ii-V-i in {effectiveKey}.",
                details = "Tilt picks the chord family (maj7/min7/dom7/min7♭5). Shape symmetry lifts bars 4 and 8 by adding the 9 or 13. Horizontal span opens the voicing inversion, vertical span doubles the pad an octave up."
            };
        }
    }
}
