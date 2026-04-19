using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;

namespace RhythmForge.Interaction
{
    [DefaultExecutionOrder(15)]
    public class ConductorGestureRecognizer : MonoBehaviour
    {
        private const float SwayAmplitudeThreshold = 0.25f;
        private const float SwayDurationSeconds = 1.0f;
        private const float LiftFadeWindowSeconds = 0.45f;
        private const float LiftFadeVerticalThreshold = 0.12f;
        private const float LiftFadeHorizontalThreshold = 0.18f;
        private const float CutOffVelocityThreshold = 1.5f;
        private const float RefractorySeconds = 0.60f;
        private const float TargetZoneMaxDistance = 1.5f;
        private const float MaxHistorySeconds = 1.25f;
        private const float MinVelocitySign = 0.02f;
        private const float TempoNudgeScale = 0.03f;
        private const int SwayDecayBars = 2;

        private const string AllZonesSentinel = "*";

        private struct GestureSample
        {
            public double time;
            public Vector3 worldPosition;
            public Vector3 localPosition;
            public Vector3 localVelocity;
            public bool leftGrip;
        }

        private struct DetectionResult
        {
            public ConductorGestureEvent gesture;
            public int swayDirection;
        }

        [SerializeField] private InputMapper _input;

        private readonly List<GestureSample> _samples = new List<GestureSample>();
        private readonly Dictionary<ConductorGestureKind, double> _refractoryUntilByKind = new Dictionary<ConductorGestureKind, double>();

        private IInputProvider _inputProvider;
        private StrokeCapture _strokeCapture;
        private SpatialZoneController _zoneController;
        private SessionStore _store;
        private RhythmForgeEventBus _eventBus;
        private Sequencer.Sequencer _sequencer;
        private bool _conductingModeOn;
        private bool _hasPreviousSample;
        private Vector3 _previousWorldPosition;
        private double _previousSampleTime;
        private int _currentAbsoluteBar = 1;
        private bool _swayActive;
        private float _swayBaselineTempo;
        private float _swayTargetTempo;
        private int _swayStartBar;
        private int _swayEndBar;

        public void Configure(IInputProvider input)
        {
            _inputProvider = input;
            _input = input as InputMapper ?? _input;
        }

        public void Initialize(
            SessionStore store,
            RhythmForgeEventBus eventBus,
            Sequencer.Sequencer sequencer,
            StrokeCapture strokeCapture,
            SpatialZoneController zoneController)
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<ConductingModeChangedEvent>(HandleConductingModeChanged);
            if (_sequencer != null)
                _sequencer.OnBarStart -= HandleBarStart;

            _store = store;
            _eventBus = eventBus;
            _sequencer = sequencer;
            _strokeCapture = strokeCapture;
            _zoneController = zoneController;

            if (_eventBus != null)
                _eventBus.Subscribe<ConductingModeChangedEvent>(HandleConductingModeChanged);
            if (_sequencer != null)
                _sequencer.OnBarStart += HandleBarStart;
        }

        internal void SetConductingMode(bool on)
        {
            _conductingModeOn = on;
            if (!on)
                ResetHistory();
        }

        internal ConductorGestureEvent? PushSyntheticSample(Vector3 worldPosition, bool leftGrip, bool isDrawing, double sampleTime)
        {
            var detection = EvaluateFrame(worldPosition, leftGrip, isDrawing, false, false, sampleTime);
            return detection?.gesture;
        }

        private void LateUpdate()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            if (input == null || _zoneController == null || _store == null)
                return;

            if (!input.IsStylusActive)
            {
                ResetHistory();
                return;
            }

