using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class HarmonyPadBehavior : IPatternBehavior
    {
        public PatternType Type => PatternType.HarmonyPad;
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

            result = Apply3DHarmonyModifications(result, shapeProfile);

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
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.HarmonyPad).Evaluate(PatternType.HarmonyPad, shapeProfile);
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

            context.audioDispatcher?.PlayChord(
                context.preset,
                context.pattern.derivedSequence.chord,
                0.38f,
                duration,
                context.instance.pan,
                context.instance.brightness,
                context.instance.depth,
                context.preset.fxSend + context.group.busFx.reverb * 0.18f,
                context.sound,
                context.instance.id);

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

        private static HarmonyDerivationResult Apply3DHarmonyModifications(
            HarmonyDerivationResult result, ShapeProfile sp)
        {
            if (sp == null || result.derivedSequence?.chord == null || result.derivedSequence.chord.Count < 2)
                return result;

            var chord = result.derivedSequence.chord;
            int root = chord[0];

            if (sp.verticalityWorld > 0.6f)
            {
                // Wide voicing: push the top note up an octave if it isn't already spread
                int topIndex = chord.Count - 1;
                if (chord[topIndex] - root < 13)
                    chord[topIndex] += 12;
            }
            else if (sp.verticalityWorld < 0.25f)
            {
                // Close voicing: pull all notes within one octave of the root
                for (int i = 1; i < chord.Count; i++)
                {
                    while (chord[i] - root > 12)
                        chord[i] -= 12;
                    while (chord[i] - root < 0)
                        chord[i] += 12;
                }
            }

            return result;
        }
    }
}
