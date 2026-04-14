using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Sequencer;
using RhythmForge.UI.Rendering;

namespace RhythmForge.UI
{
    /// <summary>
    /// Coordinates the shape renderer, halo renderer, playback animator, and interaction affordances for a pattern instance.
    /// </summary>
    public class PatternVisualizer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private float _renderScale = 0.15f;
        [SerializeField] private float _lineWidth = 0.003f;
        [SerializeField] private float _selectedLineWidth = 0.005f;
        [SerializeField] private float _minInteractionRadius = 0.05f;
        [SerializeField] private float _labelPadding = 0.035f;
        [SerializeField] private float _haloWidthScale = 2.4f;

        private LineRenderer _lineRenderer;
        private LineRenderer _haloRenderer;
        private PlaybackMarker _playbackMarker;
        private SphereCollider _interactionCollider;
        private ShapeParameterLabel _paramLabel;

        private ShapeLineRenderer _shapeLineRenderer;
        private PlaybackHaloRenderer _playbackHaloRenderer;
        private PlaybackAnimator _playbackAnimator;

        private string _instanceId;
        private PatternType _type;
        private Color _baseColor;
        private bool _isSelected;
        private bool _isMuted;
        private float _renderedWidth;
        private float _renderedHeight;
        private Vector3[] _renderedPoints = new Vector3[0];
        private PatternPlaybackVisualState _playbackState;
        private bool _hasPlaybackState;

        public string InstanceId => _instanceId;

        public void Initialize(PatternDefinition pattern, PatternInstance instance, Material material, Transform userHead = null)
        {
            EnsureMainRenderer(material);
            EnsureHaloRenderer();
            EnsurePlaybackMarker();
            EnsureParameterLabel(userHead);
            EnsureInteractionCollider();
            EnsureRenderingHelpers();

            RefreshGeometry(pattern, instance, material, userHead);
        }

        public void RefreshGeometry(PatternDefinition pattern, PatternInstance instance, Material material = null, Transform userHead = null)
        {
            _instanceId = instance.id;
            _type = pattern.type;
            _baseColor = pattern.color;

            EnsureMainRenderer(material);
            EnsureHaloRenderer();
            EnsurePlaybackMarker();
            EnsureRenderingHelpers();

            _lineRenderer.loop = pattern.type == PatternType.RhythmLoop;
            var shape = _shapeLineRenderer.Render(pattern.points, GetRenderScale(pattern.shapeProfile));
            _renderedPoints = shape.points;
            _renderedWidth = shape.width;
            _renderedHeight = shape.height;

            if (_renderedPoints.Length == 0)
            {
                _playbackHaloRenderer.Clear();
                _playbackMarker.SetTarget(null, _baseColor);
                UpdateInteractionBounds(0f, 0f);
            }
            else
            {
                _playbackHaloRenderer.ApplyShape(_renderedPoints, _lineRenderer.loop);
                _playbackMarker.SetTarget(_lineRenderer, _baseColor);
                UpdateInteractionBounds(_renderedWidth, _renderedHeight);
            }

            transform.position = instance.position;
            transform.rotation = ResolveRenderRotation(pattern, instance, userHead);

            if (_paramLabel != null)
            {
                var sp = pattern.shapeProfile;
                var snd = pattern.soundProfile ?? SoundProfileMapper.Derive(pattern.type, sp);
                _paramLabel.SetData(pattern.type, sp, snd);
            }

            if (!_hasPlaybackState)
                _playbackState = PatternPlaybackVisualState.CreateInactive(pattern.type, pattern.soundProfile);

            UpdateAppearance();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateAppearance();
        }

        public void SetPulse(float pulse)
        {
            if (!_hasPlaybackState)
                _playbackState = PatternPlaybackVisualState.CreateInactive(_type);

            _playbackState.pulse = pulse;
            _hasPlaybackState = true;
            UpdateAppearance();
        }

        public void SetPlaybackState(PatternPlaybackVisualState state)
        {
            _playbackState = state;
            _hasPlaybackState = true;
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

        private void EnsureMainRenderer(Material material)
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
                _lineRenderer.useWorldSpace = false;
                _lineRenderer.alignment = LineAlignment.View;
                _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _lineRenderer.receiveShadows = false;
            }

            if (material != null)
            {
                if (_lineRenderer.material == null || _lineRenderer.material.shader != material.shader)
                    _lineRenderer.material = new Material(material);
            }
            else if (_lineRenderer.material == null)
            {
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void EnsureHaloRenderer()
        {
            if (_haloRenderer != null)
                return;

            var haloObject = new GameObject("PlaybackHalo");
            haloObject.transform.SetParent(transform, false);
            _haloRenderer = haloObject.AddComponent<LineRenderer>();
            _haloRenderer.useWorldSpace = false;
            _haloRenderer.alignment = LineAlignment.View;
            _haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _haloRenderer.receiveShadows = false;
            _haloRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _haloRenderer.enabled = false;
        }

        private void EnsurePlaybackMarker()
        {
            if (_playbackMarker != null)
                return;

            var markerObject = new GameObject("PlaybackMarker");
            markerObject.transform.SetParent(transform, false);
            _playbackMarker = markerObject.AddComponent<PlaybackMarker>();
        }

        private void EnsureParameterLabel(Transform userHead)
        {
            if (_paramLabel != null)
                return;

            _paramLabel = gameObject.AddComponent<ShapeParameterLabel>();
            _paramLabel.Initialize(userHead);
        }

        private void EnsureInteractionCollider()
        {
            if (_interactionCollider == null)
            {
                _interactionCollider = gameObject.GetComponent<SphereCollider>();
                if (_interactionCollider == null)
                    _interactionCollider = gameObject.AddComponent<SphereCollider>();
                _interactionCollider.isTrigger = true;
            }
        }

        private void EnsureRenderingHelpers()
        {
            if (_lineRenderer != null && _shapeLineRenderer == null)
                _shapeLineRenderer = new ShapeLineRenderer(_lineRenderer);

            if (_haloRenderer != null && _playbackHaloRenderer == null)
                _playbackHaloRenderer = new PlaybackHaloRenderer(_haloRenderer, _haloWidthScale);

            if (_lineRenderer != null && _playbackHaloRenderer != null && _playbackMarker != null && _playbackAnimator == null)
                _playbackAnimator = new PlaybackAnimator(
                    _lineRenderer,
                    _playbackHaloRenderer,
                    _playbackMarker,
                    _lineWidth,
                    _selectedLineWidth);
        }

        private void UpdateAppearance()
        {
            _playbackAnimator?.UpdateAppearance(
                _type,
                _isSelected,
                _isMuted,
                _baseColor,
                _hasPlaybackState,
                _playbackState,
                _renderedHeight);
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
