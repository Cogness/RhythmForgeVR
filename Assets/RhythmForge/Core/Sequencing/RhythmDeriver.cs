using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing
{
    public struct RhythmDerivationResult
    {
        public int bars;
        public string presetId;
        public List<string> tags;
        public DerivedSequence derivedSequence;
        public string summary;
        public string details;
    }

    public static class RhythmDeriver
    {
        // World-space threshold (pilot: averageSize > 240px)
        private const float FourBarThreshold = 0.30f;

        public static RhythmDerivationResult Derive(
            StrokeCurve curve, StrokeMetrics metrics,
            string groupId, ShapeProfile sp, SoundProfile sound)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.RhythmLoop, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.RhythmLoop, sp);
            int bars = metrics.averageSize > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.RhythmLoop;

            var role = ShapeRoleProvider.Current;
            int barSteps = AppStateFactory.BarSteps;
            // Per-role step offset so multiple patterns don't all fire on beat 0 simultaneously
            int roleOffset = role.index == 0 ? 0
                           : role.index == 1 ? barSteps / 4   // step 4 — backbeat shift
                           : barSteps / 2;                     // step 8 — back half

            int density = Mathf.Clamp(
                Mathf.RoundToInt(6f + sp.angularity * 4f + (1f - sp.symmetry) * 3f + sp.wobble * 3f + sizeFactor * 4f),
                6, 14);
            float swing = MathUtils.RoundTo(
                Mathf.Clamp(sound.grooveInstability * 0.34f + sp.wobble * 0.08f + sizeFactor * 0.06f, 0f, 0.42f), 2);

            var events = new List<RhythmEvent>();

            // Kick / snare patterns
            int[] kickPattern;
            if (sp.aspectRatio < 0.52f)
                kickPattern = new[] { 0, 6, 10, 13 };
            else if (sp.circularity > 0.75f)
                kickPattern = new[] { 0, 8, 12 };
            else
                kickPattern = new[] { 0, 7, 10, 13 };

            int[] snarePattern = sp.symmetry > 0.6f ? new[] { 8 } : new[] { 5, 8, 13 };
            int hatStride = (sp.angularity > 0.68f || sizeFactor > 0.68f) ? 1 : 2;

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * barSteps;

                if (role.index == 0)
                {
                    // Primary: full kick + snare + hat
                    for (int i = 0; i < kickPattern.Length; i++)
                    {
                        int step = kickPattern[i];
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = "kick",
                            velocity = MathUtils.RoundTo(0.6f + sound.body * 0.3f - i * 0.05f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 1f) * 1.13f + sp.wobble * 5f) * sound.grooveInstability * 0.05f, 3)
                        });
                    }

                    for (int i = 0; i < snarePattern.Length; i++)
                    {
                        int step = snarePattern[i];
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = "snare",
                            velocity = MathUtils.RoundTo(0.56f + sound.transientSharpness * 0.26f - i * 0.05f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 3f) * 0.92f + sp.angularity * 4f) * sound.grooveInstability * 0.08f, 3)
                        });
                    }

                    for (int step = 0; step < barSteps; step += hatStride)
                    {
                        if (ArrayContains(kickPattern, step) || ArrayContains(snarePattern, step)) continue;
                        float emphasis = step % 4 == 0 ? 0.08f : 0f;
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = step % 2 == 0 ? "hat" : "perc",
                            velocity = MathUtils.RoundTo(0.24f + sound.brightness * 0.22f + emphasis, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 2f) * 1.47f + sp.curvatureVariance * 5f) * sound.grooveInstability * 0.18f, 3)
                        });
                    }

                    if (density > 11 || sp.symmetry < 0.45f || sizeFactor > 0.72f)
                    {
                        int[] ghostSteps = { 3, 11, 15 };
                        foreach (int step in ghostSteps)
                        {
                            events.Add(new RhythmEvent
                            {
                                step = offset + step,
                                lane = step == 11 ? "snare" : "perc",
                                velocity = MathUtils.RoundTo(0.26f + sound.drive * 0.18f, 2),
                                microShift = MathUtils.RoundTo(Mathf.Sin((step + 4f) * 1.81f) * sound.grooveInstability * 0.12f, 3)
                            });
                        }
                    }
                }
                else if (role.index == 1)
                {
                    // Counter: snare + hat only (no kick), offset so it fills around role 0
                    for (int i = 0; i < snarePattern.Length; i++)
                    {
                        int step = (snarePattern[i] + roleOffset) % barSteps;
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = "snare",
                            velocity = MathUtils.RoundTo(0.42f + sound.transientSharpness * 0.2f - i * 0.04f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 3f) * 0.92f + sp.angularity * 4f) * sound.grooveInstability * 0.08f, 3)
                        });
                    }

                    int counterHatStride = hatStride <= 1 ? 2 : hatStride;
                    for (int step = 0; step < barSteps; step += counterHatStride)
                    {
                        int shifted = (step + roleOffset) % barSteps;
                        if (ArrayContains(snarePattern, shifted)) continue;
                        events.Add(new RhythmEvent
                        {
                            step = offset + shifted,
                            lane = "hat",
                            velocity = MathUtils.RoundTo(0.18f + sound.brightness * 0.16f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((shifted + 2f) * 1.47f + sp.curvatureVariance * 5f) * sound.grooveInstability * 0.14f, 3)
                        });
                    }
                }
                else
                {
                    // Role 2+: sparse hat / perc ghosts only, interleaved in the step offset gap
                    int ghostHatStride = 4; // every quarter note only
                    for (int step = 0; step < barSteps; step += ghostHatStride)
                    {
                        int shifted = (step + roleOffset) % barSteps;
                        events.Add(new RhythmEvent
                        {
                            step = offset + shifted,
                            lane = "perc",
                            velocity = MathUtils.RoundTo(0.14f + sound.brightness * 0.10f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((shifted + 1f) * 1.81f) * sound.grooveInstability * 0.10f, 3)
                        });
                    }
                }
            }

            string roleLabel  = role.index == 0 ? "full" : role.index == 1 ? "counter" : "ghost";
            string angularWord = sp.angularity > 0.6f ? "sharp" : "round";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { roleLabel, $"{density} accents", sound.drive > 0.58f ? "heated" : "steady" },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} {roleLabel} loop, {bars} bars, {density} accents, swing {Mathf.Round(swing * 100f)}%, {angularWord} DNA.",
                details = "Size pushes body, tail, space, and groove looseness, while circularity drives kick weight and angularity sharpens the transient bite."
            };
        }

        private static bool ArrayContains(int[] array, int value)
        {
            foreach (var v in array)
                if (v == value) return true;
            return false;
        }
    }
}
