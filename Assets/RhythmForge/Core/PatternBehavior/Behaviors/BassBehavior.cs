using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class BassBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.Bass;
        public string DisplayName => "Bass";
        public bool PrefersClosedStroke => false;
        public string DraftNamePrefix => "Bass";

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var result = BassDeriver.Derive(points, metrics, keyName, groupId, shapeProfile, soundProfile);
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
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.Bass).Evaluate(PatternType.Bass, shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence?.notes == null)
                return;

            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                foreach (var note in context.pattern.derivedSequence.notes)
                    results.Add(VoiceSpecResolver.ResolveBass(
                        context.preset,
                        context.sound,
                        note.midi,
                        note.durationSteps * context.stepDuration,
                        context.instance.brightness,
                        context.preset.fxSend,
                        note.glide));
            }
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.pattern.derivedSequence?.notes == null)
                return;

            foreach (var note in context.pattern.derivedSequence.notes)
            {
                if (note.step != context.localStep)
                    continue;

                float duration = note.durationSteps * context.stepDuration;
                using (PatternContextScope.ForPattern(context.appState, context.pattern))
                {
                    context.audioDispatcher?.PlayBass(
                        context.preset,
                        note.midi,
                        note.velocity,
                        duration,
                        context.instance.pan,
                        context.instance.brightness,
                        context.instance.depth,
                        context.preset.fxSend + context.group.busFx.delay * 0.06f,
                        context.sound,
                        note.glide);
                }

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime,
                    GetVisualDuration(duration, context.sound));
            }
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            return VisualGrammarProfiles.GetBass().Apply(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return VisualGrammarProfiles.GetBass().Animate(state, pulse, sustain, renderedHeight, timeSeconds);
        }

        private static float GetVisualDuration(float noteDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return noteDuration + 0.08f + sound.body * 0.1f + sound.releaseBias * 0.36f;
        }
    }
}
