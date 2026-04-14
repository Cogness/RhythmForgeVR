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
