using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;

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
        [SerializeField] private float _minInteractionRadius = 0.05f;
        [SerializeField] private float _labelPadding = 0.035f;

        private LineRenderer _lineRenderer;
        private SphereCollider _interactionCollider;
        private string _instanceId;
        private PatternType _type;
        private Color _baseColor;
        private bool _isSelected;
        private float _currentPulse;
        private bool _isMuted;
        private ShapeParameterLabel _paramLabel;
        private float _renderedWidth;
        private float _renderedHeight;

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

            if (_paramLabel == null)
            {
                _paramLabel = gameObject.AddComponent<ShapeParameterLabel>();
                _paramLabel.Initialize(userHead);
            }

            if (_interactionCollider == null)
            {
                _interactionCollider = gameObject.GetComponent<SphereCollider>();
                if (_interactionCollider == null)
                    _interactionCollider = gameObject.AddComponent<SphereCollider>();
                _interactionCollider.isTrigger = true;
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
            RenderPoints(pattern.points, pattern.shapeProfile);
            transform.position = instance.position;
            transform.rotation = ResolveRenderRotation(pattern, instance, userHead);
            UpdateAppearance();

            if (_paramLabel != null)
            {
                var sp = pattern.shapeProfile;
                var snd = pattern.soundProfile ?? SoundProfileMapper.Derive(pattern.type, sp);
                _paramLabel.SetData(pattern.type, sp, snd);
            }
        }

        private void RenderPoints(List<Vector2> normalizedPoints, ShapeProfile shapeProfile)
        {
            if (normalizedPoints == null || normalizedPoints.Count == 0)
            {
                _lineRenderer.positionCount = 0;
                UpdateInteractionBounds(0f, 0f);
                return;
            }

            _lineRenderer.positionCount = normalizedPoints.Count;
            float renderScale = GetRenderScale(shapeProfile);

            // Center the points around origin
            Vector2 center = Vector2.zero;
            foreach (var p in normalizedPoints) center += p;
            center /= normalizedPoints.Count;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 p = normalizedPoints[i] - center;
                var position = new Vector3(p.x * renderScale, p.y * renderScale, 0f);
                _lineRenderer.SetPosition(i, position);
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            _renderedWidth = maxX - minX;
            _renderedHeight = maxY - minY;
            UpdateInteractionBounds(_renderedWidth, _renderedHeight);
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

        public void SetParameterLabelVisible(bool visible)
        {
            if (_paramLabel != null)
                _paramLabel.SetVisible(visible);
        }

        public void UpdateParameterData(PatternType type, ShapeProfile sp, SoundProfile snd)
        {
            if (_paramLabel != null)
                _paramLabel.SetData(type, sp, snd);
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

        private float GetRenderScale(ShapeProfile shapeProfile)
        {
            if (shapeProfile != null && shapeProfile.worldMaxDimension > 0.0001f)
                return shapeProfile.worldMaxDimension;

            return _renderScale;
        }

        private void UpdateInteractionBounds(float renderedWidth, float renderedHeight)
        {
            float maxDimension = Mathf.Max(renderedWidth, renderedHeight);
            float radius = Mathf.Max(_minInteractionRadius, maxDimension * 0.6f + 0.015f);

            if (_interactionCollider != null)
            {
                _interactionCollider.center = Vector3.zero;
                _interactionCollider.radius = radius;
            }

            if (_paramLabel != null)
            {
                float offsetY = -Mathf.Max(0.06f, renderedHeight * 0.5f + _labelPadding);
                _paramLabel.SetLocalOffset(new Vector3(0f, offsetY, 0f));
            }
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
