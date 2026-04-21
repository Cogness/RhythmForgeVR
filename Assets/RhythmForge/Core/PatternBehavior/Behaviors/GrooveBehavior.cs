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
                    totalSteps = GuidedDefaults.Bars * AppStateFactory.BarSteps,
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
            var groove = context.appState?.composition?.groove;
            if (groove == null || context.recordTrigger == null)
                return;

            int gridStep = groove.quantizeGrid >= 16 ? 1 : 2;
            int stride = ResolveVisualStride(groove.density, gridStep);
            if (stride <= 0)
                return;

            bool isTriggerStep = context.localStep % stride == 0;
            bool isAnchor = context.localStep == 0 || context.localStep == AppStateFactory.BarSteps * 4;
            if (!isTriggerStep && !isAnchor)
                return;

            float[] accentCurve = groove.accentCurve != null && groove.accentCurve.Length >= 4
                ? groove.accentCurve
                : new[] { 1f, 0.7f, 0.85f, 0.7f };
            float accent = accentCurve[Mathf.Abs(context.localStep) % 4];
            float duration = 0.08f + accent * 0.1f + groove.swing * 0.06f;

            context.recordTrigger.Invoke(
                context.instance.id,
                context.scheduledTime,
                duration);
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

        private static int ResolveVisualStride(float density, int gridStep)
        {
            if (density < 0.66f)
                return Mathf.Max(2, gridStep * 4);
            if (density < 0.9f)
                return Mathf.Max(2, gridStep * 2);
            if (density > 1.2f)
                return 1;
            return Mathf.Max(1, gridStep);
        }
    }
}