            bool hasPendingDraft = _strokeCapture != null && _strokeCapture.HasPendingDraft;
            EvaluateFrame(
                input.StylusPose.position,
                input.LeftGrip,
                input.IsDrawing,
                hasPendingDraft,
                PanelDragCoordinator.IsDragging,
                Time.unscaledTimeAsDouble);
        }

        private DetectionResult? EvaluateFrame(
            Vector3 worldPosition,
            bool leftGrip,
            bool isDrawing,
            bool hasPendingDraft,
            bool isPanelDragging,
            double sampleTime)
        {
            if (!_conductingModeOn || isDrawing || hasPendingDraft || isPanelDragging)
            {
                ResetHistory();
                return null;
            }

            AppendSample(worldPosition, leftGrip, sampleTime);
            TrimHistory(sampleTime);

            if (TryDetectCutOff(sampleTime, out var cutOff))
                return DispatchGesture(cutOff);
            if (TryDetectLiftFade(ConductorGestureKind.LiftTendu, sampleTime, out var lift))
                return DispatchGesture(lift);
            if (TryDetectLiftFade(ConductorGestureKind.FadePlie, sampleTime, out var fade))
                return DispatchGesture(fade);
            if (TryDetectSway(sampleTime, out var sway))
                return DispatchGesture(sway);

            return null;
        }

        private DetectionResult? DispatchGesture(DetectionResult detection)
        {
            if (detection.gesture.kind == ConductorGestureKind.Sway)
                ApplySway(detection.gesture.magnitude, detection.swayDirection);
            else
                _zoneController?.ApplyConductorGesture(detection.gesture.targetZoneId, detection.gesture.kind, detection.gesture.magnitude);

            _eventBus?.Publish(new ConductorGestureFiredEvent(detection.gesture));
            _refractoryUntilByKind[detection.gesture.kind] = detection.gesture.dspTime + RefractorySeconds;
            return detection;
        }

        private void AppendSample(Vector3 worldPosition, bool leftGrip, double sampleTime)
        {
            Vector3 localPosition = _zoneController != null
                ? _zoneController.transform.InverseTransformPoint(worldPosition)
                : worldPosition;

            Vector3 localVelocity = Vector3.zero;
            if (_hasPreviousSample && sampleTime > _previousSampleTime)
            {
                Vector3 previousLocal = _zoneController != null
                    ? _zoneController.transform.InverseTransformPoint(_previousWorldPosition)
                    : _previousWorldPosition;
                localVelocity = (localPosition - previousLocal) / (float)(sampleTime - _previousSampleTime);
            }

            _samples.Add(new GestureSample
            {
                time = sampleTime,
                worldPosition = worldPosition,
                localPosition = localPosition,
                localVelocity = localVelocity,
                leftGrip = leftGrip
            });

            _previousWorldPosition = worldPosition;
            _previousSampleTime = sampleTime;
            _hasPreviousSample = true;
        }

        private void TrimHistory(double sampleTime)
        {
            double cutoff = sampleTime - MaxHistorySeconds;
            while (_samples.Count > 0 && _samples[0].time < cutoff)
                _samples.RemoveAt(0);
        }

        private void ResetHistory()
        {
            _samples.Clear();
            _hasPreviousSample = false;
            _previousSampleTime = 0d;
            _previousWorldPosition = Vector3.zero;
        }

        private bool TryDetectCutOff(double sampleTime, out DetectionResult detection)
        {
            detection = default;
            if (!IsReady(ConductorGestureKind.CutOff, sampleTime) || _samples.Count < 2)
                return false;

            float peakVelocity = 0f;
            int peakIndex = -1;
            int startIndex = -1;
            double cutoff = sampleTime - 0.20d;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].time < cutoff)
                    continue;

                if (startIndex < 0)
                    startIndex = i;

                float velocity = new Vector2(_samples[i].localVelocity.x, _samples[i].localVelocity.z).magnitude;
                if (velocity <= peakVelocity || !_samples[i].leftGrip)
                    continue;

                peakVelocity = velocity;
                peakIndex = i;
            }

            if (peakIndex < 0 || peakVelocity <= CutOffVelocityThreshold)
                return false;

            string targetZoneId = ResolveTargetZoneId(_samples[startIndex].worldPosition, _samples[peakIndex].leftGrip);
            if (string.IsNullOrEmpty(targetZoneId))
                return false;

            detection = new DetectionResult
            {
                gesture = new ConductorGestureEvent(
                    ConductorGestureKind.CutOff,
                    targetZoneId,
                    Mathf.Clamp01((peakVelocity - CutOffVelocityThreshold) / CutOffVelocityThreshold),
                    _samples[startIndex].worldPosition,
                    sampleTime),
                swayDirection = 0
            };
            return true;
        }

        private bool TryDetectLiftFade(ConductorGestureKind kind, double sampleTime, out DetectionResult detection)
        {
            detection = default;
            if (!IsReady(kind, sampleTime) || _samples.Count < 2)
                return false;

            int startIndex = FindWindowStartIndex(sampleTime - LiftFadeWindowSeconds);
            if (startIndex < 0 || startIndex >= _samples.Count - 1)
                return false;

            GestureSample start = _samples[startIndex];
            GestureSample end = _samples[_samples.Count - 1];
            float yDelta = end.localPosition.y - start.localPosition.y;
            float horizontalTravel = Vector2.Distance(
                new Vector2(start.localPosition.x, start.localPosition.z),
                new Vector2(end.localPosition.x, end.localPosition.z));

            if (horizontalTravel >= LiftFadeHorizontalThreshold)
                return false;

            bool isLift = kind == ConductorGestureKind.LiftTendu;
            if (isLift && yDelta <= LiftFadeVerticalThreshold)
                return false;
            if (!isLift && yDelta >= -LiftFadeVerticalThreshold)
                return false;

            string targetZoneId = ResolveTargetZoneId(start.worldPosition, end.leftGrip);
            if (string.IsNullOrEmpty(targetZoneId))
                return false;

            detection = new DetectionResult
            {
                gesture = new ConductorGestureEvent(
                    kind,
                    targetZoneId,
                    Mathf.Clamp01((Mathf.Abs(yDelta) - LiftFadeVerticalThreshold) / 0.30f),
                    start.worldPosition,
                    sampleTime),
                swayDirection = 0
            };
            return true;
        }

        private bool TryDetectSway(double sampleTime, out DetectionResult detection)
        {
            detection = default;
            if (!IsReady(ConductorGestureKind.Sway, sampleTime) || _samples.Count < 3)
                return false;

            int startIndex = FindWindowStartIndex(sampleTime - SwayDurationSeconds);
            if (startIndex < 0 || startIndex >= _samples.Count - 2)
                return false;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            int zeroCrossings = 0;
            int previousSign = 0;
            int swayDirection = 0;

            for (int i = startIndex; i < _samples.Count; i++)
            {
                float x = _samples[i].localPosition.x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;

                int sign = VelocitySign(_samples[i].localVelocity.x);
                if (sign != 0)
                {
                    if (previousSign != 0 && sign != previousSign)
                        zeroCrossings++;
                    previousSign = sign;
                    swayDirection = sign;
                }
            }

            float amplitude = (maxX - minX) * 0.5f;
            if (amplitude < SwayAmplitudeThreshold || zeroCrossings < 3 || swayDirection == 0)
                return false;

            string targetZoneId = ResolveTargetZoneId(_samples[startIndex].worldPosition, _samples[_samples.Count - 1].leftGrip);
            if (string.IsNullOrEmpty(targetZoneId))
                return false;

            detection = new DetectionResult
            {
                gesture = new ConductorGestureEvent(
                    ConductorGestureKind.Sway,
                    targetZoneId,
                    Mathf.Clamp01((amplitude - SwayAmplitudeThreshold) / 0.25f),
                    _samples[startIndex].worldPosition,
                    sampleTime),
                swayDirection = swayDirection
            };
            return true;
        }

        private string ResolveTargetZoneId(Vector3 originPosition, bool leftGrip)
        {
            string targetZoneId = _zoneController?.GetClosestZoneId(originPosition, TargetZoneMaxDistance);
            if (string.IsNullOrEmpty(targetZoneId))
                return null;

            return leftGrip ? AllZonesSentinel : targetZoneId;
        }

        private bool IsReady(ConductorGestureKind kind, double sampleTime)
        {
            return !_refractoryUntilByKind.TryGetValue(kind, out var readyAt) || sampleTime >= readyAt;
        }

        private int FindWindowStartIndex(double earliestTime)
        {
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].time >= earliestTime)
                    return i;
            }

            return -1;
        }

        private static int VelocitySign(float velocityX)
        {
            if (velocityX > MinVelocitySign)
                return 1;
            if (velocityX < -MinVelocitySign)
                return -1;
            return 0;
        }

        private void ApplySway(float magnitude, int swayDirection)
        {
            if (_store?.State == null || swayDirection == 0)
                return;

            float currentTempo = _store.State.tempo;
            _swayBaselineTempo = currentTempo;
            _swayTargetTempo = Mathf.Clamp(currentTempo * (1f + TempoNudgeScale * magnitude * swayDirection), 60f, 160f);
            _swayStartBar = _currentAbsoluteBar;
            _swayEndBar = _currentAbsoluteBar + SwayDecayBars;
            _swayActive = true;
            _store.SetTempo(_swayTargetTempo);
        }

        private void HandleConductingModeChanged(ConductingModeChangedEvent evt)
        {
            SetConductingMode(evt.On);
        }

        private void HandleBarStart(int absoluteBar, double dspTime)
        {
            _currentAbsoluteBar = Mathf.Max(1, absoluteBar);

            if (!_swayActive || _store?.State == null)
                return;

            if (_currentAbsoluteBar >= _swayEndBar)
            {
                _store.SetTempo(_swayBaselineTempo);
                _swayActive = false;
                return;
            }

            float progress = Mathf.Clamp01((_currentAbsoluteBar - _swayStartBar) / (float)(_swayEndBar - _swayStartBar));
            float nextTempo = Mathf.Lerp(_swayTargetTempo, _swayBaselineTempo, progress);
            if (Mathf.Abs(_store.State.tempo - nextTempo) > 0.01f)
                _store.SetTempo(nextTempo);
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
                _eventBus.Unsubscribe<ConductingModeChangedEvent>(HandleConductingModeChanged);
            if (_sequencer != null)
                _sequencer.OnBarStart -= HandleBarStart;
        }
    }
}
