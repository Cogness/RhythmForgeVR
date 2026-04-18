using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmForge.Core.Analysis;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Captures MX Ink stylus strokes in 3D space, renders them with a LineRenderer,
    /// projects to 2D for analysis, and invokes the draft builder.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class StrokeCapture : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputMapper _input;
        [SerializeField] private DrawModeController _drawMode;
        [SerializeField] private Transform _userHead;

        [Header("Line Settings")]
        [SerializeField] private Material _strokeMaterial;
        [SerializeField] private float _maxLineWidth = 0.008f;
        [SerializeField] private float _minLineWidth = 0.0005f;
        [SerializeField] private float _minPointDistance = 0.002f;

        // 3D world-space points
        private List<Vector3> _worldPoints = new List<Vector3>();
        private List<float> _pressures = new List<float>();
        private List<Quaternion> _stylusRotations = new List<Quaternion>();
        private List<double> _timestamps = new List<double>();
        private double _strokeStartTime;
        private LineRenderer _currentLine;
        private GameObject _currentLineObj;
        private bool _isDrawing;
        private Vector3 _previousPoint;
        private GameObject _sidetoneObject;
        private AudioSource _sidetoneSource;
        private AudioClip _sidetoneClip;
        private float _middlePressureHoldSeconds;
        private bool _currentOrnamentFlag;
        private bool _backButtonHeldDuringStroke;

        private static readonly Color UprightStrokeColor = new Color(0.28f, 0.95f, 1f, 1f);
        private static readonly Color TiltedStrokeColor = new Color(1f, 0.72f, 0.24f, 1f);
        private const float OrnamentPressureThreshold = 0.3f;
        private const float OrnamentReleaseThreshold = 0.15f;
        private const float OrnamentHoldSeconds = 0.12f;
        private const float AccentMaxDurationSeconds = 0.45f;
        private const float AccentMaxLengthMeters = 0.3f;

        // Pending draft
        public DraftResult PendingDraft { get; private set; }
        public bool HasPendingDraft => PendingDraft != null && PendingDraft.success;

        // Events
        public event Action<DraftResult> OnDraftCreated;
        public event Action OnDraftDiscarded;
        public event Action OnStrokeStarted;

        // External references
        private SessionStore _store;
        private RhythmForgeEventBus _eventBus;
        private StylusUIPointer _uiPointer;
        private IInputProvider _inputProvider;
        public RhythmForgeEventBus EventBus => _eventBus;

        /// <summary>Called by RhythmForgeBootstrapper to inject component references.</summary>
        public void Configure(IInputProvider input, DrawModeController drawMode,
            Transform userHead, Material strokeMaterial = null,
            StylusUIPointer uiPointer = null)
        {
            _inputProvider = input;
            _input = input as InputMapper ?? _input;
            _drawMode = drawMode;
            _userHead = userHead;
            _uiPointer = uiPointer;
            if (strokeMaterial != null) _strokeMaterial = strokeMaterial;
        }

        public void Initialize(SessionStore store)
        {
            _store = store;
            _eventBus = store != null ? store.EventBus : null;
        }

        private void Update()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            if (_store == null || input == null) return;

            // Don't allow new drawing while there's a pending draft
            if (HasPendingDraft)
            {
                // If a UI button was clicked this frame (Save/Discard in CommitCardPanel)
                // the button's onClick handles it — don't also confirm via pen logic.
                if (!input.FrontButtonConsumed)
                {
                    if (input.FrontButtonDown)
                        ConfirmDraft(false);
                    else if (input.BackButtonDown || input.BackDoubleTap)
                        DiscardPending();
                }
                return;
            }

            float pressure = input.DrawPressure;

            // Suppress drawing when interacting with UI.
            if (_uiPointer != null && _uiPointer.IsHoveringUI) pressure = 0f;

            if (pressure > 0.05f)
            {
                if (!_isDrawing)
                {
                    StartStroke();
                    _isDrawing = true;
                }

                UpdateExpressionFlags(input);
                AddPoint(input.StylusPose.position, pressure, input.StylusPose.rotation);
                UpdateSidetone(input.StylusPose.position, pressure);
            }
            else if (_isDrawing)
            {
                _isDrawing = false;
                FinishStroke();
            }

            // Back button undoes last stroke
            if (input.BackButtonDown && !_isDrawing && !HasPendingDraft)
                ClearCurrentStroke();
        }

        private void StartStroke()
        {
            _worldPoints.Clear();
            _pressures.Clear();
            _stylusRotations.Clear();
            _timestamps.Clear();
            _strokeStartTime = Time.unscaledTimeAsDouble;
            _middlePressureHoldSeconds = 0f;
            _currentOrnamentFlag = false;
            _backButtonHeldDuringStroke = false;

            _currentLineObj = new GameObject("RhythmForge_Stroke");
            _currentLine = _currentLineObj.AddComponent<LineRenderer>();
            _currentLine.positionCount = 0;
            _currentLine.material = _strokeMaterial ? _strokeMaterial : new Material(Shader.Find("Sprites/Default"));
            _currentLine.material.color = _drawMode != null ? _drawMode.GetCurrentColor() : Color.cyan;
            _currentLine.loop = false;
            _currentLine.startWidth = _minLineWidth;
            _currentLine.endWidth = _minLineWidth;
            _currentLine.useWorldSpace = true;
            _currentLine.alignment = LineAlignment.View;
            _currentLine.widthCurve = new AnimationCurve();
            _currentLine.shadowCastingMode = ShadowCastingMode.Off;
            _currentLine.receiveShadows = false;
            _previousPoint = Vector3.positiveInfinity;
            StartSidetone();

            OnStrokeStarted?.Invoke();
            _eventBus?.Publish(new StrokeStartedEvent());
        }

        private void AddPoint(Vector3 worldPos, float pressure, Quaternion stylusRot)
        {
            if (Vector3.Distance(worldPos, _previousPoint) < _minPointDistance)
                return;

            _previousPoint = worldPos;
            _worldPoints.Add(worldPos);
            _pressures.Add(pressure);
            _stylusRotations.Add(stylusRot);
            _timestamps.Add(Time.unscaledTimeAsDouble - _strokeStartTime);

            _currentLine.positionCount = _worldPoints.Count;
            _currentLine.SetPosition(_worldPoints.Count - 1, worldPos);

            var curve = new AnimationCurve();
            var colorKeys = new GradientColorKey[_worldPoints.Count];
            var alphaKeys = new GradientAlphaKey[_worldPoints.Count];
            for (int i = 0; i < _worldPoints.Count; i++)
            {
                float tilt01 = GetTilt01(_stylusRotations[i]);
                float widthScale = Mathf.Lerp(0.85f, 1.15f, tilt01);
                float w = Mathf.Max(_pressures[i] * _maxLineWidth * widthScale, _minLineWidth);
                float t = _worldPoints.Count > 1 ? i / (float)(_worldPoints.Count - 1) : 0f;
                curve.AddKey(t, w);
                colorKeys[i] = new GradientColorKey(Color.Lerp(UprightStrokeColor, TiltedStrokeColor, tilt01), t);
                alphaKeys[i] = new GradientAlphaKey(1f, t);
            }
            _currentLine.widthCurve = curve;
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            _currentLine.colorGradient = gradient;
        }

        private void FinishStroke()
        {
            if (_worldPoints.Count < 3)
            {
                ClearCurrentStroke();
                return;
            }

            // Calculate 3D stroke center for spawn position
            Vector3 center = Vector3.zero;
            foreach (var p in _worldPoints) center += p;
            center /= _worldPoints.Count;

            // Preserve the stroke's own plane instead of flattening onto a head-facing plane.
            var strokeFrame = BuildStrokeFrame(_worldPoints, center);

            // Build draft
            PatternType type = _drawMode != null ? _drawMode.GetDominantType() : PatternType.RhythmLoop;
            Vector3 bondStrength = _drawMode != null
                ? _drawMode.GetBondStrength()
                : new Vector3(1f, 0f, 0f);
            bool freeMode = _drawMode != null && _drawMode.IsFreeMode();

            // Phase G: StrokeCapture no longer projects to 2D itself. The raw
            // 3D sample stream + plane basis is handed to DraftBuilder, which
            // constructs a <see cref="StrokeCurve"/> (samples + projected 2D +
            // basis) inside BuildFromStroke. Melody derivers stay bit-identical
            // because the projection math is unchanged — it just relocated.
            bool accentFlag = _backButtonHeldDuringStroke &&
                GetStrokeDurationSeconds() <= AccentMaxDurationSeconds &&
                GetStrokeLengthMeters() <= AccentMaxLengthMeters;

            var richSamples = BuildStrokeSamples(accentFlag);
            Vector3 referenceUp = _userHead != null ? _userHead.up : Vector3.up;

            var draft = DraftBuilder.BuildFromStroke(
                type, rawPoints: null, center, strokeFrame.rotation, _store.State, _store,
                richSamples, referenceUp, bondStrength, freeMode,
                strokeRight: strokeFrame.right, strokeUp: strokeFrame.up);

            if (!draft.success)
            {
                Debug.Log($"[RhythmForge] Draft failed: {draft.error}");
                ClearCurrentStroke();
                return;
            }

            PendingDraft = draft;
            OnDraftCreated?.Invoke(draft);
            _eventBus?.Publish(new DraftCreatedEvent(draft));
        }

        private StrokeFrame BuildStrokeFrame(List<Vector3> worldPoints, Vector3 center)
        {
            const float epsilon = 0.000001f;

            float maxDistanceSq = 0f;
            Vector3 planeRight = Vector3.right;
            for (int i = 0; i < worldPoints.Count - 1; i++)
            {
                for (int j = i + 1; j < worldPoints.Count; j++)
                {
                    Vector3 delta = worldPoints[j] - worldPoints[i];
                    float distanceSq = delta.sqrMagnitude;
                    if (distanceSq > maxDistanceSq)
                    {
                        maxDistanceSq = distanceSq;
                        planeRight = delta;
                    }
                }
            }

            if (maxDistanceSq <= epsilon)
                return BuildFallbackStrokeFrame();

            planeRight.Normalize();

            float maxOffsetSq = 0f;
            Vector3 planeSecondary = Vector3.zero;
            foreach (var point in worldPoints)
            {
                Vector3 relative = point - center;
                Vector3 onAxis = Vector3.Dot(relative, planeRight) * planeRight;
                Vector3 offset = relative - onAxis;
                float offsetSq = offset.sqrMagnitude;
                if (offsetSq > maxOffsetSq)
                {
                    maxOffsetSq = offsetSq;
                    planeSecondary = offset;
                }
            }

            if (maxOffsetSq <= epsilon)
                return BuildFallbackStrokeFrame();

            Vector3 planeNormal = Vector3.Cross(planeRight, planeSecondary);
            if (planeNormal.sqrMagnitude <= epsilon)
                return BuildFallbackStrokeFrame();

            planeNormal.Normalize();

            Vector3 toHead = _userHead != null ? (_userHead.position - center) : Vector3.forward;
            if (toHead.sqrMagnitude > epsilon && Vector3.Dot(planeNormal, toHead.normalized) < 0f)
                planeNormal = -planeNormal;

            Vector3 planeUp = Vector3.Cross(planeNormal, planeRight);
            if (planeUp.sqrMagnitude <= epsilon)
                return BuildFallbackStrokeFrame();

            planeUp.Normalize();

            Vector3 referenceUp = _userHead != null ? _userHead.up : Vector3.up;
            if (Vector3.Dot(planeUp, referenceUp) < 0f)
            {
                planeUp = -planeUp;
                planeRight = -planeRight;
            }

            return new StrokeFrame
            {
                right = planeRight,
                up = planeUp,
                rotation = Quaternion.LookRotation(planeNormal, planeUp)
            };
        }

        private StrokeFrame BuildFallbackStrokeFrame()
        {
            Vector3 forward = _userHead != null ? _userHead.forward : Vector3.forward;
            Vector3 up = _userHead != null ? _userHead.up : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude <= 0.000001f)
                right = Vector3.right;
            else
                right.Normalize();

            up = Vector3.Cross(forward, right);
            if (up.sqrMagnitude <= 0.000001f)
                up = Vector3.up;
            else
                up.Normalize();

            forward = Vector3.Cross(right, up);
            if (forward.sqrMagnitude <= 0.000001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            return new StrokeFrame
            {
                right = right,
                up = up,
                rotation = Quaternion.LookRotation(forward, up)
            };
        }

        // Phase G: ProjectTo2D removed. The 2D projection is now computed by
        // StrokeCurve.FromSamples inside DraftBuilder.BuildFromStroke, using
        // the same dot-product math against the stroke-plane basis that lived
        // here before.

        private struct StrokeFrame
        {
            public Vector3 right;
            public Vector3 up;
            public Quaternion rotation;
        }

        private List<StrokeSample> BuildStrokeSamples(bool accentFlag)
        {
            int n = _worldPoints.Count;
            var samples = new List<StrokeSample>(n);
            for (int i = 0; i < n; i++)
            {
                samples.Add(new StrokeSample
                {
                    worldPos = _worldPoints[i],
                    pressure = i < _pressures.Count ? _pressures[i] : 0f,
                    stylusRot = i < _stylusRotations.Count ? _stylusRotations[i] : Quaternion.identity,
                    timestamp = i < _timestamps.Count ? _timestamps[i] : 0.0,
                    ornamentFlag = _currentOrnamentFlag,
                    accentFlag = accentFlag
                });
            }
            return samples;
        }

        public void ConfirmDraft(bool duplicate)
        {
            if (PendingDraft == null || !PendingDraft.success) return;

            _store.ReserveName(PendingDraft.type);
            var draft = PendingDraft;
            var instance = _store.CommitDraft(draft, duplicate);
            _eventBus?.Publish(new DraftCommittedEvent(draft, instance, duplicate));
            PendingDraft = null;
            ClearCurrentStroke();
        }

        public void DiscardPending()
        {
            PendingDraft = null;
            ClearCurrentStroke();
            OnDraftDiscarded?.Invoke();
            _eventBus?.Publish(new DraftDiscardedEvent());
        }

        private void ClearCurrentStroke()
        {
            StopSidetone();
            if (_currentLineObj != null)
            {
                Destroy(_currentLineObj);
                _currentLineObj = null;
                _currentLine = null;
            }
            _worldPoints.Clear();
            _pressures.Clear();
            _stylusRotations.Clear();
            _timestamps.Clear();
            _middlePressureHoldSeconds = 0f;
            _currentOrnamentFlag = false;
            _backButtonHeldDuringStroke = false;
        }

        private void StartSidetone()
        {
            StopSidetone();

            _sidetoneObject = new GameObject("PenSidetone");
            _sidetoneObject.transform.SetParent(transform, false);
            _sidetoneSource = _sidetoneObject.AddComponent<AudioSource>();
            _sidetoneSource.playOnAwake = false;
            _sidetoneSource.loop = true;
            _sidetoneSource.spatialBlend = 0f;
            _sidetoneSource.volume = 0f;

            if (_sidetoneClip == null)
                _sidetoneClip = BuildSidetoneClip();

            _sidetoneSource.clip = _sidetoneClip;
            _sidetoneSource.Play();
        }

        private void UpdateSidetone(Vector3 stylusPosition, float pressure)
        {
            if (_sidetoneSource == null)
                return;

            if (pressure <= 0.05f)
            {
                _sidetoneSource.volume = 0f;
                return;
            }

            _sidetoneSource.volume = Mathf.Clamp01((pressure - 0.05f) / 0.95f) * 0.18f;

            float referenceHeight = _userHead != null ? _userHead.position.y : 1.4f;
            float heightDelta = stylusPosition.y - referenceHeight;
            _sidetoneSource.pitch = Mathf.Clamp(1f + heightDelta * 0.4f, 0.65f, 1.5f);
        }

        private void StopSidetone()
        {
            if (_sidetoneSource != null)
            {
                _sidetoneSource.Stop();
                _sidetoneSource = null;
            }

            if (_sidetoneObject != null)
            {
                Destroy(_sidetoneObject);
                _sidetoneObject = null;
            }
        }

        private static AudioClip BuildSidetoneClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.2f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[sampleCount];
            const float frequency = 220f;

            for (int i = 0; i < sampleCount; i++)
            {
                float phase = (i / (float)sampleRate) * frequency;
                float frac = phase - Mathf.Floor(phase);
                data[i] = frac < 0.5f
                    ? Mathf.Lerp(-0.6f, 0.6f, frac / 0.5f)
                    : Mathf.Lerp(0.6f, -0.6f, (frac - 0.5f) / 0.5f);
            }

            var clip = AudioClip.Create("PenSidetone", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void UpdateExpressionFlags(IInputProvider input)
        {
            if (input == null)
                return;

            _backButtonHeldDuringStroke |= input.BackButton;

            float middlePressure = input.MiddlePressure;
            if (middlePressure >= OrnamentPressureThreshold)
            {
                _middlePressureHoldSeconds += Time.unscaledDeltaTime;
                if (_middlePressureHoldSeconds >= OrnamentHoldSeconds)
                    _currentOrnamentFlag = true;
            }
            else if (middlePressure < OrnamentReleaseThreshold)
            {
                _middlePressureHoldSeconds = 0f;
            }
        }

        private float GetStrokeDurationSeconds()
        {
            return _timestamps.Count > 0 ? (float)_timestamps[_timestamps.Count - 1] : 0f;
        }

        private float GetStrokeLengthMeters()
        {
            float length = 0f;
            for (int i = 1; i < _worldPoints.Count; i++)
                length += Vector3.Distance(_worldPoints[i - 1], _worldPoints[i]);
            return length;
        }

        private float GetTilt01(Quaternion rotation)
        {
            Vector3 referenceUp = _userHead != null ? _userHead.up : Vector3.up;
            Vector3 stylusUp = rotation * Vector3.up;
            if (stylusUp.sqrMagnitude <= 0.000001f)
                stylusUp = Vector3.up;

            float angle = Vector3.Angle(stylusUp, referenceUp.normalized);
            return Mathf.Clamp01(angle / 60f);
        }
    }
}
