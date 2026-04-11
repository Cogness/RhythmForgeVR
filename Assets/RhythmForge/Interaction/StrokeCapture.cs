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

        /// <summary>Called by RhythmForgeBootstrapper to inject component references.</summary>
        public void Configure(InputMapper input, DrawModeController drawMode,
            Transform userHead, Material strokeMaterial = null,
            StylusUIPointer uiPointer = null)
        {
            _input = input;
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
            if (_store == null || _input == null) return;

            // Don't allow new drawing while there's a pending draft
            if (HasPendingDraft)
            {
                // Front button (single press) = confirm/save draft
                if (_input.FrontButtonDown)
                    ConfirmDraft(false);
                // Back button (single press) = discard draft
                else if (_input.BackButtonDown)
                    DiscardPending();
                // Back double-tap = also discard (legacy)
                else if (_input.BackDoubleTap)
                    DiscardPending();
                return;
            }

            float pressure = _input.TipPressure;

            if (pressure > 0.05f)
            {
                if (!_isDrawing)
                {
                    StartStroke();
                    _isDrawing = true;
                }
                AddPoint(_input.StylusPose.position, pressure);
            }
            else if (_isDrawing)
            {
                _isDrawing = false;
                FinishStroke();
            }

            // Front button cycles draw mode (suppressed when ray is on a UI button)
            if (_input.FrontButtonDown && (_uiPointer == null || !_uiPointer.IsHoveringUI))
                _drawMode?.CycleMode();

            // Back button undoes last stroke
            if (_input.BackButtonDown && !_isDrawing && !HasPendingDraft)
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

            // Project 3D points to 2D for analysis
            // Use a plane facing the user at the time of drawing
            List<Vector2> projected = ProjectTo2D(_worldPoints);

            // Calculate 3D stroke center for spawn position
            Vector3 center = Vector3.zero;
            foreach (var p in _worldPoints) center += p;
            center /= _worldPoints.Count;

            // Build draft
            PatternType type = _drawMode != null ? _drawMode.CurrentMode : PatternType.RhythmLoop;
            var draft = DraftBuilder.BuildFromStroke(type, projected, center, _store.State, _store);

            if (!draft.success)
            {
                Debug.Log($"[RhythmForge] Draft failed: {draft.error}");
                ClearCurrentStroke();
                return;
            }

            PendingDraft = draft;
            OnDraftCreated?.Invoke(draft);
        }

        /// <summary>
        /// Projects 3D world points onto a 2D plane facing the user.
        /// The plane's normal is the user's forward direction.
        /// X maps to local right, Y maps to local up.
        /// </summary>
        private List<Vector2> ProjectTo2D(List<Vector3> worldPoints)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in worldPoints) center += p;
            center /= worldPoints.Count;

            // Use user's head orientation to define the projection plane
            Vector3 forward = _userHead != null ? _userHead.forward : Vector3.forward;
            Vector3 up = _userHead != null ? _userHead.up : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;

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
