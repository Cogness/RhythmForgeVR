using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// New Age rhythm deriver: sparse, meditative patterns using soft mallet, shaker, and singing bowl timbres.
    /// Larger shapes → more sparse (breathing space). Circular shapes → bowl-dominant. Angular → shaker accents.
    /// </summary>
    public sealed class NewAgeRhythmDeriver : IRhythmDeriver
    {
        public RhythmDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            ShapeProfile sp,
            SoundProfile sound,
            GenreProfile genre)
        {
            float sizeFactor = ShapeProfileSizing.GetSizeFactor(PatternType.RhythmLoop, sp);
            string sizeWord = ShapeProfileSizing.DescribeSize(PatternType.RhythmLoop, sp);
            int bars = metrics.averageSize > 0.30f ? 4 : 2;
            int totalSteps = bars * AppStateFactory.BarSteps;
            string presetId = genre.GetDefaultPresetId(PatternType.RhythmLoop);

            var role = ShapeRoleProvider.Current;
            int barSteps = AppStateFactory.BarSteps;
            // Temporal step offset per role so patterns don't all trigger on step 0 together
            int roleOffset = role.index == 0 ? 0
                           : role.index == 1 ? barSteps / 3   // ≈ step 5
                           : barSteps * 2 / 3;                // ≈ step 10

            // Sparse density: 2-6 events per bar (meditative space between events)
            int density = Mathf.Clamp(
                Mathf.RoundToInt(2f + sp.angularity * 2f + (1f - sp.circularity) * 1.5f + sizeFactor * 1.5f),
                2, 6);

            // No swing — flowing, even timing; very slight natural wobble
            float swing = MathUtils.RoundTo(sp.wobble * 0.06f, 2);

            var events = new List<RhythmEvent>();

            for (int bar = 0; bar < bars; bar++)
            {
                int offset = bar * barSteps;

                if (role.index == 0)
                {
                    // Primary: singing bowl (kick) on downbeats
                    events.Add(new RhythmEvent
                    {
                        step = (offset + 0 + roleOffset) % totalSteps,
                        lane = "kick",
                        velocity = MathUtils.RoundTo(0.38f + sound.body * 0.28f, 2),
                        microShift = 0f
                    });

                    if (sp.circularity < 0.72f)
                    {
                        events.Add(new RhythmEvent
                        {
                            step = (offset + 8) % totalSteps,
                            lane = "kick",
                            velocity = MathUtils.RoundTo(0.26f + sound.body * 0.18f, 2),
                            microShift = MathUtils.RoundTo(sp.wobble * 0.02f, 3)
                        });
                    }

                    // Shaker on off-beats
                    int[] shakerSteps = sp.angularity > 0.6f ? new[] { 4, 10, 14 }
                                      : sp.symmetry > 0.65f  ? new[] { 6 }
                                                              : new[] { 4, 12 };
                    foreach (int step in shakerSteps)
                    {
                        events.Add(new RhythmEvent
                        {
                            step = (offset + step) % totalSteps,
                            lane = "hat",
                            velocity = MathUtils.RoundTo(0.18f + (1f - sp.angularity) * 0.1f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 1f) * 0.73f + sp.wobble * 3f) * 0.02f, 3)
                        });
                    }

                    if (density >= 4 && (bar == 0 || sp.symmetry < 0.5f))
                    {
                        events.Add(new RhythmEvent
                        {
                            step = (offset + 4) % totalSteps,
                            lane = "perc",
                            velocity = MathUtils.RoundTo(0.22f + sound.body * 0.14f, 2),
                            microShift = 0f
                        });
                    }
                }
                else if (role.index == 1)
                {
                    // Counter: NO bowl — shaker only, on off-beats offset from role 0
                    int[] counterSteps = sp.angularity > 0.5f ? new[] { 2, 9, 13 } : new[] { 3, 11 };
                    foreach (int step in counterSteps)
                    {
                        events.Add(new RhythmEvent
                        {
                            step = (offset + step + roleOffset) % totalSteps,
                            lane = "hat",
                            velocity = MathUtils.RoundTo(0.14f + (1f - sp.angularity) * 0.08f, 2),
                            microShift = MathUtils.RoundTo(Mathf.Sin((step + 2f) * 0.91f + sp.wobble * 2f) * 0.02f, 3)
                        });
                    }
                }
                else
                {
                    // Role 2+: ghost perc only — no bowl, no shaker
                    if (bar % 2 == 0)
                    {
                        events.Add(new RhythmEvent
                        {
                            step = (offset + roleOffset) % totalSteps,
                            lane = "perc",
                            velocity = MathUtils.RoundTo(0.12f + sound.body * 0.08f, 2),
                            microShift = MathUtils.RoundTo(sp.wobble * 0.015f, 3)
                        });
                    }
                }
            }

            string roleWord = role.index == 0 ? "bowl" : role.index == 1 ? "shaker" : "ghost";
            string densityWord = density <= 3 ? "sparse" : "flowing";
            return new RhythmDerivationResult
            {
                bars = bars,
                presetId = presetId,
                tags = new List<string> { "meditation", densityWord, roleWord },
                derivedSequence = new DerivedSequence
                {
                    kind = "rhythm",
                    totalSteps = totalSteps,
                    swing = swing,
                    events = events
                },
                summary = $"{sizeWord} {roleWord} pattern, {bars} bars, {density} touches, {densityWord} space.",
                details = "Circularity drives bowl resonance, angularity shapes shaker presence, size determines breath space between beats."
            };
        }
    }
}
