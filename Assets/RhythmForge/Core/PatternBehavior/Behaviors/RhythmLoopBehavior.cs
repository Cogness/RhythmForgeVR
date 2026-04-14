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

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var result = RhythmDeriver.Derive(points, metrics, groupId, shapeProfile, soundProfile);
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
            return SoundProfileMapper.DeriveRhythm(shapeProfile);
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
                    context.sound);

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime,
                    GetVisualDuration(evt.lane, context.sound));
            }
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            var spec = baseSpec;
            soundProfile = soundProfile ?? new SoundProfile();
            spec.markerScale = Mathf.Clamp01(spec.markerScale + 0.12f);
            spec.haloStrength = Mathf.Clamp01(spec.haloStrength * 0.55f + soundProfile.body * 0.08f);
            spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength * 0.42f + soundProfile.grooveInstability * 0.28f);
            spec.phaseJitter = Mathf.Clamp01(spec.phaseJitter + soundProfile.grooveInstability * 0.22f);
            spec.motionSpeed = Mathf.Lerp(0.85f, 1.9f, soundProfile.grooveInstability * 0.45f + soundProfile.transientSharpness * 0.55f);
            return spec;
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return new AnimationEnergies
            {
                lineEnergy = pulse * 0.72f + sustain * 0.22f,
                haloEnergy = pulse * 0.55f + sustain * 0.18f,
                markerEnergy = state.phase >= 0f ? Mathf.Max(0.15f, pulse, sustain * 0.3f) : 0f,
                markerPhase = state.phase >= 0f
                    ? Mathf.Repeat(
                        state.phase + Mathf.Sin(timeSeconds * (1.6f + state.visualSpec.motionSpeed * 0.8f)) * state.visualSpec.phaseJitter * 0.015f,
                        1f)
                    : -1f,
                markerScale = state.visualSpec.markerScale * (0.72f + pulse * 0.84f + sustain * 0.2f),
                normalOffset = Mathf.Sin(timeSeconds * (2.2f + state.visualSpec.motionSpeed)) * renderedHeight * 0.02f * state.visualSpec.secondaryStrength,
                haloBreath = 1f
            };
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
    }
}
