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
    public sealed class HarmonyPadBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.HarmonyPad;
        public string DisplayName => "Harmony";
        public bool PrefersClosedStroke => false;
        public string DraftNamePrefix => "Pad";

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.HarmonyPad).Evaluate(PatternType.HarmonyPad, shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence?.chord == null) return;
            float duration = totalSteps * context.stepDuration * 0.96f;
            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                foreach (var midi in context.pattern.derivedSequence.chord)
                    results.Add(VoiceSpecResolver.ResolveHarmony(
                        context.preset,
                        context.sound,
                        midi,
                        duration,
                        context.instance.brightness,
                        context.preset.fxSend));
            }
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

            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
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
            }

            context.recordTrigger?.Invoke(
                context.instance.id,
                context.scheduledTime,
                GetVisualDuration(duration, context.sound));
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            return VisualGrammarProfiles.GetHarmonyPad().Apply(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return VisualGrammarProfiles.GetHarmonyPad().Animate(state, pulse, sustain, renderedHeight, timeSeconds);
        }

        private static float GetVisualDuration(float chordDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return chordDuration + 0.14f + sound.releaseBias * 0.78f + sound.reverbBias * 0.22f;
        }
    }
}
