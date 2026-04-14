using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class MelodyLineBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.MelodyLine;
        public string DisplayName => "Melody";
        public bool PrefersClosedStroke => false;

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var result = MelodyDeriver.Derive(points, metrics, keyName, groupId, shapeProfile, soundProfile);
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
            return SoundProfileMapper.DeriveMelody(shapeProfile);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.pattern.derivedSequence.notes == null)
                return;

            foreach (var note in context.pattern.derivedSequence.notes)
            {
                if (note.step != context.localStep)
                    continue;

                float duration = note.durationSteps * context.stepDuration;

                context.audioDispatcher?.PlayMelody(
                    context.preset,
                    note.midi,
                    note.velocity,
                    duration,
                    context.instance.pan,
                    context.instance.brightness,
                    context.instance.depth,
                    context.preset.fxSend + context.group.busFx.delay * 0.1f,
                    context.sound,
                    note.glide);

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime,
                    GetVisualDuration(duration, context.sound));
            }
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            var spec = baseSpec;
            soundProfile = soundProfile ?? new SoundProfile();
            spec.markerScale = Mathf.Clamp01(spec.markerScale + 0.04f);
            spec.haloStrength = Mathf.Clamp01(spec.haloStrength * 0.72f + soundProfile.releaseBias * 0.16f);
            spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength + soundProfile.modDepth * 0.2f);
            spec.motionSpeed = Mathf.Lerp(0.55f, 1.3f, soundProfile.filterMotion * 0.4f + soundProfile.modDepth * 0.6f);
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
                lineEnergy = pulse * 0.36f + sustain * 0.46f,
                haloEnergy = pulse * 0.3f + sustain * 0.54f,
                markerEnergy = Mathf.Max(pulse * 0.95f, sustain * 0.85f),
                markerPhase = state.phase,
                markerScale = state.visualSpec.markerScale * (0.6f + pulse * 0.56f + sustain * 0.34f),
                normalOffset = Mathf.Sin(timeSeconds * (1.2f + state.visualSpec.motionSpeed * 2.4f)) * renderedHeight * 0.12f * state.visualSpec.motionAmplitude,
                haloBreath = 1f + Mathf.Sin(timeSeconds * (1.1f + state.visualSpec.motionSpeed * 1.4f)) * 0.06f * state.visualSpec.secondaryStrength
            };
        }

        private static float GetVisualDuration(float noteDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return noteDuration + 0.06f + sound.releaseBias * 0.42f + sound.body * 0.08f;
        }
    }
}
