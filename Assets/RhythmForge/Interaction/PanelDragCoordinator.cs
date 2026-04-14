using UnityEngine;
using System.Collections.Generic;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Coordinates panel dragging for both the rigid main UI group and
    /// independent contextual panels.
    /// </summary>
    public class PanelDragCoordinator : MonoBehaviour
    {
        public enum DragMembership
        {
            Independent,
            MainGroup
        }

        private enum DragMode
        {
            None,
            Independent,
            MainGroup
        }

        private struct RegisteredPanel
        {
            public Canvas Canvas;
            public DragMembership Membership;
        }

        private struct GroupPanelPose
        {
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        [SerializeField] private InputMapper _input;
        [SerializeField] private Transform _lookAtTarget;

        private readonly List<RegisteredPanel> _panels = new List<RegisteredPanel>();
        private readonly Dictionary<Canvas, DragMembership> _memberships = new Dictionary<Canvas, DragMembership>();
        private readonly Dictionary<Canvas, GroupPanelPose> _mainGroupPoses = new Dictionary<Canvas, GroupPanelPose>();

        private bool _isDragging;
        private DragMode _dragMode;
        private Canvas _draggedPanel;

        private Vector3 _grabRayOrigin;
        private Vector3 _grabRayDirection;
        private float _grabDistance;
        private Vector3 _dragAnchorStartPosition;
        private Quaternion _dragAnchorStartRotation;
        private Vector3 _singlePanelGrabOffset;
        private IInputProvider _inputProvider;

        /// <summary>True when any panel is being dragged (back button held).</summary>
        public static bool IsDragging { get; private set; }

        public void Configure(IInputProvider input, Transform lookAtTarget)
        {
            _inputProvider = input;
            _input = input as InputMapper ?? _input;
            _lookAtTarget = lookAtTarget;
        }

        public void SetLookAtTarget(Transform lookAtTarget)
        {
            _lookAtTarget = lookAtTarget;
        }

        public void RegisterPanel(Canvas canvas, DragMembership membership)
        {
            if (canvas == null || _memberships.ContainsKey(canvas)) return;

            _panels.Add(new RegisteredPanel
            {
                Canvas = canvas,
                Membership = membership
            });
            _memberships[canvas] = membership;
        }

        private void Update()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            if (input == null) return;
            if (!input.IsStylusActive) return;

            var pose = input.StylusPose;
            Vector3 origin = pose.position;
            Vector3 direction = pose.rotation * Vector3.forward;

            // Check if back button is held and we're pointing at any panel
            bool holdingBack = input.BackButton;

            if (holdingBack && !_isDragging)
            {
                // Try to start drag - check if ray hits any panel
                Canvas hitPanel = FindHitPanel(origin, direction);
                if (hitPanel != null)
                {
                    StartDrag(origin, direction, hitPanel);
                }
            }
            else if (!holdingBack && _isDragging)
            {
                StopDrag();
            }

            if (!_isDragging) return;

            Vector3 originalHit = _grabRayOrigin + _grabRayDirection * _grabDistance;
            Vector3 currentHit = origin + direction * _grabDistance;
            Vector3 delta = currentHit - originalHit;

            switch (_dragMode)
            {
                case DragMode.MainGroup:
                    UpdateMainGroupDrag(delta);
                    break;
                case DragMode.Independent:
                    UpdateIndependentDrag(currentHit, origin);
                    break;
            }
        }

        private Canvas FindHitPanel(Vector3 origin, Vector3 direction)
        {
            float closestDist = 4f;
            Canvas hitCanvas = null;

            foreach (var panel in _panels)
            {
                var canvas = panel.Canvas;
                if (canvas == null) continue;
                if (!canvas.gameObject.activeInHierarchy) continue;

                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                float dist;
                if (plane.Raycast(new Ray(origin, direction), out dist) && dist < closestDist && dist > 0.01f)
                {
                    // Check if within canvas bounds
                    Vector3 worldHit = origin + direction * dist;
                    Vector3 localHit = canvas.transform.InverseTransformPoint(worldHit);
                    var rt = canvas.GetComponent<RectTransform>();
                    if (localHit.x >= 0 && localHit.x <= rt.sizeDelta.x &&
                        localHit.y >= 0 && localHit.y <= rt.sizeDelta.y)
                    {
                        closestDist = dist;
                        hitCanvas = canvas;
                    }
                }
            }
            return hitCanvas;
        }

        private void StartDrag(Vector3 origin, Vector3 direction, Canvas hitPanel)
        {
            _isDragging = true;
            IsDragging = true;
            _grabRayOrigin = origin;
            _grabRayDirection = direction;

            var plane = new Plane(hitPanel.transform.forward, hitPanel.transform.position);
            plane.Raycast(new Ray(origin, direction), out _grabDistance);

            DragMembership membership;
            if (!_memberships.TryGetValue(hitPanel, out membership))
                membership = DragMembership.Independent;

            if (membership == DragMembership.MainGroup)
                StartMainGroupDrag(hitPanel);
            else
                StartIndependentDrag(hitPanel, origin, direction);
        }

        private void StartMainGroupDrag(Canvas hitPanel)
        {
            _dragMode = DragMode.MainGroup;
            _draggedPanel = null;
            _dragAnchorStartPosition = GetMainGroupAnchorPosition();
            _dragAnchorStartRotation = GetSharedYaw(_dragAnchorStartPosition, hitPanel.transform.rotation);

            _mainGroupPoses.Clear();
            foreach (var panel in _panels)
            {
                if (panel.Canvas == null || panel.Membership != DragMembership.MainGroup) continue;

                _mainGroupPoses[panel.Canvas] = new GroupPanelPose
                {
                    LocalPosition = Quaternion.Inverse(_dragAnchorStartRotation) *
                        (panel.Canvas.transform.position - _dragAnchorStartPosition),
                    LocalRotation = Quaternion.Inverse(_dragAnchorStartRotation) *
                        panel.Canvas.transform.rotation
                };
            }
        }

        private void StartIndependentDrag(Canvas hitPanel, Vector3 origin, Vector3 direction)
        {
            _dragMode = DragMode.Independent;
            _draggedPanel = hitPanel;

            Vector3 grabPoint = origin + direction * _grabDistance;
            _singlePanelGrabOffset = hitPanel.transform.position - grabPoint;
        }

        private void UpdateMainGroupDrag(Vector3 delta)
        {
            Vector3 anchorPosition = _dragAnchorStartPosition + delta;
            Quaternion anchorRotation = GetSharedYaw(anchorPosition, _dragAnchorStartRotation);

            foreach (var poseEntry in _mainGroupPoses)
            {
                if (poseEntry.Key == null) continue;

                var pose = poseEntry.Value;
                poseEntry.Key.transform.position = anchorPosition + anchorRotation * pose.LocalPosition;
                poseEntry.Key.transform.rotation = anchorRotation * pose.LocalRotation;
            }
        }

        private void UpdateIndependentDrag(Vector3 currentHit, Vector3 origin)
        {
            if (_draggedPanel == null) return;

            _draggedPanel.transform.position = currentHit + _singlePanelGrabOffset;

            Vector3 toUser = origin - _draggedPanel.transform.position;
            toUser.y = 0f;
            if (toUser.sqrMagnitude > 0.001f)
                _draggedPanel.transform.rotation = Quaternion.LookRotation(-toUser.normalized, Vector3.up);
        }

        private void StopDrag()
        {
            _isDragging = false;
            _dragMode = DragMode.None;
            _draggedPanel = null;
            _mainGroupPoses.Clear();
            IsDragging = false;
        }

        private Vector3 GetMainGroupAnchorPosition()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var panel in _panels)
            {
                if (panel.Canvas == null || panel.Membership != DragMembership.MainGroup) continue;

                sum += GetCanvasWorldCenter(panel.Canvas);
                count++;
            }

            return count > 0 ? sum / count : Vector3.zero;
        }

        private Quaternion GetSharedYaw(Vector3 anchorPosition, Quaternion fallback)
        {
            if (_lookAtTarget == null) return fallback;

            Vector3 toUser = _lookAtTarget.position - anchorPosition;
            toUser.y = 0f;
            if (toUser.sqrMagnitude <= 0.001f) return fallback;

            return Quaternion.LookRotation(-toUser.normalized, Vector3.up);
        }

        private static Vector3 GetCanvasWorldCenter(Canvas canvas)
        {
            var rect = canvas.GetComponent<RectTransform>();
            if (rect == null) return canvas.transform.position;

            return rect.TransformPoint(new Vector3(rect.sizeDelta.x * 0.5f, rect.sizeDelta.y * 0.5f, 0f));
        }
    }
}
