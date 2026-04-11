using UnityEngine;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Centralizes input mapping from MX Ink stylus and left Quest controller
    /// into unified application actions.
    /// </summary>
    public class InputMapper : MonoBehaviour
    {
        [SerializeField] private StylusHandler _stylusHandler;

        // MX Ink state accessors
        public bool IsStylusActive => _stylusHandler != null && _stylusHandler.CurrentState.isActive;
        public float TipPressure => _stylusHandler != null ? _stylusHandler.CurrentState.tip_value : 0f;
        public float MiddlePressure => _stylusHandler != null ? _stylusHandler.CurrentState.cluster_middle_value : 0f;
        public bool FrontButton => _stylusHandler != null && _stylusHandler.CurrentState.cluster_front_value;
        public bool BackButton => _stylusHandler != null && _stylusHandler.CurrentState.cluster_back_value;
        public bool BackDoubleTap => _stylusHandler != null && _stylusHandler.CurrentState.cluster_back_double_tap_value;
        public Pose StylusPose => _stylusHandler != null ? _stylusHandler.CurrentState.inkingPose : new Pose();

        public bool IsDrawing => TipPressure > 0.05f;

        // Left controller state (OVRInput)
        public bool LeftTrigger => OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        public bool LeftTriggerDown => OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        public bool LeftTriggerUp => OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        public bool LeftGrip => OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        public Vector2 LeftThumbstick => OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        public bool ButtonOne => OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch); // X button
        public bool ButtonTwo => OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch); // Y button

        // Front button edge detection
        private bool _prevFrontButton;
        private bool _prevBackButton;

        public bool FrontButtonDown { get; private set; }
        public bool BackButtonDown { get; private set; }

        private void Update()
        {
            FrontButtonDown = FrontButton && !_prevFrontButton;
            BackButtonDown = BackButton && !_prevBackButton;
            _prevFrontButton = FrontButton;
            _prevBackButton = BackButton;
        }
    }
}
