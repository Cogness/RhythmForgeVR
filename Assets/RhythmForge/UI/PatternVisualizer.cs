using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmForge.Core.Data;
using RhythmForge.Core.Analysis;
using RhythmForge.Sequencer;

namespace RhythmForge.UI
{
    /// <summary>
    /// Renders a pattern's normalized points as a 3D LineRenderer at the instance's position.
    /// Supports selection highlight plus playback-reactive animation.
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

            _lineRenderer.loop = pattern.type == PatternType.RhythmLoop;
            _haloRenderer.loop = _lineRenderer.loop;

            RenderPoints(pattern.points, pattern.shapeProfile);
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

        private void RenderPoints(List<Vector2> normalizedPoints, ShapeProfile shapeProfile)
        {
            if (normalizedPoints == null || normalizedPoints.Count == 0)
            {
                _renderedPoints = new Vector3[0];
                _lineRenderer.positionCount = 0;
                _haloRenderer.positionCount = 0;
                _playbackMarker.SetTarget(null, _baseColor);
                UpdateInteractionBounds(0f, 0f);
                return;
            }

            _renderedPoints = new Vector3[normalizedPoints.Count];
            _lineRenderer.positionCount = normalizedPoints.Count;
            _haloRenderer.positionCount = normalizedPoints.Count;

            float renderScale = GetRenderScale(shapeProfile);

            Vector2 center = Vector2.zero;
            foreach (var p in normalizedPoints) center += p;
            center /= normalizedPoints.Count;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 point = normalizedPoints[i] - center;
                var position = new Vector3(point.x * renderScale, point.y * renderScale, 0f);
                _renderedPoints[i] = position;
                _lineRenderer.SetPosition(i, position);
                _haloRenderer.SetPosition(i, position);

                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            _renderedWidth = maxX - minX;
            _renderedHeight = maxY - minY;
            _playbackMarker.SetTarget(_lineRenderer, _baseColor);
            UpdateInteractionBounds(_renderedWidth, _renderedHeight);
        }

        private void UpdateAppearance()
        {
            if (_lineRenderer == null || _lineRenderer.material == null)
                return;

            var state = _hasPlaybackState
                ? _playbackState
                : PatternPlaybackVisualState.CreateInactive(_type);
            var spec = state.visualSpec;

            float pulse = _isMuted ? 0f : Mathf.Clamp01(state.pulse);
            float sustain = _isMuted ? 0f : Mathf.Clamp01(state.sustainAmount);

            float lineEnergy;
            float haloEnergy;
            float markerEnergy;
            float markerPhase;
            float markerScale;
            float normalOffset;

            switch (_type)
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
                    normalOffset = Mathf.Sin(Time.time * (2.2f + spec.motionSpeed)) * _renderedHeight * 0.02f * spec.secondaryStrength;
                    break;
                case PatternType.MelodyLine:
                    lineEnergy = pulse * 0.36f + sustain * 0.46f;
                    haloEnergy = pulse * 0.3f + sustain * 0.54f;
                    markerEnergy = Mathf.Max(pulse * 0.95f, sustain * 0.85f);
                    markerPhase = state.phase;
                    markerScale = spec.markerScale * (0.6f + pulse * 0.56f + sustain * 0.34f);
                    normalOffset = Mathf.Sin(Time.time * (1.2f + spec.motionSpeed * 2.4f)) * _renderedHeight * 0.12f * spec.motionAmplitude;
                    break;
                default:
                    lineEnergy = pulse * 0.16f + sustain * 0.5f;
                    haloEnergy = Mathf.Max(sustain * 0.92f, state.isActive ? 0.18f : 0f) + pulse * 0.14f;
                    markerEnergy = Mathf.Max(sustain * 0.42f, pulse * 0.18f);
                    markerPhase = state.phase >= 0f
                        ? Mathf.Repeat(state.phase * 0.35f + Time.time * (0.04f + spec.motionSpeed * 0.08f), 1f)
                        : -1f;
                    markerScale = spec.markerScale * (0.52f + sustain * 0.42f);
                    normalOffset = Mathf.Sin(Time.time * (0.8f + spec.motionSpeed * 1.2f)) * _renderedHeight * 0.08f *
                        (0.3f + spec.motionAmplitude * 0.7f);
                    break;
            }

            float width = _isSelected ? _selectedLineWidth : _lineWidth;
            width += lineEnergy * (0.0014f + spec.thickness * 0.0032f);
            if (_type == PatternType.HarmonyPad)
                width += haloEnergy * 0.0012f;

            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;

            float brightnessBoost = (_isSelected ? 0.28f : 0f) + lineEnergy * (0.12f + spec.brightness * 0.2f);
            Color lineColor = new Color(
                Mathf.Clamp01(_baseColor.r + brightnessBoost),
                Mathf.Clamp01(_baseColor.g + brightnessBoost),
                Mathf.Clamp01(_baseColor.b + brightnessBoost),
                _isMuted ? 0.25f : Mathf.Lerp(0.74f, 1f, Mathf.Clamp01(0.25f + lineEnergy + (_isSelected ? 0.2f : 0f))));

            _lineRenderer.material.color = lineColor;
            UpdateHalo(spec, haloEnergy, width, lineColor);
            UpdateMarker(spec, markerEnergy, markerPhase, markerScale, normalOffset, lineColor);
        }

        private void UpdateHalo(PlaybackVisualSpec spec, float haloEnergy, float baseWidth, Color lineColor)
        {
            if (_haloRenderer == null || _haloRenderer.material == null || _renderedPoints.Length == 0 || _isMuted || haloEnergy <= 0.01f)
            {
                if (_haloRenderer != null)
                    _haloRenderer.enabled = false;
                return;
            }

            _haloRenderer.enabled = true;

            float breath = 1f;
            if (_type == PatternType.HarmonyPad)
                breath += Mathf.Sin(Time.time * (0.9f + spec.motionSpeed * 0.6f)) * 0.12f * spec.motionAmplitude;
            else if (_type == PatternType.MelodyLine)
                breath += Mathf.Sin(Time.time * (1.1f + spec.motionSpeed * 1.4f)) * 0.06f * spec.secondaryStrength;

            float haloWidth = baseWidth * _haloWidthScale *
                Mathf.Lerp(0.75f, 1.55f, spec.haloStrength) *
                Mathf.Lerp(0.4f, 1f, haloEnergy) *
                breath;

            _haloRenderer.startWidth = haloWidth;
            _haloRenderer.endWidth = haloWidth;

            float haloBrightness = haloEnergy * (0.16f + spec.brightness * 0.2f);
            _haloRenderer.material.color = new Color(
                Mathf.Clamp01(lineColor.r + haloBrightness),
                Mathf.Clamp01(lineColor.g + haloBrightness),
                Mathf.Clamp01(lineColor.b + haloBrightness),
                Mathf.Lerp(0.08f, 0.34f, haloEnergy));
        }

        private void UpdateMarker(PlaybackVisualSpec spec, float markerEnergy, float markerPhase, float markerScale, float normalOffset, Color lineColor)
        {
            if (_playbackMarker == null)
                return;

            _playbackMarker.SetTint(lineColor);
            _playbackMarker.ApplyState(markerPhase, markerEnergy, markerScale, normalOffset);
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
