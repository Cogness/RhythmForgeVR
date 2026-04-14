using UnityEngine;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Attach to a world-space Canvas root. Holding the MX Ink back button while the
    /// stylus ray hits this panel grabs and drags it through world space.
    /// The panel always faces the user while being dragged.
    /// </summary>
    public class PanelDragger : MonoBehaviour
    {
        [SerializeField] private InputMapper _input;
        [SerializeField] private float _maxRayDistance = 4f;

        private bool _isDragging;
        private Vector3 _grabOffset;     // offset from panel origin to ray hit in world space
        private float _grabDistance;     // distance along ray at grab time
        private IInputProvider _inputProvider;

        public void Configure(IInputProvider input)
        {
            _inputProvider = input;
            _input = input as InputMapper ?? _input;
        }

        private void Update()
        {
            var input = _inputProvider ?? (IInputProvider)_input;
            if (input == null) return;
            if (!input.IsStylusActive) return;

            var pose      = input.StylusPose;
            Vector3 origin    = pose.position;
            Vector3 direction = pose.rotation * Vector3.forward;

            bool holdingBack = input.BackButton;

            if (holdingBack && !_isDragging)
            {
                // Try to grab: check if ray hits our canvas plane
                var plane = new Plane(transform.forward, transform.position);
                float dist;
                if (plane.Raycast(new Ray(origin, direction), out dist) && dist < _maxRayDistance)
                {
                    Vector3 hitWorld = origin + direction * dist;
                    _grabOffset  = transform.position - hitWorld;
                    _grabDistance = dist;
                    _isDragging  = true;
                }
            }
            else if (!holdingBack)
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                // Move panel so the grabbed point stays under the ray
                Vector3 newHit = origin + direction * _grabDistance;
                transform.position = newHit + _grabOffset;

                // Always face the user (flatten to horizontal yaw)
                Vector3 toUser = origin - transform.position;
                toUser.y = 0f;
                if (toUser.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(-toUser.normalized, Vector3.up);
            }
        }
    }
}
