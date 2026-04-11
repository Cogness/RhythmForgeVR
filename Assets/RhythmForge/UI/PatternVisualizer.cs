using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmForge.Core.Data;

namespace RhythmForge.UI
{
    /// <summary>
    /// Renders a pattern's normalized points as a 3D LineRenderer at the instance's position.
    /// Supports selection highlight and playback pulse.
    /// </summary>
    public class PatternVisualizer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private float _renderScale = 0.15f;
        [SerializeField] private float _lineWidth = 0.003f;
        [SerializeField] private float _selectedLineWidth = 0.005f;

        private LineRenderer _lineRenderer;
        private string _instanceId;
        private PatternType _type;
        private Color _baseColor;
        private bool _isSelected;
        private float _currentPulse;

        public string InstanceId => _instanceId;

        public void Initialize(PatternDefinition pattern, PatternInstance instance, Material material)
        {
            _instanceId = instance.id;
            _type = pattern.type;
            _baseColor = pattern.color;

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                _lineRenderer.useWorldSpace = false;
                _lineRenderer.alignment = LineAlignment.View;
                _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _lineRenderer.receiveShadows = false;
            }

            _lineRenderer.material = material ? material : new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.material.color = _baseColor;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.loop = pattern.type == PatternType.RhythmLoop;

            // Set points from normalized coordinates
            RenderPoints(pattern.points);

            // Position the instance in the scene
            transform.position = instance.position;
        }

        private void RenderPoints(List<Vector2> normalizedPoints)
        {
            if (normalizedPoints == null || normalizedPoints.Count == 0) return;

            _lineRenderer.positionCount = normalizedPoints.Count;

            // Center the points around origin
            Vector2 center = Vector2.zero;
            foreach (var p in normalizedPoints) center += p;
            center /= normalizedPoints.Count;

            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 p = normalizedPoints[i] - center;
                _lineRenderer.SetPosition(i, new Vector3(p.x * _renderScale, p.y * _renderScale, 0f));
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateAppearance();
        }

        public void SetPulse(float pulse)
        {
            _currentPulse = pulse;
            UpdateAppearance();
        }

        public void SetMuted(bool muted)
        {
            if (_lineRenderer != null)
            {
                Color c = _baseColor;
                c.a = muted ? 0.25f : 1f;
                _lineRenderer.material.color = c;
            }
        }

        public void UpdatePosition(Vector3 worldPos)
        {
            transform.position = worldPos;
        }

        private void UpdateAppearance()
        {
            if (_lineRenderer == null) return;

            float width = _isSelected ? _selectedLineWidth : _lineWidth;
            width += _currentPulse * 0.003f;
            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;

            Color c = _baseColor;
            float brightnessBoost = _isSelected ? 0.3f : _currentPulse * 0.4f;
            c = new Color(
                Mathf.Clamp01(c.r + brightnessBoost),
                Mathf.Clamp01(c.g + brightnessBoost),
                Mathf.Clamp01(c.b + brightnessBoost),
                1f
            );
            _lineRenderer.material.color = c;
        }
    }
}
