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
            if (_lineRenderer == null || _lineRenderer.material == null)
                return;

            var state = hasPlaybackState
                ? playbackState
                : PatternPlaybackVisualState.CreateInactive(type);
            var spec = state.visualSpec;

            float pulse = isMuted ? 0f : Mathf.Clamp01(state.pulse);
            float sustain = isMuted ? 0f : Mathf.Clamp01(state.sustainAmount);
            var animation = PatternBehaviorRegistry.Get(type).ComputeAnimation(state, pulse, sustain, renderedHeight, Time.time);

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

            _lineRenderer.material.color = lineColor;
            _haloRenderer.Update(spec, isMuted, animation.haloEnergy, width, lineColor, animation.haloBreath);
            UpdateMarker(lineColor, animation.markerEnergy, animation.markerPhase, animation.markerScale, animation.normalOffset);
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
