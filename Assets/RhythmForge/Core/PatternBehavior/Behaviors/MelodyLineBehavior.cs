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

            result = Apply3DMelodyModifications(result, shapeProfile);

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
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.MelodyLine).Evaluate(PatternType.MelodyLine, shapeProfile);
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
                    note.glide,
                    context.instance.id);

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

        private static MelodyDerivationResult Apply3DMelodyModifications(
            MelodyDerivationResult result, ShapeProfile sp)
        {
            if (sp == null || result.derivedSequence?.notes == null || result.derivedSequence.notes.Count == 0)
                return result;

            var notes = result.derivedSequence.notes;

            if (sp.verticalityWorld > 0.6f)
            {
                // Vertical stroke → octave leaps on every other note
                for (int i = 1; i < notes.Count; i += 2)
                    notes[i].midi += 12;
            }
            else if (sp.verticalityWorld < 0.25f && notes.Count > 1)
            {
                // Horizontal stroke → constrain to stepwise motion (max ±2 semitones between notes)
                for (int i = 1; i < notes.Count; i++)
                {
                    int delta = notes[i].midi - notes[i - 1].midi;
                    if (delta > 2)  notes[i].midi = notes[i - 1].midi + 2;
                    if (delta < -2) notes[i].midi = notes[i - 1].midi - 2;
                }
            }

            return result;
        }
    }
}
