using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class PercussionBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.Percussion;
        public string DisplayName => "Percussion";
        public bool PrefersClosedStroke => true;
        public string DraftNamePrefix => "Percussion";

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
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.Percussion).Evaluate(PatternType.Percussion, shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence?.events == null)
                return;

            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                foreach (var evt in context.pattern.derivedSequence.events)
                {
                    results.Add(VoiceSpecResolver.ResolveDrum(
                        evt.lane,
                        context.preset,
                        context.sound,
                        context.instance.brightness,
                        context.preset.fxSend));
                }
            }
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.pattern.derivedSequence?.events == null)
                return;

            float grooveSwing = ResolveGrooveSwing(context);
            foreach (var evt in context.pattern.derivedSequence.events)
            {
                if (!TryResolveStartDelay(evt, context.localStep, context.stepDuration, grooveSwing, out float startDelay))
                    continue;

                using (PatternContextScope.ForPattern(context.appState, context.pattern))
                {
                    context.audioDispatcher?.PlayDrum(
                        context.preset,
                        evt.lane,
                        evt.velocity,
                        context.instance.pan,
                        context.instance.brightness,
                        context.instance.depth,
                        context.preset.fxSend + context.group.busFx.reverb * 0.2f,
                        context.sound,
                        startDelay);
                }

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime + startDelay,
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

        private static float ResolveGrooveSwing(PatternSchedulingContext context)
        {
            if (context.appState == null || !context.appState.guidedMode)
                return 0f;

            return Mathf.Clamp01(context.appState.composition?.groove?.swing ?? 0f);
        }

        private static bool TryResolveStartDelay(
            RhythmEvent evt,
            int localStep,
            float stepDuration,
            float grooveSwing,
            out float startDelay)
        {
            float shiftSteps = ResolveShiftSteps(evt, grooveSwing);
            int scheduledStep = evt.step;

            while (shiftSteps < 0f && scheduledStep > 0)
            {
                scheduledStep--;
                shiftSteps += 1f;
            }

            while (shiftSteps >= 1f)
            {
                scheduledStep++;
                shiftSteps -= 1f;
            }

            if (scheduledStep != localStep)
            {
                startDelay = 0f;
                return false;
            }

            startDelay = Mathf.Clamp(shiftSteps, 0f, 0.99f) * stepDuration;
            return true;
        }

        private static float ResolveShiftSteps(RhythmEvent evt, float grooveSwing)
        {
            float shiftSteps = evt.microShift;
            if (grooveSwing <= 0.001f)
                return shiftSteps;

            int beatStep = Mathf.Abs(evt.step) % 4;
            if (beatStep == 2)
                return shiftSteps + grooveSwing;
            if (beatStep == 3)
                return shiftSteps - grooveSwing * 0.5f;

            return shiftSteps;
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
