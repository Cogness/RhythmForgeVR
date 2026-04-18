using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class RhythmLoopBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.RhythmLoop;
        public string DisplayName => "Rhythm";
        public bool PrefersClosedStroke => true;
        public string DraftNamePrefix => "Beat";

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var genre = GenreRegistry.GetActive();
            var result = genre.RhythmDeriver.Derive(points, metrics, shapeProfile, soundProfile, genre);

            // Phase 3 — 3D stroke behaviour modifications
            result = Apply3DRhythmModifications(result, shapeProfile);

            return new PatternDerivationResult
            {
                bars = result.bars,
                presetId = result.presetId,
                tags = result.tags,
                derivedSequence = result.derivedSequence,
                summary = result.summary,
                details = result.details
            };
        }

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.RhythmLoop).Evaluate(PatternType.RhythmLoop, shapeProfile);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.pattern.derivedSequence.events == null)
                return;

            foreach (var evt in context.pattern.derivedSequence.events)
            {
                if (evt.step != context.localStep)
                    continue;

                context.audioDispatcher?.PlayDrum(
                    context.preset,
                    evt.lane,
                    evt.velocity,
                    context.instance.pan,
                    context.instance.brightness,
                    context.instance.depth,
                    context.preset.fxSend + context.group.busFx.reverb * 0.2f,
                    context.sound,
                    context.instance.id);

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime,
                    GetVisualDuration(evt.lane, context.sound));
            }
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            return VisualGrammarProfiles.GetRhythmLoop().Apply(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return VisualGrammarProfiles.GetRhythmLoop().Animate(state, pulse, sustain, renderedHeight, timeSeconds);
        }

        private static float GetVisualDuration(string lane, SoundProfile sound)
        {
            float baseDuration = lane == "kick"
                ? 0.22f
                : lane == "snare" ? 0.18f
                : lane == "perc" ? 0.14f : 0.1f;

            sound = sound ?? new SoundProfile();
            return baseDuration + sound.body * 0.14f + sound.releaseBias * 0.24f;
        }

        private static RhythmDerivationResult Apply3DRhythmModifications(
            RhythmDerivationResult result, ShapeProfile sp)
        {
            if (sp == null || result.derivedSequence == null) return result;

            // ── Single-shot accent (Grand Jeté Strike) ─────────────────────────
            // A short, forward-jabbing stroke with high angularity → one hit, not a loop.
            bool isThrust     = sp.thrustAxis > 0.6f;
            bool isShort      = sp.strokeSeconds > 0f && sp.strokeSeconds < 0.35f;
            bool isAngular    = sp.angularity > 0.55f;

            if (isThrust && isShort && isAngular && result.derivedSequence.events != null)
            {
                // Keep only the first hit at step 0 (highest velocity), clear the rest.
                var singleShot = new List<RhythmEvent>();
                RhythmEvent best = null;
                foreach (var evt in result.derivedSequence.events)
                {
                    if (evt.step == 0 || best == null)
                    {
                        if (best == null || evt.velocity > best.velocity)
                            best = evt;
                    }
                }
                if (best != null)
                {
                    best.step = 0;
                    best.velocity = Mathf.Min(1f, best.velocity + 0.15f); // accent the hit
                    singleShot.Add(best);
                }
                result.derivedSequence.events = singleShot;
                // Keep totalSteps at the original bar length — it still loops, just has one accent hit per bar
            }

            // ── Shaker / ride bed (non-planar stroke = textural) ──────────────
            // Low planarity → the stroke was 3D. Add a rhythmic texture layer.
            if (sp.planarity < 0.5f && result.derivedSequence.events != null && result.derivedSequence.totalSteps > 0)
            {
                int totalSteps = result.derivedSequence.totalSteps;
                float textureDensity = Mathf.Lerp(4, 8, 1f - sp.planarity * 2f); // 4–8 evenly-spaced hits
                int spacing = Mathf.Max(1, Mathf.RoundToInt(totalSteps / textureDensity));
                float textureVelocity = Mathf.Lerp(0.18f, 0.32f, 1f - sp.planarity * 2f);

                for (int step = 0; step < totalSteps; step += spacing)
                {
                    // Avoid doubling up on existing events at the same step
                    bool alreadyHit = false;
                    foreach (var existing in result.derivedSequence.events)
                        if (existing.step == step) { alreadyHit = true; break; }

                    if (!alreadyHit)
                    {
                        result.derivedSequence.events.Add(new RhythmEvent
                        {
                            step = step,
                            lane = "hat",          // hi-hat shaker feel
                            velocity = textureVelocity,
                            microShift = 0f
                        });
                    }
                }
            }

            return result;
        }
    }
}
