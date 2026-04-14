using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RhythmForge.Core.Data;
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
        private StylusUIPointer _uiPointer;
        private IInputProvider _inputProvider;

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
                AddPoint(input.StylusPose.position, pressure);
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
        }

        private void AddPoint(Vector3 worldPos, float pressure)
        {
            if (Vector3.Distance(worldPos, _previousPoint) < _minPointDistance)
                return;

            _previousPoint = worldPos;
            _worldPoints.Add(worldPos);
            _pressures.Add(pressure);

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
            List<Vector2> projected = ProjectTo2D(_worldPoints, center, strokeFrame.right, strokeFrame.up);

            // Build draft
            PatternType type = _drawMode != null ? _drawMode.CurrentMode : PatternType.RhythmLoop;
            var draft = DraftBuilder.BuildFromStroke(
                type, projected, center, strokeFrame.rotation, _store.State, _store);

            if (!draft.success)
            {
                Debug.Log($"[RhythmForge] Draft failed: {draft.error}");
                ClearCurrentStroke();
                return;
            }

            PendingDraft = draft;
            OnDraftCreated?.Invoke(draft);
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
            _store.CommitDraft(PendingDraft, duplicate);
            PendingDraft = null;
            ClearCurrentStroke();
        }

        public void DiscardPending()
        {
            PendingDraft = null;
            ClearCurrentStroke();
            OnDraftDiscarded?.Invoke();
        }

        private void ClearCurrentStroke()
        {
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
