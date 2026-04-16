using UnityEngine;
using RhythmForge.Core.Session;
using RhythmForge.UI;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Uses the left Quest controller to raycast, select, and drag pattern instances in 3D.
    /// Moving an instance updates its spatial mix parameters (pan, brightness, depth).
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
        private float _grabDistanceProgress = 0.3f;

        // The currently hovered visualizer (for highlighting)
        private PatternVisualizer _hoveredVisualizer;

        /// <summary>Called by RhythmForgeBootstrapper to inject component references.</summary>
        public void Configure(IInputProvider input, Transform leftController,
            LineRenderer rayLine, LayerMask instanceLayer)
        {
            _inputProvider = input;
            _input = input as InputMapper ?? _input;
            _leftControllerTransform = leftController;
            _rayVisual = rayLine;
            _instanceLayer = instanceLayer;
        }

        public void Initialize(SessionStore store)
        {
            _store = store;
        }

        private void Update()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            if (_store == null || input == null) return;

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
                    float currentDist = Vector3.Distance(_leftControllerTransform.position, hit.collider.transform.position);
                    _grabDistanceProgress = Mathf.InverseLerp(0.4f, 6f, Mathf.Clamp(currentDist, 0.4f, 6f));
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
                float currentDist = Vector3.Distance(_leftControllerTransform.position, closest.transform.position);
                _grabDistanceProgress = Mathf.InverseLerp(0.4f, 6f, Mathf.Clamp(currentDist, 0.4f, 6f));
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

            // Left thumbstick Y while grabbing = push/pull depth
            if (input != null)
            {
                float thumbY = input.LeftThumbstick.y;
                _grabDistanceProgress = Mathf.Clamp01(_grabDistanceProgress + thumbY * Time.deltaTime * 0.8f);
            }

            float distance = Mathf.Lerp(0.4f, 6f, _grabDistanceProgress);

            Ray ray = new Ray(_leftControllerTransform.position, _leftControllerTransform.forward);
            Vector3 targetPos = ray.GetPoint(distance) - _grabOffset;

            _hasMoved = true;
            _store.UpdateInstance(_grabbedInstanceId, position: targetPos);
        }

        private void UpdateRayVisual()
        {
            if (_rayVisual == null || _leftControllerTransform == null) return;

            var input = _inputProvider ?? (IInputProvider)_input;
            if (input != null && (input.LeftTrigger || _grabbedInstanceId != null))
            {
                _rayVisual.enabled = true;
                _rayVisual.SetPosition(0, _leftControllerTransform.position);
                float rayLength = _grabbedInstanceId != null
                    ? Mathf.Lerp(0.4f, 6f, _grabDistanceProgress)
                    : _maxRayDistance;
                _rayVisual.SetPosition(1, _leftControllerTransform.position + _leftControllerTransform.forward * rayLength);
            }
            else
            {
                _rayVisual.enabled = false;
            }
        }
    }
}
