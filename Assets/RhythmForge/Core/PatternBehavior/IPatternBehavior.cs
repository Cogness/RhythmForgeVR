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

        SoundProfile DeriveSoundProfile(ShapeProfile shapeProfile);

        void Schedule(PatternSchedulingContext context);

        /// <summary>
        /// Collect all ResolvedVoiceSpecs that would be played across all steps of this pattern.
        /// Used by the pre-warm path to kick off background renders one bar ahead.
        /// </summary>
        void CollectVoiceSpecs(PatternSchedulingContext context, int totalSteps, List<ResolvedVoiceSpec> results);

        PlaybackVisualSpec AdjustVisualSpec(PlaybackVisualSpec baseSpec, SoundProfile soundProfile);

        AnimationEnergies ComputeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds);
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
        // Phase C: MusicalShapeBehavior uses this to resolve per-facet
        // InstrumentPresets (facet presetIds live on MusicalShape). Legacy
        // per-type behaviors ignore it.
        public Func<string, InstrumentPreset> presetLookup;
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
