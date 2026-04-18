using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    /// <summary>
    /// Legacy single-facet behavior. Phase C onwards new strokes route through
    /// <see cref="MusicalShapeBehavior"/>; this class survives only to play back
    /// pre-v7 saves whose <c>PatternDefinition.musicalShape == null</c>.
    /// </summary>
    [Obsolete("Pre-v7 saves only; new shapes route through MusicalShapeBehavior.", false)]
    public sealed class RhythmLoopBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.RhythmLoop;
        public string DisplayName => "Rhythm";
        public bool PrefersClosedStroke => true;
        public string DraftNamePrefix => "Beat";

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
                        context.instance.reverbSend,
                        context.instance.delaySend));
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
                        context.instance.gainTrim,
                        context.instance.brightness,
                        Mathf.Clamp01(context.instance.reverbSend + context.group.busFx.reverb * 0.2f),
                        context.instance.delaySend,
                        context.sound,
                        context.instance.id);
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
