using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RhythmForge.Core.Session;
using RhythmForge.Audio;
using RhythmForge.UI;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Uses the left Quest controller to raycast, select, and drag pattern instances in 3D.
    /// Moving an instance updates its spatial mix parameters (brightness, depth-driven sends, gain trim).
    /// </summary>
    public class InstanceGrabber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputMapper _input;
        [SerializeField] private Transform _leftControllerTransform;
        [SerializeField] private LineRenderer _rayVisual;

        [Header("Settings")]
        [SerializeField] private float _maxRayDistance = 8f;
        [SerializeField] private float _grabSnapRadius = 0.12f;
        [SerializeField] private LayerMask _instanceLayer;

        private SessionStore _store;
        private string _grabbedInstanceId;
        private Vector3 _grabOffset;
        private bool _hasMoved;
        private IInputProvider _inputProvider;
        private float _grabDistance = 1.2f;
        private AudioEngine _audioEngine;
        private readonly Dictionary<string, float> _lastReleaseTimeById = new Dictionary<string, float>();
        private Coroutine _hapticCoroutine;

        private const float MinGrabDistance = 0.4f;
        private const float MaxGrabDistance = 6.0f;
        private const float GrabPushSpeed = 1.8f;
        private const float HapticGraceSeconds = 1f;
        private const float HapticAmplitude = 0.12f;
        private const float HapticFrequency = 0.2f;
        private const float HapticDurationSeconds = 0.006f;

        // The currently hovered visualizer (for highlighting)
        private PatternVisualizer _hoveredVisualizer;

        // Lazy-resolved left controller — OVRCameraRig anchors may not be
        // available during Awake when the bootstrapper runs.
        private Transform _cachedLeftController;
        private bool _leftControllerResolved;
        private int _resolveAttempts;
        private const int MaxResolveAttempts = 120; // ~2 seconds at 60 fps

        /// <summary>Called by RhythmForgeBootstrapper to inject component references.</summary>
        public void Configure(IInputProvider input, Transform leftController,
            LineRenderer rayLine, LayerMask instanceLayer)
        {
            _inputProvider = input;
            _input = input as InputMapper ?? _input;
            _leftControllerTransform = leftController;
            _rayVisual = rayLine;
            _instanceLayer = instanceLayer;

            if (leftController != null)
            {
                _cachedLeftController = leftController;
                _leftControllerResolved = true;
            }
        }

        /// <summary>
        /// Re-inject the left controller transform. Called by the bootstrapper
        /// after tracking has initialized (Start / first valid head pose).
        /// </summary>
        public void SetLeftController(Transform leftController)
        {
            if (leftController == null) return;
            _leftControllerTransform = leftController;
            _cachedLeftController = leftController;
            _leftControllerResolved = true;
        }

        private Transform ResolveLeftController()
        {
            if (_leftControllerResolved)
                return _cachedLeftController;

            // Throttle re-resolution attempts to avoid spamming Find.
            if (_resolveAttempts >= MaxResolveAttempts)
                return null;

            _resolveAttempts++;

            var rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null && rig.leftControllerAnchor != null)
            {
                _cachedLeftController = rig.leftControllerAnchor;
                _leftControllerTransform = _cachedLeftController;
                _leftControllerResolved = true;
                Debug.Log("[RhythmForge] InstanceGrabber: left controller resolved lazily.");
                return _cachedLeftController;
            }

            var go = GameObject.Find("LeftControllerAnchor");
            if (go != null)
            {
                _cachedLeftController = go.transform;
                _leftControllerTransform = _cachedLeftController;
                _leftControllerResolved = true;
                Debug.Log("[RhythmForge] InstanceGrabber: left controller resolved by name.");
                return _cachedLeftController;
            }

            return null;
        }

        public void Initialize(SessionStore store, AudioEngine audioEngine = null)
        {
            _store = store;
            BindAudioEngine(audioEngine);
        }

        private void Update()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            if (_store == null || input == null) return;

            // Lazy-resolve left controller if it wasn't available at boot.
            if (_leftControllerTransform == null)
                ResolveLeftController();

            // Grab start
            if (input.LeftTriggerDown)
            {
                TryGrab();
            }

            // Grab move
            if (input.LeftTrigger && _grabbedInstanceId != null)
            {
                DragInstance();
            }

            // Grab release
            if (input.LeftTriggerUp)
            {
                if (_grabbedInstanceId != null)
                {
                    if (!_hasMoved)
                    {
                        // Click without drag → select
                        _store.SetSelectedInstance(_grabbedInstanceId);
                    }
                    _lastReleaseTimeById[_grabbedInstanceId] = Time.unscaledTime;
                    _grabbedInstanceId = null;
                }
                else
                {
                    // Click on empty → deselect
                    _store.SetSelectedInstance(null);
                }
            }

            UpdateRayVisual();
        }

        private void TryGrab()
        {
            if (_leftControllerTransform == null) return;

            Ray ray = new Ray(_leftControllerTransform.position, _leftControllerTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _instanceLayer))
            {
                var visualizer = hit.collider.GetComponentInParent<PatternVisualizer>();
                if (visualizer != null)
                {
                    _grabbedInstanceId = visualizer.InstanceId;
                    _grabDistance = 1.2f;
                    _grabOffset = hit.point - hit.collider.transform.position;
                    _hasMoved = false;
                    _store.SetSelectedInstance(_grabbedInstanceId);
                    return;
                }
            }

            // No hit — check proximity to any instance
            float closestDist = _grabSnapRadius;
            PatternVisualizer closest = null;

            foreach (var vis in FindObjectsByType<PatternVisualizer>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(ray.GetPoint(0.5f), vis.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = vis;
                }
            }

            if (closest != null)
            {
                _grabbedInstanceId = closest.InstanceId;
                _grabDistance = 1.2f;
                _grabOffset = Vector3.zero;
                _hasMoved = false;
                _store.SetSelectedInstance(_grabbedInstanceId);
            }
            else
            {
                _grabbedInstanceId = null;
            }
        }

        private void DragInstance()
        {
            if (_leftControllerTransform == null || _grabbedInstanceId == null) return;

            var input = _inputProvider ?? (IInputProvider)_input;
            float stickY = input != null ? input.LeftThumbstick.y : 0f;
            if (Mathf.Abs(stickY) > 0.15f)
            {
                _grabDistance = Mathf.Clamp(
                    _grabDistance + stickY * GrabPushSpeed * Time.deltaTime,
                    MinGrabDistance,
                    MaxGrabDistance);
            }

            Ray ray = new Ray(_leftControllerTransform.position, _leftControllerTransform.forward);
            Vector3 targetPos = ray.GetPoint(_grabDistance) - _grabOffset;

            _hasMoved = true;
            _store.UpdateInstance(_grabbedInstanceId, position: targetPos);
        }

        private void UpdateRayVisual()
        {
            if (_rayVisual == null) return;
            if (_leftControllerTransform == null) { _rayVisual.enabled = false; return; }

            // Show the ray while the left trigger is held or while dragging.
            if (InputHasLeftTrigger() || _grabbedInstanceId != null)
            {
                _rayVisual.enabled = true;
                _rayVisual.SetPosition(0, _leftControllerTransform.position);
                _rayVisual.SetPosition(1, _leftControllerTransform.position + _leftControllerTransform.forward * _maxRayDistance);
            }
            else
            {
                _rayVisual.enabled = false;
            }
        }

        private bool InputHasLeftTrigger()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            return input != null && input.LeftTrigger;
        }

        private void OnDestroy()
        {
            BindAudioEngine(null);
            StopHaptics();
        }

        private void BindAudioEngine(AudioEngine audioEngine)
        {
            if (_audioEngine != null)
                _audioEngine.OnEventScheduled -= HandleEventScheduled;

            _audioEngine = audioEngine;

            if (_audioEngine != null)
                _audioEngine.OnEventScheduled += HandleEventScheduled;
        }

        private void HandleEventScheduled(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return;

            if (instanceId == _grabbedInstanceId)
            {
                TriggerHaptics();
                return;
            }

            if (_lastReleaseTimeById.TryGetValue(instanceId, out var releaseTime) &&
                Time.unscaledTime - releaseTime <= HapticGraceSeconds)
            {
                TriggerHaptics();
            }
        }

        private void TriggerHaptics()
        {
            if (!isActiveAndEnabled)
                return;

            if (_hapticCoroutine != null)
                StopCoroutine(_hapticCoroutine);

            _hapticCoroutine = StartCoroutine(HapticPulseCoroutine());
        }

        private IEnumerator HapticPulseCoroutine()
        {
            OVRInput.SetControllerVibration(HapticFrequency, HapticAmplitude, OVRInput.Controller.LTouch);
            yield return new WaitForSecondsRealtime(HapticDurationSeconds);
            StopHaptics();
        }

        private void StopHaptics()
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            _hapticCoroutine = null;
        }
    }
}
