using UnityEngine;
using RhythmForge.Core.Data;
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

            float lineEnergy;
            float haloEnergy;
            float markerEnergy;
            float markerPhase;
            float markerScale;
            float normalOffset;

            switch (type)
            {
                case PatternType.RhythmLoop:
                    lineEnergy = pulse * 0.72f + sustain * 0.22f;
                    haloEnergy = pulse * 0.55f + sustain * 0.18f;
                    markerEnergy = state.phase >= 0f ? Mathf.Max(0.15f, pulse, sustain * 0.3f) : 0f;
                    markerPhase = state.phase >= 0f
                        ? Mathf.Repeat(
                            state.phase + Mathf.Sin(Time.time * (1.6f + spec.motionSpeed * 0.8f)) * spec.phaseJitter * 0.015f,
                            1f)
                        : -1f;
                    markerScale = spec.markerScale * (0.72f + pulse * 0.84f + sustain * 0.2f);
                    normalOffset = Mathf.Sin(Time.time * (2.2f + spec.motionSpeed)) * renderedHeight * 0.02f * spec.secondaryStrength;
                    break;
                case PatternType.MelodyLine:
                    lineEnergy = pulse * 0.36f + sustain * 0.46f;
                    haloEnergy = pulse * 0.3f + sustain * 0.54f;
                    markerEnergy = Mathf.Max(pulse * 0.95f, sustain * 0.85f);
                    markerPhase = state.phase;
                    markerScale = spec.markerScale * (0.6f + pulse * 0.56f + sustain * 0.34f);
                    normalOffset = Mathf.Sin(Time.time * (1.2f + spec.motionSpeed * 2.4f)) * renderedHeight * 0.12f * spec.motionAmplitude;
                    break;
                default:
                    lineEnergy = pulse * 0.16f + sustain * 0.5f;
                    haloEnergy = Mathf.Max(sustain * 0.92f, state.isActive ? 0.18f : 0f) + pulse * 0.14f;
                    markerEnergy = Mathf.Max(sustain * 0.42f, pulse * 0.18f);
                    markerPhase = state.phase >= 0f
                        ? Mathf.Repeat(state.phase * 0.35f + Time.time * (0.04f + spec.motionSpeed * 0.08f), 1f)
                        : -1f;
                    markerScale = spec.markerScale * (0.52f + sustain * 0.42f);
                    normalOffset = Mathf.Sin(Time.time * (0.8f + spec.motionSpeed * 1.2f)) * renderedHeight * 0.08f *
                        (0.3f + spec.motionAmplitude * 0.7f);
                    break;
            }

            float width = isSelected ? _selectedLineWidth : _lineWidth;
            width += lineEnergy * (0.0014f + spec.thickness * 0.0032f);
            if (type == PatternType.HarmonyPad)
                width += haloEnergy * 0.0012f;

            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;

            float brightnessBoost = (isSelected ? 0.28f : 0f) + lineEnergy * (0.12f + spec.brightness * 0.2f);
            Color lineColor = new Color(
                Mathf.Clamp01(baseColor.r + brightnessBoost),
                Mathf.Clamp01(baseColor.g + brightnessBoost),
                Mathf.Clamp01(baseColor.b + brightnessBoost),
                isMuted ? 0.25f : Mathf.Lerp(0.74f, 1f, Mathf.Clamp01(0.25f + lineEnergy + (isSelected ? 0.2f : 0f))));

            _lineRenderer.material.color = lineColor;
            _haloRenderer.Update(spec, type, isMuted, haloEnergy, width, lineColor);
            UpdateMarker(lineColor, markerEnergy, markerPhase, markerScale, normalOffset);
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
