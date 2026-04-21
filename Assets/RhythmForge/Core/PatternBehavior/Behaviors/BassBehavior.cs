using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Audio;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior.Behaviors
{
    public sealed class BassBehavior : IPatternBehavior
    {
        private static readonly MelodyBehavior MelodyDelegate = new MelodyBehavior();

        public PatternType Type => PatternType.Bass;
        public string DisplayName => "Bass";
        public bool PrefersClosedStroke => MelodyDelegate.PrefersClosedStroke;
        public string DraftNamePrefix => "Bass";

        public PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile)
        {
            return MelodyDelegate.Derive(points, metrics, keyName, groupId, shapeProfile, soundProfile);
        }

        public SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile)
        {
            return MelodyDelegate.DeriveSoundProfile(shapeProfile);
        }

        public void Schedule(PatternSchedulingContext context)
        {
            MelodyDelegate.Schedule(context);
        }

        public void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results)
        {
            MelodyDelegate.CollectVoiceSpecs(context, totalSteps, results);
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
