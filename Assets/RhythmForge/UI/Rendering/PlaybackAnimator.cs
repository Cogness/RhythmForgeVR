using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Sequencer;

namespace RhythmForge.UI.Rendering
{
    internal sealed class PlaybackAnimator
    {
        private readonly LineRenderer _lineRenderer;
        private readonly PlaybackHaloRenderer _haloRenderer;
        private readonly PlaybackMarker _playbackMarker;
        private readonly float _lineWidth;
        private readonly float _selectedLineWidth;

        public PlaybackAnimator(
            LineRenderer lineRenderer,
            PlaybackHaloRenderer haloRenderer,
            PlaybackMarker playbackMarker,
            float lineWidth,
            float selectedLineWidth)
        {
            _lineRenderer = lineRenderer;
            _haloRenderer = haloRenderer;
            _playbackMarker = playbackMarker;
            _lineWidth = lineWidth;
            _selectedLineWidth = selectedLineWidth;
        }

        public void UpdateAppearance(
            PatternType type,
            bool isSelected,
            bool isMuted,
            Color baseColor,
            bool hasPlaybackState,
            PatternPlaybackVisualState playbackState,
            float renderedHeight)
        {
            if (_lineRenderer == null || _lineRenderer.sharedMaterial == null)
                return;

            var state = hasPlaybackState
                ? playbackState
                : PatternPlaybackVisualState.CreateInactive(type);
            var spec = state.visualSpec;

            float pulse = isMuted ? 0f : Mathf.Clamp01(state.pulse);
            float sustain = isMuted ? 0f : Mathf.Clamp01(state.sustainAmount);
            var animation = state.isShapeNative
                ? ComputeShapeAnimation(state, pulse, sustain, renderedHeight, Time.time)
                : PatternBehaviorRegistry.Get(type).ComputeAnimation(state, pulse, sustain, renderedHeight, Time.time);

            float width = isSelected ? _selectedLineWidth : _lineWidth;
            width += animation.lineEnergy * (0.0014f + spec.thickness * 0.0032f);
            width += animation.extraLineWidth;

            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;

            float brightnessBoost = (isSelected ? 0.28f : 0f) + animation.lineEnergy * (0.12f + spec.brightness * 0.2f);
            Color lineColor = new Color(
                Mathf.Clamp01(baseColor.r + brightnessBoost),
                Mathf.Clamp01(baseColor.g + brightnessBoost),
                Mathf.Clamp01(baseColor.b + brightnessBoost),
                isMuted ? 0.25f : Mathf.Lerp(0.74f, 1f, Mathf.Clamp01(0.25f + animation.lineEnergy + (isSelected ? 0.2f : 0f))));

            _lineRenderer.sharedMaterial.color = lineColor;
            _haloRenderer.Update(spec, isMuted, animation.haloEnergy, width, lineColor, animation.haloBreath);
            UpdateMarker(lineColor, animation.markerEnergy, animation.markerPhase, animation.markerScale, animation.normalOffset);
        }

        private static AnimationEnergies ComputeShapeAnimation(
            PatternPlaybackVisualState state,
            float pulse,
            float sustain,
            float renderedHeight,
            float timeSeconds)
        {
            float rhythm = Mathf.Clamp01(state.rhythmPulse);
            float melody = Mathf.Clamp01(state.melodyMotion);
            float harmony = Mathf.Clamp01(state.harmonySustain);

            return new AnimationEnergies
            {
                lineEnergy = Mathf.Clamp01(rhythm * 0.45f + melody * 0.35f + harmony * 0.2f + pulse * 0.15f),
                haloEnergy = Mathf.Clamp01(harmony * 0.75f + melody * 0.2f + sustain * 0.15f),
                markerEnergy = Mathf.Clamp01(rhythm * 0.55f + melody * 0.45f),
                markerPhase = state.phase >= 0f ? state.phase : 0f,
                markerScale = 0.95f + melody * 0.35f + rhythm * 0.2f,
                normalOffset = (melody - harmony) * Mathf.Max(0.01f, renderedHeight) * 0.08f,
                haloBreath = 0.82f + harmony * 0.35f + Mathf.Sin(timeSeconds * 2.3f) * 0.08f,
                extraLineWidth = harmony * 0.0014f
            };
        }

        private void UpdateMarker(Color lineColor, float markerEnergy, float markerPhase, float markerScale, float normalOffset)
        {
            if (_playbackMarker == null)
                return;

            _playbackMarker.SetTint(lineColor);
            _playbackMarker.ApplyState(markerPhase, markerEnergy, markerScale, normalOffset);
        }
    }
}
