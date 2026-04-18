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
    public sealed class MelodyLineBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.MelodyLine;
        public string DisplayName => "Melody";
        public bool PrefersClosedStroke => false;
        public string DraftNamePrefix => "Melody";

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.MelodyLine).Evaluate(PatternType.MelodyLine, shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence?.notes == null) return;
            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                foreach (var note in context.pattern.derivedSequence.notes)
                    results.Add(VoiceSpecResolver.ResolveMelody(
                        context.preset,
                        context.sound,
                        note.midi,
                        note.durationSteps * context.stepDuration,
                        context.instance.brightness,
                        context.instance.reverbSend,
                        context.instance.delaySend,
                        note.glide));
            }
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

                using (PatternContextScope.ForPattern(context.appState, context.pattern))
                {
                    context.audioDispatcher?.PlayMelody(
                        context.preset,
                        note.midi,
                        note.velocity,
                        duration,
                        context.instance.gainTrim,
                        context.instance.brightness,
                        context.instance.reverbSend,
                        Mathf.Clamp01(context.instance.delaySend + context.group.busFx.delay * 0.1f),
                        context.sound,
                        note.glide,
                        context.instance.id);
                }

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime,
                    GetVisualDuration(duration, context.sound));
            }
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            return VisualGrammarProfiles.GetMelodyLine().Apply(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return VisualGrammarProfiles.GetMelodyLine().Animate(state, pulse, sustain, renderedHeight, timeSeconds);
        }

        private static float GetVisualDuration(float noteDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return noteDuration + 0.06f + sound.releaseBias * 0.42f + sound.body * 0.08f;
        }
    }
}
