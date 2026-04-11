using System.Collections.Generic;
using UnityEngine;
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
            List<Vector2> points, StrokeMetrics metrics,
            string groupId, ShapeProfile sp, SoundProfile sound)
        {
            int bars = metrics.averageSize > FourBarThreshold ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            var group = InstrumentGroups.Get(groupId);
            string presetId = group.defaultPresetByType.RhythmLoop;

            int density = Mathf.Clamp(
                Mathf.RoundToInt(6f + sp.angularity * 4f + (1f - sp.symmetry) * 3f + sp.wobble * 3f),
                6, 14);
            float swing = MathUtils.RoundTo(
                Mathf.Clamp(sound.grooveInstability * 0.34f + sp.wobble * 0.08f, 0f, 0.42f), 2);

            var events = new List<RhythmEvent>();

            // Kick pattern
            int[] kickPattern;
            if (sp.aspectRatio < 0.52f)
                kickPattern = new[] { 0, 6, 10, 13 };
            else if (sp.circularity > 0.75f)
                kickPattern = new[] { 0, 8, 12 };
            else
                kickPattern = new[] { 0, 7, 10, 13 };

            // Snare pattern
            int[] snarePattern = sp.symmetry > 0.6f ? new[] { 8 } : new[] { 5, 8, 13 };

            // Hat stride
            int hatStride = sp.angularity > 0.68f ? 1 : 2;

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * AppStateFactory.BarSteps;

                // Kicks
                for (int i = 0; i < kickPattern.Length; i++)
                {
                    int step = kickPattern[i];
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = "kick",
                        velocity = MathUtils.RoundTo(0.6f + sound.body * 0.3f - i * 0.05f, 2),
                        microShift = MathUtils.RoundTo(
                            Mathf.Sin((step + 1f) * 1.13f + sp.wobble * 5f) * sound.grooveInstability * 0.05f, 3)
                    });
                }

                // Snares
                for (int i = 0; i < snarePattern.Length; i++)
                {
                    int step = snarePattern[i];
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = "snare",
                        velocity = MathUtils.RoundTo(0.56f + sound.transientSharpness * 0.26f - i * 0.05f, 2),
                        microShift = MathUtils.RoundTo(
                            Mathf.Sin((step + 3f) * 0.92f + sp.angularity * 4f) * sound.grooveInstability * 0.08f, 3)
                    });
                }

                // Hats and percs
                for (int step = 0; step < AppStateFactory.BarSteps; step += hatStride)
                {
                    if (ArrayContains(kickPattern, step) || ArrayContains(snarePattern, step))
                        continue;

                    float emphasis = step % 4 == 0 ? 0.08f : 0f;
                    events.Add(new RhythmEvent
                    {
                        step = offset + step,
                        lane = step % 2 == 0 ? "hat" : "perc",
                        velocity = MathUtils.RoundTo(0.24f + sound.brightness * 0.22f + emphasis, 2),
                        microShift = MathUtils.RoundTo(
                            Mathf.Sin((step + 2f) * 1.47f + sp.curvatureVariance * 5f) * sound.grooveInstability * 0.18f, 3)
                    });
                }

                // Ghost notes for high density
                if (density > 11 || sp.symmetry < 0.45f)
                {
                    int[] ghostSteps = { 3, 11, 15 };
                    foreach (int step in ghostSteps)
                    {
                        events.Add(new RhythmEvent
                        {
                            step = offset + step,
                            lane = step == 11 ? "snare" : "perc",
                            velocity = MathUtils.RoundTo(0.26f + sound.drive * 0.18f, 2),
                            microShift = MathUtils.RoundTo(
                                Mathf.Sin((step + 4f) * 1.81f) * sound.grooveInstability * 0.12f, 3)
                        });
                    }
                }
            }

            string angularWord = sp.angularity > 0.6f ? "sharp" : "round";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "loop", $"{density} accents", sound.drive > 0.58f ? "heated" : "steady" },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"Closed loop, {bars} bars, {density} accents, swing {Mathf.Round(swing * 100f)}%, {angularWord} transient DNA.",
                details = "Circularity drives kick weight, angularity pushes transient bite and drive, and symmetry loss adds broken micro-timing."
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
