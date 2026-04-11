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
        private bool _isMuted;

        public string InstanceId => _instanceId;

        public void Initialize(PatternDefinition pattern, PatternInstance instance, Material material, Transform userHead = null)
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                _lineRenderer.useWorldSpace = false;
                _lineRenderer.alignment = LineAlignment.View;
                _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _lineRenderer.receiveShadows = false;
            }

            RefreshGeometry(pattern, instance, material, userHead);
        }

        public void RefreshGeometry(PatternDefinition pattern, PatternInstance instance, Material material = null, Transform userHead = null)
        {
            _instanceId = instance.id;
            _type = pattern.type;
            _baseColor = pattern.color;

            if (material != null || _lineRenderer.material == null)
                _lineRenderer.material = material ? material : new Material(Shader.Find("Sprites/Default"));

            _lineRenderer.loop = pattern.type == PatternType.RhythmLoop;
            RenderPoints(pattern.points);
            transform.position = instance.position;
            transform.rotation = ResolveRenderRotation(pattern, instance, userHead);
            UpdateAppearance();
        }

        private void RenderPoints(List<Vector2> normalizedPoints)
        {
            if (normalizedPoints == null || normalizedPoints.Count == 0)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

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
            _isMuted = muted;
            UpdateAppearance();
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
                _isMuted ? 0.25f : 1f
            );
            _lineRenderer.material.color = c;
        }

        private Quaternion ResolveRenderRotation(PatternDefinition pattern, PatternInstance instance, Transform userHead)
        {
            if (pattern != null && pattern.hasRenderRotation && IsValidRotation(pattern.renderRotation))
                return NormalizeQuaternion(pattern.renderRotation);

            if (userHead != null)
            {
                Vector3 toUser = userHead.position - instance.position;
                if (toUser.sqrMagnitude > 0.0001f)
                {
                    Vector3 forward = toUser.normalized;
                    Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                        ? Vector3.forward
                        : Vector3.up;
                    return Quaternion.LookRotation(forward, up);
                }
            }

            return Quaternion.identity;
        }

        private static bool IsValidRotation(Quaternion rotation)
        {
            if (float.IsNaN(rotation.x) || float.IsNaN(rotation.y) ||
                float.IsNaN(rotation.z) || float.IsNaN(rotation.w))
                return false;

            return rotation.x * rotation.x +
                   rotation.y * rotation.y +
                   rotation.z * rotation.z +
                   rotation.w * rotation.w > 0.0001f;
        }

        private static Quaternion NormalizeQuaternion(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);

            if (magnitude <= 0.0001f)
                return Quaternion.identity;

            return new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
        }
    }
}
