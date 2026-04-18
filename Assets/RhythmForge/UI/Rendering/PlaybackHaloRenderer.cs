using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Sequencer;

namespace RhythmForge.UI.Rendering
{
    internal sealed class PlaybackHaloRenderer
    {
        private readonly LineRenderer _haloRenderer;
        private readonly float _haloWidthScale;

        public PlaybackHaloRenderer(LineRenderer haloRenderer, float haloWidthScale)
        {
            _haloRenderer = haloRenderer;
            _haloWidthScale = haloWidthScale;
        }

        public void ApplyShape(Vector3[] renderedPoints, bool loop)
        {
            _haloRenderer.loop = loop;
            _haloRenderer.positionCount = renderedPoints.Length;
            for (int i = 0; i < renderedPoints.Length; i++)
                _haloRenderer.SetPosition(i, renderedPoints[i]);
        }

        public void Clear()
        {
            _haloRenderer.positionCount = 0;
            _haloRenderer.enabled = false;
        }

        public void Update(PlaybackVisualSpec spec, bool isMuted, float haloEnergy, float baseWidth, Color lineColor, float breath)
        {
            if (_haloRenderer == null || _haloRenderer.sharedMaterial == null || _haloRenderer.positionCount == 0 || isMuted || haloEnergy <= 0.01f)
            {
                if (_haloRenderer != null)
                    _haloRenderer.enabled = false;
                return;
            }

            _haloRenderer.enabled = true;

            float haloWidth = baseWidth * _haloWidthScale *
                Mathf.Lerp(0.75f, 1.55f, spec.haloStrength) *
                Mathf.Lerp(0.4f, 1f, haloEnergy) *
                breath;

            _haloRenderer.startWidth = haloWidth;
            _haloRenderer.endWidth = haloWidth;

            float haloBrightness = haloEnergy * (0.16f + spec.brightness * 0.2f);
            _haloRenderer.sharedMaterial.color = new Color(
                Mathf.Clamp01(lineColor.r + haloBrightness),
                Mathf.Clamp01(lineColor.g + haloBrightness),
                Mathf.Clamp01(lineColor.b + haloBrightness),
                Mathf.Lerp(0.08f, 0.34f, haloEnergy));
        }
    }
}
