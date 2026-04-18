using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
        private RhythmForge.Core.Analysis.StrokeKinematics _kinematics = new RhythmForge.Core.Analysis.StrokeKinematics();
        private float _startTime;
        private LineRenderer _currentLine;
        private GameObject _currentLineObj;
        private bool _isDrawing;
        private Vector3 _previousPoint;

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

        // SW-1: Pen sidetone
        private PenSidetoneSource _sidetone;

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
            _sidetone = gameObject.AddComponent<PenSidetoneSource>(); _sidetone.Initialize();
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

            // Suppress drawing while back button held (panel dragging) or hovering UI
            if (input.BackButton) pressure = 0f;
            if (_uiPointer != null && _uiPointer.IsHoveringUI) pressure = 0f;

            if (pressure > 0.05f)
            {
                if (!_isDrawing)
                {
                    StartStroke();
                    _isDrawing = true;
                }
                AddPoint(input.StylusPose.position, pressure, input.StylusPose.rotation);
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
            _kinematics = new RhythmForge.Core.Analysis.StrokeKinematics();
            _startTime = Time.time;

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

            OnStrokeStarted?.Invoke();
            _eventBus?.Publish(new StrokeStartedEvent());
            _sidetone?.StartDraw(_drawMode != null ? _drawMode.CurrentMode : PatternType.RhythmLoop);
        }

        private void AddPoint(Vector3 worldPos, float pressure, Quaternion rotation)
        {
            if (Vector3.Distance(worldPos, _previousPoint) < _minPointDistance)
                return;

            _previousPoint = worldPos;
            _worldPoints.Add(worldPos);
            _pressures.Add(pressure);
            _kinematics.AddPoint(worldPos, pressure, 0f, rotation, Time.time - _startTime);

            _currentLine.positionCount = _worldPoints.Count;
            _currentLine.SetPosition(_worldPoints.Count - 1, worldPos);

            // Update width curve
            float width = Mathf.Max(pressure * _maxLineWidth, _minLineWidth);
            var curve = new AnimationCurve();
            for (int i = 0; i < _worldPoints.Count; i++)
            {
                float w = Mathf.Max(_pressures[i] * _maxLineWidth, _minLineWidth);
                float t = _worldPoints.Count > 1 ? i / (float)(_worldPoints.Count - 1) : 0f;
                curve.AddKey(t, w);
            }
            _currentLine.widthCurve = curve;

            // SW-1: Update sidetone volume from pressure
            _sidetone?.UpdatePressure(pressure);

            // SW-2: Tilt feedback: blend base color toward amber as the pen tilts from vertical
            Color baseColor = _drawMode != null ? _drawMode.GetCurrentColor() : Color.cyan;
            Color tiltColor = new Color(1f, 0.55f, 0.1f); // amber

            // Compute angle between stylus forward and world up — 0 = pointing up, 90 = horizontal
            Vector3 stylusForward = rotation * Vector3.forward;
            float tiltAngle = Vector3.Angle(stylusForward, Vector3.up);  // 0–180 degrees
            // Normalize: 0 at 0°/180° (aligned with up), 1 at 90° (horizontal / most expressive)
            float tiltT = Mathf.Sin(tiltAngle * Mathf.Deg2Rad);  // peaks at 90°, stays in 0–1

            Color blendedColor = Color.Lerp(baseColor, tiltColor, tiltT * 0.6f);
            _currentLine.material.color = blendedColor;
            _currentLine.startColor = blendedColor;
            _currentLine.endColor = blendedColor;
        }

        private void FinishStroke()
        {
            _sidetone?.StopDraw();
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
            List<Vector2> projected = ProjectTo2D(_worldPoints, center, strokeFrame.right, strokeFrame.up);

            // Inform kinematics of the stroke plane normal for tilt projection (Phase 1)
            Vector3 planeNormal = strokeFrame.rotation * Vector3.forward;
            _kinematics.SetPlaneNormal(planeNormal);

            // Phase 3 — 3D stroke feature extraction
            Compute3DStrokeFeatures(center, planeNormal);

            // Build draft
            PatternType type = _drawMode != null ? _drawMode.CurrentMode : PatternType.RhythmLoop;
            var draft = DraftBuilder.BuildFromStroke(
                type, projected, center, strokeFrame.rotation, _store.State, _store, _kinematics);

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

        private void Compute3DStrokeFeatures(Vector3 center, Vector3 planeNormal)
        {
            if (_worldPoints.Count < 2) return;

            // ── Planarity ─────────────────────────────────────────────────────────
            // Measure how much the stroke escapes its best-fit plane.
            // planarity = 1 means perfectly flat, 0 means highly volumetric.
            float sumSqOut = 0f;
            float maxSqOut = 0f;
            foreach (var pt in _worldPoints)
            {
                float d = Vector3.Dot(pt - center, planeNormal);
                float sq = d * d;
                sumSqOut += sq;
                if (sq > maxSqOut) maxSqOut = sq;
            }
            float meanSqOut = sumSqOut / _worldPoints.Count;
            _kinematics.planarity = maxSqOut > 0.000001f
                ? Mathf.Clamp01(1f - Mathf.Sqrt(meanSqOut / maxSqOut))
                : 1f;

            // ── ThrustAxis ────────────────────────────────────────────────────────
            // How much of the stroke's overall displacement is toward/away from the user.
            Vector3 strokeSpan = _worldPoints[_worldPoints.Count - 1] - _worldPoints[0];
            float spanMag = strokeSpan.magnitude;
            if (spanMag > 0.001f && _userHead != null)
            {
                Vector3 headFwd = _userHead.forward;
                _kinematics.thrustAxis = Mathf.Abs(Vector3.Dot(strokeSpan / spanMag, headFwd));
            }
            else
            {
                _kinematics.thrustAxis = 0f;
            }

            // ── VerticalityWorld ─────────────────────────────────────────────────
            // How aligned the stroke's principal axis is with world up.
            _kinematics.verticalityWorld = spanMag > 0.001f
                ? Mathf.Abs(Vector3.Dot(strokeSpan / spanMag, Vector3.up))
                : 0f;
        }

        private List<Vector2> ProjectTo2D(List<Vector3> worldPoints, Vector3 center, Vector3 right, Vector3 up)
        {
            var result = new List<Vector2>(worldPoints.Count);
            foreach (var p in worldPoints)
            {
                Vector3 relative = p - center;
                float x = Vector3.Dot(relative, right);
                float y = Vector3.Dot(relative, up);
                result.Add(new Vector2(x, y));
            }
            return result;
        }

        private struct StrokeFrame
        {
            public Vector3 right;
            public Vector3 up;
            public Quaternion rotation;
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
            _sidetone?.StopDraw();
            if (_currentLineObj != null)
            {
                Destroy(_currentLineObj);
                _currentLineObj = null;
                _currentLine = null;
            }
            _worldPoints.Clear();
            _pressures.Clear();
        }
    }
}
