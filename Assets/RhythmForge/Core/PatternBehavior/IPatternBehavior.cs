using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Audio;
using RhythmForge.Sequencer;

namespace RhythmForge.Core.PatternBehavior
{
    public interface IPatternBehavior
    {
        PatternType Type { get; }
        string DisplayName { get; }
        bool PrefersClosedStroke { get; }
        /// <summary>Prefix used when auto-naming drafts of this type (e.g. "Beat", "Melody", "Pad").</summary>
        string DraftNamePrefix { get; }

        PatternDerivationResult Derive(
            List<Vector2> points,
            StrokeMetrics metrics,
            string keyName,
            string groupId,
            ShapeProfile shapeProfile,
            SoundProfile soundProfile);

        SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile);

        void Schedule(PatternSchedulingContext context);

        PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile);

        AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds);
    }

    public struct PatternDerivationResult
    {
        public int bars;
        public string presetId;
        public List<string> tags;
        public DerivedSequence derivedSequence;
        public string summary;
        public string details;
    }

    public struct PatternSchedulingContext
    {
        public PatternDefinition pattern;
        public PatternInstance instance;
        public int localStep;
        public float stepDuration;
        public double scheduledTime;
        public SoundProfile sound;
        public InstrumentPreset preset;
        public InstrumentGroup group;
        public Transport transport;
        public AppState appState;
        public IAudioDispatcher audioDispatcher;
        public Action<string, double, float> recordTrigger;
    }

    public struct AnimationEnergies
    {
        public float lineEnergy;
        public float haloEnergy;
        public float markerEnergy;
        public float markerPhase;
        public float markerScale;
        public float normalOffset;
        public float haloBreath;
        /// <summary>
        /// Extra width added to the line on top of the energy-driven base.
        /// Set by each pattern type's animation profile (HarmonyPad uses a non-zero value).
        /// </summary>
        public float extraLineWidth;
    }
}
