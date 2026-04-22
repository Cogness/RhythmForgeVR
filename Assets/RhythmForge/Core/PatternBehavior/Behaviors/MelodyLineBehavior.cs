using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class MelodyBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.Melody;
        public string DisplayName => "Melody";
        public bool PrefersClosedStroke => false;
        public string DraftNamePrefix => "Melody";

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var genre = GenreRegistry.GetActive();
            var result = genre.MelodyDeriver.Derive(points, metrics, keyName, shapeProfile, soundProfile, genre);
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
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.Melody).Evaluate(PatternType.Melody, shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            if (context.pattern.derivedSequence?.notes == null) return;

            var notes = GetScheduledNotes(context, totalSteps);
            using (PatternContextScope.ForPattern(context.appState, context.pattern))
            {
                foreach (var note in notes)
                    results.Add(VoiceSpecResolver.ResolveMelody(
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
            if (context.pattern.derivedSequence.notes == null)
                return;

            int totalSteps = context.pattern.derivedSequence.totalSteps > 0
                ? context.pattern.derivedSequence.totalSteps
                : AppStateFactory.BarSteps;
            var notes = GetScheduledNotes(context, totalSteps);

            foreach (var note in notes)
            {
                if (note.step != context.localStep)
                    continue;

                float duration = note.durationSteps * context.stepDuration;
                float startDelay = note.startDelaySteps * context.stepDuration;

                using (PatternContextScope.ForPattern(context.appState, context.pattern))
                {
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
                        note.glide,
                        startDelay);
                }

                context.recordTrigger?.Invoke(
                    context.instance.id,
                    context.scheduledTime + startDelay,
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

        private static List<ScheduledMelodyNote> GetScheduledNotes(PatternSchedulingContext context, int totalSteps)
        {
            GrooveProfile groove = context.appState != null && context.appState.guidedMode
                ? context.appState.composition?.groove
                : null;
            ChordProgression progression = context.appState != null && context.appState.guidedMode
                ? context.appState.composition?.progression
                : null;
            return MelodyGrooveApplier.Apply(context.pattern.derivedSequence.notes, groove, totalSteps, progression);
        }
    }
}
