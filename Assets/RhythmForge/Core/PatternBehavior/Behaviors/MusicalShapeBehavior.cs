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
    /// Phase C: single behavior that fans out all three facets of a
    /// <see cref="MusicalShape"/> (rhythm + melody + harmony) in one
    /// <see cref="Schedule"/> call, under a single
    /// scene-wide role. A facet with <c>bondStrength.{x|y|z} &lt;= 0</c> is silent
    /// (Solo-Rhythm / Solo-Melody / Solo-Harmony), so the same behavior
    /// services both Free mode (all three audible) and Solo modes.
    ///
    /// Reuses the existing per-facet audio dispatch paths
    /// (<see cref="IAudioDispatcher.PlayDrum"/> / <see cref="IAudioDispatcher.PlayMelody"/>
    /// / <see cref="IAudioDispatcher.PlayChord"/>) so the audible output per
    /// facet is bit-identical to the legacy per-type behaviors.
    /// </summary>
    public sealed class MusicalShapeBehavior : IPatternBehavior
    {
        // Phase C does NOT add a new PatternType enum value. The registry routes
        // to this behavior whenever <c>pattern.musicalShape != null</c>; the
        // pattern's <c>type</c> field still carries the dominant facet so all
        // the legacy per-type switches (TypeColors, VoiceSpecResolver,
        // visualizers) keep working with the dominant facet's identity.
        // The Type below is a placeholder — it's never used as a dictionary
        // key because this behavior isn't registered in the per-type dict.
        public PatternType Type => PatternType.RhythmLoop;
        public string DisplayName => "Shape";
        public bool PrefersClosedStroke => false;
        public string DraftNamePrefix => "Shape";

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            // Unused — MusicalShape sound profiles are populated per-facet by
            // UnifiedShapeDeriverBase.
            return new SoundProfile();
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            var shape = context.pattern.musicalShape;
            if (shape == null)
                return;

            if (shape.bondStrength.x > 0f)
                CollectRhythmSpecs(context, shape, results);

            if (shape.bondStrength.y > 0f)
                CollectMelodySpecs(context, shape, results);

            if (shape.bondStrength.z > 0f)
                CollectHarmonySpecs(context, shape, results);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            var shape = context.pattern.musicalShape;
            if (shape == null)
                return;

            if (shape.bondStrength.x > 0f)
                ScheduleRhythmFacet(context, shape);

            if (shape.bondStrength.y > 0f)
                ScheduleMelodyFacet(context, shape);

            if (shape.bondStrength.z > 0f)
                ScheduleHarmonyFacet(context, shape);
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            // Dominant facet drives the visualizer until Phase C.4 introduces
            // the three-layer ShapeVisualizer. Route through the dominant
            // facet's legacy behavior for visual continuity.
            return DominantBehavior(baseSpec.type).AdjustVisualSpec(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return DominantBehavior(state.visualSpec.type)
                .ComputeAnimation(state, pulse, sustain, renderedHeight, timeSeconds);
        }

        // --- per-facet scheduling ---

        private static void ScheduleRhythmFacet(PatternSchedulingContext ctx, MusicalShape shape)
        {
            var rhythm = shape.facets?.rhythm;
            if (rhythm?.events == null)
                return;

            var preset = ResolveFacetPreset(ctx, shape.rhythmPresetId) ?? ctx.preset;
            var sound  = shape.rhythmSoundProfile ?? ctx.sound;

            // Phase E: bondStrength.x (rhythm weight) tapers event velocity so a
            // rhythm-heavy shape is audibly louder on rhythm than a harmony-heavy
            // one. Legacy one-hot (weight==1) is bit-identical: 0.4 + 0.6*1 = 1.0.
            float rhythmScale = shape.bondStrength.x;

            foreach (var evt in rhythm.events)
            {
                if (evt.step != ctx.localStep)
                    continue;

                ctx.audioDispatcher?.PlayDrum(
                    preset,
                    evt.lane,
                    BondStrengthVelocity.ScaleRhythm(evt.velocity, rhythmScale),
                    ctx.instance.gainTrim,
                    ctx.instance.brightness,
                    Mathf.Clamp01(ctx.instance.reverbSend + ctx.group.busFx.reverb * 0.2f),
                    ctx.instance.delaySend,
                    sound,
                    ctx.instance.id);

                ctx.recordTrigger?.Invoke(
                    ctx.instance.id,
                    ctx.scheduledTime,
                    GetRhythmVisualDuration(evt.lane, sound));
            }
        }

        private static void ScheduleMelodyFacet(PatternSchedulingContext ctx, MusicalShape shape)
        {
            var melody = shape.facets?.melody;
            if (melody?.notes == null)
                return;

            var preset = ResolveFacetPreset(ctx, shape.melodyPresetId) ?? ctx.preset;
            var sound  = shape.melodySoundProfile ?? ctx.sound;

            // Phase E: see ScheduleRhythmFacet comment. bondStrength.y is the
            // melody weight; weight==1 (legacy one-hot Solo) passes velocity
            // through unchanged.
            float melodyScale = shape.bondStrength.y;

            foreach (var note in melody.notes)
            {
                if (note.step != ctx.localStep)
                    continue;

                float duration = note.durationSteps * ctx.stepDuration;

                ctx.audioDispatcher?.PlayMelody(
                    preset,
                    note.midi,
                    BondStrengthVelocity.ScaleMelody(note.velocity, melodyScale),
                    duration,
                    ctx.instance.gainTrim,
                    ctx.instance.brightness,
                    ctx.instance.reverbSend,
                    Mathf.Clamp01(ctx.instance.delaySend + ctx.group.busFx.delay * 0.1f),
                    sound,
                    note.glide,
                    ctx.instance.id);

                ctx.recordTrigger?.Invoke(
                    ctx.instance.id,
                    ctx.scheduledTime,
                    GetMelodyVisualDuration(duration, sound));
            }
        }

        private static void ScheduleHarmonyFacet(PatternSchedulingContext ctx, MusicalShape shape)
        {
            var harmony = shape.facets?.harmony;
            if (harmony?.events == null || harmony.events.Count == 0)
                return;

            var preset = ResolveFacetPreset(ctx, shape.harmonyPresetId) ?? ctx.preset;
            var sound  = shape.harmonySoundProfile ?? ctx.sound;

            // Phase E: harmony has no per-event velocity field — the chord
            // plays at a single fixed base of <c>HarmonyBaseVelocity</c>. We
            // scale that base by bondStrength.z so a harmony-heavy shape's pad
            // sits forward in the mix and a rhythm-heavy shape's pad recedes.
            // Legacy one-hot HarmonyPad saves (bondStrength==(0,0,1)) pass
            // through unchanged (0.4 + 0.6*1 = 1.0).
            const float HarmonyBaseVelocity = 0.38f;
            float harmonyVelocity = BondStrengthVelocity.ScaleHarmony(
                HarmonyBaseVelocity, shape.bondStrength.z);

            foreach (var evt in harmony.events)
            {
                if (evt == null || evt.step != ctx.localStep || evt.chord == null || evt.chord.Count == 0)
                    continue;

                float duration = Mathf.Max(1, evt.durationSteps) * ctx.stepDuration * 0.96f;

                ctx.audioDispatcher?.PlayChord(
                    preset,
                    evt.chord,
                    harmonyVelocity,
                    duration,
                    ctx.instance.gainTrim,
                    ctx.instance.brightness,
                    Mathf.Clamp01(ctx.instance.reverbSend + ctx.group.busFx.reverb * 0.18f),
                    ctx.instance.delaySend,
                    sound,
                    ctx.instance.id);

                ctx.recordTrigger?.Invoke(
                    ctx.instance.id,
                    ctx.scheduledTime,
                    GetHarmonyVisualDuration(duration, sound));
            }
        }

        // --- per-facet voice-spec collection (for sample warming) ---

        private static void CollectRhythmSpecs(PatternSchedulingContext ctx, MusicalShape shape, List<ResolvedVoiceSpec> results)
        {
            var rhythm = shape.facets?.rhythm;
            if (rhythm?.events == null)
                return;

            var preset = ResolveFacetPreset(ctx, shape.rhythmPresetId) ?? ctx.preset;
            var sound  = shape.rhythmSoundProfile ?? ctx.sound;

            foreach (var evt in rhythm.events)
                results.Add(VoiceSpecResolver.ResolveDrum(
                    evt.lane,
                    preset,
                    sound,
                    ctx.instance.brightness,
                    ctx.instance.reverbSend,
                    ctx.instance.delaySend));
        }

        private static void CollectMelodySpecs(PatternSchedulingContext ctx, MusicalShape shape, List<ResolvedVoiceSpec> results)
        {
            var melody = shape.facets?.melody;
            if (melody?.notes == null)
                return;

            var preset = ResolveFacetPreset(ctx, shape.melodyPresetId) ?? ctx.preset;
            var sound  = shape.melodySoundProfile ?? ctx.sound;

            foreach (var note in melody.notes)
                results.Add(VoiceSpecResolver.ResolveMelody(
                    preset, sound, note.midi,
                    note.durationSteps * ctx.stepDuration,
                    ctx.instance.brightness,
                    ctx.instance.reverbSend,
                    ctx.instance.delaySend,
                    note.glide));
        }

        private static void CollectHarmonySpecs(PatternSchedulingContext ctx, MusicalShape shape, List<ResolvedVoiceSpec> results)
        {
            var harmony = shape.facets?.harmony;
            if (harmony?.events == null)
                return;

            var preset = ResolveFacetPreset(ctx, shape.harmonyPresetId) ?? ctx.preset;
            var sound  = shape.harmonySoundProfile ?? ctx.sound;

            foreach (var evt in harmony.events)
            {
                if (evt?.chord == null)
                    continue;

                float duration = Mathf.Max(1, evt.durationSteps) * ctx.stepDuration * 0.96f;
                foreach (var midi in evt.chord)
                    results.Add(VoiceSpecResolver.ResolveHarmony(
                        preset,
                        sound,
                        midi,
                        duration,
                        ctx.instance.brightness,
                        ctx.instance.reverbSend,
                        ctx.instance.delaySend));
            }
        }

        // --- helpers ---

        private static InstrumentPreset ResolveFacetPreset(PatternSchedulingContext ctx, string presetId)
        {
            if (string.IsNullOrEmpty(presetId) || ctx.presetLookup == null)
                return null;
            return ctx.presetLookup(presetId);
        }

        private static IPatternBehavior DominantBehavior(PatternType type)
        {
            return PatternBehaviorRegistry.Get(type);
        }

        private static float GetRhythmVisualDuration(string lane, SoundProfile sound)
        {
            float baseDuration = lane == "kick"
                ? 0.22f
                : lane == "snare" ? 0.18f
                : lane == "perc" ? 0.14f : 0.1f;
            sound = sound ?? new SoundProfile();
            return baseDuration + sound.body * 0.14f + sound.releaseBias * 0.24f;
        }

        private static float GetMelodyVisualDuration(float noteDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return noteDuration + 0.06f + sound.releaseBias * 0.42f + sound.body * 0.08f;
        }

        private static float GetHarmonyVisualDuration(float chordDuration, SoundProfile sound)
        {
            sound = sound ?? new SoundProfile();
            return chordDuration + 0.14f + sound.releaseBias * 0.78f + sound.reverbBias * 0.22f;
        }
    }
}
