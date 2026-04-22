using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class HarmonyBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.Harmony;
        public string DisplayName => "Harmony";
        public bool PrefersClosedStroke => false;
        public string DraftNamePrefix => "Pad";

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var genre = GenreRegistry.GetActive();
            var result = genre.HarmonyDeriver.Derive(points, metrics, keyName, shapeProfile, soundProfile, genre);
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
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.Harmony).Evaluate(PatternType.Harmony, shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence == null)
                return;

            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                if (context.pattern.derivedSequence.chordEvents != null && context.pattern.derivedSequence.chordEvents.Count > 0)
                {
                    float duration = AppStateFactory.BarSteps * context.stepDuration * 0.96f;
                    for (int i = 0; i < context.pattern.derivedSequence.chordEvents.Count; i++)
                    {
                        var slot = context.pattern.derivedSequence.chordEvents[i];
                        if (slot?.voicing == null)
                            continue;

                        for (int n = 0; n < slot.voicing.Count; n++)
                        {
                            results.Add(VoiceSpecResolver.ResolveHarmony(
                                context.preset,
                                context.sound,
                                slot.voicing[n],
                                duration,
                                context.instance.brightness,
                                context.preset.fxSend));
                        }
                    }
                    return;
                }
            }
        }

        public void Schedule(PatternSchedulingContext context)
        {
            if (context.pattern.derivedSequence == null)
                return;

            if (context.pattern.derivedSequence.chordEvents != null && context.pattern.derivedSequence.chordEvents.Count > 0)
            {
                ScheduleProgression(context);
                return;
            }
        }

        private void ScheduleProgression(PatternSchedulingContext context)
        {
            if (context.localStep % AppStateFactory.BarSteps != 0)
                return;

            int barIndex = context.localStep / AppStateFactory.BarSteps;
            if (barIndex < 0 || barIndex >= context.pattern.derivedSequence.chordEvents.Count)
                return;

            var slot = context.pattern.derivedSequence.chordEvents[barIndex];
            if (slot?.voicing == null || slot.voicing.Count == 0)
                return;

            float duration = AppStateFactory.BarSteps * context.stepDuration * 0.96f;
            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                context.audioDispatcher?.PlayChord(
                    context.preset,
                    slot.voicing,
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
