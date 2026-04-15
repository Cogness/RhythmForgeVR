using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
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

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence?.events == null) return;
            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                foreach (var evt in context.pattern.derivedSequence.events)
                    results.Add(VoiceSpecResolver.ResolveDrum(
                        evt.lane,
                        context.preset,
                        context.sound,
                        context.instance.brightness,
                        context.preset.fxSend));
            }
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.pattern.derivedSequence.events == null)
                return;

            foreach (var evt in context.pattern.derivedSequence.events)
            {
                if (evt.step != context.localStep)
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
                        context.sound);
                }

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
    }
}
