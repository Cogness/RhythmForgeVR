using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class RhythmLoopBehavior : IPatternBehavior
    {
        private static readonly PercussionBehavior Inner = new PercussionBehavior();

        public PatternType Type => PatternType.RhythmLoop;
        public string DisplayName => Inner.DisplayName;
        public bool PrefersClosedStroke => Inner.PrefersClosedStroke;
        public string DraftNamePrefix => Inner.DraftNamePrefix;

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            return Inner.Derive(points, metrics, keyName, groupId, shapeProfile, soundProfile);
        }

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            return Inner.DeriveSoundProfile(shapeProfile);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            Inner.CollectVoiceSpecs(context, totalSteps, results);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            Inner.Schedule(context);
        }

        public PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile)
        {
            return Inner.AdjustVisualSpec(baseSpec, soundProfile);
        }

        public AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            return Inner.ComputeAnimation(state, pulse, sustain, renderedHeight, timeSeconds);
        }
    }
}
