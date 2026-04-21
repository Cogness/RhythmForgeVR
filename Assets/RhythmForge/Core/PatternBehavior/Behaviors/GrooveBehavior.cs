using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class GrooveBehavior : IPatternBehavior
    {
        private static readonly MelodyBehavior MelodyDelegate = new MelodyBehavior();

        public PatternType Type => PatternType.Groove;
        public string DisplayName => "Groove";
        public bool PrefersClosedStroke => MelodyDelegate.PrefersClosedStroke;
        public string DraftNamePrefix => "Groove";

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            var grooveProfile = GrooveShapeMapper.Map(shapeProfile);
            string presetId = InstrumentGroups.Get(groupId).defaultPresetByType.GetDefault(PatternType.Groove);
            string densityWord = grooveProfile.density < 0.85f
                ? "sparse"
                : grooveProfile.density > 1.15f ? "busy" : "balanced";
            string gridWord = grooveProfile.quantizeGrid >= 16 ? "16th-grid" : "8th-grid";

            return new PatternDerivationResult
            {
                bars = GuidedDefaults.Bars,
                presetId = presetId,
                tags = new List<string>
                {
                    densityWord,
                    grooveProfile.syncopation > 0.22f ? "syncopated" : "steady",
                    gridWord
                },
                derivedSequence = new DerivedSequence
                {
                    kind = "groove",
                    totalSteps = 0,
                    grooveProfile = grooveProfile
                },
                summary = $"{densityWord} groove profile, {gridWord}, swing {Mathf.RoundToInt(grooveProfile.swing * 100f)}%.",
                details = "Path length sets melodic density, angularity adds syncopation, curvature variance adds swing, and vertical span reshapes the beat accents without changing melody pitch choices."
            };
        }

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            return GenreRegistry.GetActive().GetSoundMapping(PatternType.Groove).Evaluate(PatternType.Groove, shapeProfile);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            // Groove is a schedule-time modifier for Melody and does not emit audio directly.
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            // Groove owns no audio clips of its own; Melody warms the clips it needs after groove shaping.
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            return MelodyDelegate.AdjustVisualSpec(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return MelodyDelegate.ComputeAnimation(state, pulse, sustain, renderedHeight, timeSeconds);
        }
    }
}
