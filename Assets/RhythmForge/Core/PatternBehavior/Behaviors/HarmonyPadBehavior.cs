using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class HarmonyPadBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.HarmonyPad;
        public string DisplayName => "Harmony";
        public bool PrefersClosedStroke => false;

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var result = HarmonyDeriver.Derive(points, metrics, keyName, groupId, shapeProfile, soundProfile);
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
            return SoundProfileMapper.DeriveHarmony(shapeProfile);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.localStep != 0 || context.pattern.derivedSequence.chord == null)
                return;

            int totalSteps = context.pattern.derivedSequence.totalSteps > 0
                ? context.pattern.derivedSequence.totalSteps
                : AppStateFactory.BarSteps;
            int effectiveSteps = totalSteps;

            if (context.transport.mode == "arrangement" && context.transport.slotIndex >= 0)
            {
                var slot = context.appState.arrangement[context.transport.slotIndex];
                if (slot != null)
                    effectiveSteps = Mathf.Min(totalSteps, slot.bars * AppStateFactory.BarSteps);
            }

            float duration = effectiveSteps * context.stepDuration * 0.96f;

            context.audioDispatcher?.PlayChord(
                context.preset,
                context.pattern.derivedSequence.chord,
                0.38f,
                duration,
                context.instance.pan,
                context.instance.brightness,
                context.instance.depth,
                context.preset.fxSend + context.group.busFx.reverb * 0.18f,
                context.sound);

            context.recordTrigger?.Invoke(
                context.instance.id,
                context.scheduledTime,
                GetVisualDuration(duration, context.sound));
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            var spec = baseSpec;
            soundProfile = soundProfile ?? new SoundProfile();
            spec.markerScale = Mathf.Clamp01(spec.markerScale * 0.72f + soundProfile.body * 0.08f);
            spec.haloStrength = Mathf.Clamp01(spec.haloStrength + soundProfile.stereoSpread * 0.2f + soundProfile.reverbBias * 0.18f);
            spec.secondaryStrength = Mathf.Clamp01(spec.secondaryStrength + soundProfile.releaseBias * 0.2f + soundProfile.modDepth * 0.16f);
            spec.motionSpeed = Mathf.Lerp(0.2f, 0.72f, soundProfile.filterMotion * 0.45f + soundProfile.modDepth * 0.55f);
            spec.phaseJitter = Mathf.Clamp01(spec.phaseJitter * 0.35f);
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
                lineEnergy = pulse * 0.16f + sustain * 0.5f,
                haloEnergy = Mathf.Max(sustain * 0.92f, state.isActive ? 0.18f : 0f) + pulse * 0.14f,
                markerEnergy = Mathf.Max(sustain * 0.42f, pulse * 0.18f),
                markerPhase = state.phase >= 0f
                    ? Mathf.Repeat(state.phase * 0.35f + timeSeconds * (0.04f + state.visualSpec.motionSpeed * 0.08f), 1f)
                    : -1f,
                markerScale = state.visualSpec.markerScale * (0.52f + sustain * 0.42f),
                normalOffset = Mathf.Sin(timeSeconds * (0.8f + state.visualSpec.motionSpeed * 1.2f)) * renderedHeight * 0.08f *
                    (0.3f + state.visualSpec.motionAmplitude * 0.7f),
                haloBreath = 1f + Mathf.Sin(timeSeconds * (0.9f + state.visualSpec.motionSpeed * 0.6f)) * 0.12f * state.visualSpec.motionAmplitude
            };
        }

        private static float GetVisualDuration(float chordDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return chordDuration + 0.14f + sound.releaseBias * 0.78f + sound.reverbBias * 0.22f;
        }
    }
}
