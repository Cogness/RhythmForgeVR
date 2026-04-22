using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Sequencing
{
    public static class HarmonyShapeModulator
    {
        public static ChordProgression Modulate(
            StrokeMetrics metrics,
            ShapeProfile shapeProfile,
            ChordProgression defaults,
            string keyName,
            string genreId)
        {
            return Modulate(metrics, shapeProfile, defaults, keyName, genreId,
                HarmonyFlavorPolicies.Get(genreId));
        }

        public static ChordProgression Modulate(
            StrokeMetrics metrics,
            ShapeProfile shapeProfile,
            ChordProgression defaults,
            string keyName,
            string genreId,
            IHarmonyFlavorPolicy flavorPolicy)
        {
            flavorPolicy = flavorPolicy ?? HarmonyFlavorPolicies.Default;
            var baseProgression = defaults?.Clone() ?? GuidedDefaults.CreateDefaultProgression();
            if (baseProgression.chords == null)
                baseProgression.chords = new List<ChordSlot>();

            int inversion = GetInversion(shapeProfile);
            bool addUpperOctave = shapeProfile != null && shapeProfile.verticalSpan > 0.5f;
            float symmetry = shapeProfile != null ? shapeProfile.symmetry : 1f;
            var harmonyRange = RegisterPolicy.GetRange(PatternType.Harmony, genreId);

            for (int i = 0; i < baseProgression.chords.Count; i++)
            {
                var original = baseProgression.chords[i];
                if (original == null)
                    continue;

                string flavor = flavorPolicy.PickBaseFlavor(shapeProfile);
                int[] degrees = flavorPolicy.GetScaleDegrees(flavor);
                var voicing = MusicalKeys.BuildScaleChord(original.rootMidi, keyName, degrees);

                bool cadenceBar = original.barIndex == 3 || original.barIndex == 7;
                if (cadenceBar)
                    ApplyCadenceLift(voicing, original.rootMidi, keyName, symmetry, flavorPolicy, ref flavor);

                ApplyInversion(voicing, inversion);

                if (addUpperOctave)
                {
                    int copyCount = voicing.Count;
                    for (int n = 0; n < copyCount; n++)
                        voicing.Add(voicing[n] + 12);
                }

                ShiftVoicingToCentroid(voicing, shapeProfile, harmonyRange.min, harmonyRange.max);
                QuantizeAndClampVoicing(voicing, keyName, genreId);

                original.flavor = flavor;
                original.voicing = voicing;
            }

            return baseProgression;
        }

        private static int GetInversion(ShapeProfile shapeProfile)
        {
            if (shapeProfile == null)
                return 0;

            if (shapeProfile.horizontalSpan >= 0.67f)
                return 2;
            if (shapeProfile.horizontalSpan >= 0.34f)
                return 1;
            return 0;
        }

        private static void ApplyInversion(List<int> voicing, int inversion)
        {
            if (voicing == null || voicing.Count < 2)
                return;

            int steps = Mathf.Clamp(inversion, 0, 2);
            for (int i = 0; i < steps; i++)
            {
                int note = voicing[0];
                voicing.RemoveAt(0);
                voicing.Add(note + 12);
            }
        }

        private static void ShiftVoicingToCentroid(List<int> voicing, ShapeProfile shapeProfile, int minMidi, int maxMidi)
        {
            if (voicing == null || voicing.Count == 0 || shapeProfile == null)
                return;

            float average = 0f;
            for (int i = 0; i < voicing.Count; i++)
                average += voicing[i];

            average /= voicing.Count;
            float target = Mathf.Lerp(minMidi + 4f, maxMidi - 4f, Mathf.Clamp01(shapeProfile.centroidHeight));
            int shift = Mathf.RoundToInt(target - average);
            for (int i = 0; i < voicing.Count; i++)
                voicing[i] += shift;
        }

        private static void QuantizeAndClampVoicing(List<int> voicing, string keyName, string genreId)
        {
            if (voicing == null)
                return;

            var unique = new HashSet<int>();
            for (int i = 0; i < voicing.Count; i++)
            {
                int midi = MusicalKeys.QuantizeToKey(voicing[i], keyName);
                midi = RegisterPolicy.Clamp(midi, PatternType.Harmony, genreId);
                midi = MusicalKeys.QuantizeToKey(midi, keyName);
                unique.Add(midi);
            }

            voicing.Clear();
            voicing.AddRange(unique);
            voicing.Sort();
        }

        private static void ApplyCadenceLift(
            List<int> voicing,
            int rootMidi,
            string keyName,
            float symmetry,
            IHarmonyFlavorPolicy flavorPolicy,
            ref string flavor)
        {
            var lift = flavorPolicy.PickCadenceLift(flavor, symmetry);
            if (lift.addDegrees != null)
            {
                for (int d = 0; d < lift.addDegrees.Length; d++)
                {
                    var single = MusicalKeys.BuildScaleChord(rootMidi, keyName, new[] { lift.addDegrees[d] });
                    if (single != null && single.Count > 0)
                        AddPitchClassIfMissing(voicing, single[0]);
                }
            }

            if (!string.IsNullOrEmpty(lift.newFlavor))
                flavor = lift.newFlavor;
        }

        private static void AddPitchClassIfMissing(List<int> voicing, int midi)
        {
            if (voicing == null)
                return;

            int pitchClass = ((midi % 12) + 12) % 12;
            for (int i = 0; i < voicing.Count; i++)
            {
                if ((((voicing[i] % 12) + 12) % 12) == pitchClass)
                    return;
            }

            voicing.Add(midi);
        }
    }
}
