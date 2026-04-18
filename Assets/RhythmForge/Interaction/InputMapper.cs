using UnityEngine;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Centralizes input mapping from MX Ink stylus and left Quest controller
    /// into unified application actions.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class InputMapper : MonoBehaviour, IInputProvider
    {
        [SerializeField] private StylusHandler _stylusHandler;

        /// <summary>Called by RhythmForgeBootstrapper to inject the stylus handler.</summary>
        public void Configure(StylusHandler stylusHandler)
        {
            _stylusHandler = stylusHandler;
        }

        // MX Ink state accessors
        public bool IsStylusActive => _stylusHandler != null && _stylusHandler.CurrentState.isActive;
        public float TipPressure => _stylusHandler != null ? _stylusHandler.CurrentState.tip_value : 0f;
        public float MiddlePressure => _stylusHandler != null ? _stylusHandler.CurrentState.cluster_middle_value : 0f;
        public bool FrontButton => _stylusHandler != null && _stylusHandler.CurrentState.cluster_front_value;
        public bool BackButton => _stylusHandler != null && _stylusHandler.CurrentState.cluster_back_value;
        public bool BackDoubleTap => _stylusHandler != null && _stylusHandler.CurrentState.cluster_back_double_tap_value;
        public Pose StylusPose => _stylusHandler != null ? _stylusHandler.CurrentState.inkingPose : new Pose();

        public float DrawPressure => Mathf.Max(TipPressure, MiddlePressure);
        public bool IsDrawing => DrawPressure > 0.05f;

        // Left controller state (OVRInput)
        public bool LeftTrigger => OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        public bool LeftTriggerDown => OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        public bool LeftTriggerUp => OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
        public bool LeftGrip => OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        public Vector2 LeftThumbstick => OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        public bool ButtonOne => OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch); // X button
        public bool ButtonTwo { get; private set; }
        public bool ButtonTwoLongPress { get; private set; }

        // Front button edge detection
        private bool _prevFrontButton;
        private bool _prevBackButton;
        private bool _buttonTwoHeld;
        private bool _buttonTwoLongPressFired;
        private float _buttonTwoPressStartTime;

        private const float ButtonTwoLongPressSeconds = 0.8f;

        public bool FrontButtonDown { get; private set; }
        public bool BackButtonDown  { get; private set; }

        /// <summary>
        /// Set this to true to prevent other systems from acting on FrontButtonDown
        /// this frame. StylusUIPointer sets it when a UI button is clicked.
        /// </summary>
        public bool FrontButtonConsumed { get; set; }

        private void Update()
        {
            // Reset consumed flags first — other systems running later this frame may set them.
            FrontButtonConsumed = false;
            ButtonTwo = false;
            ButtonTwoLongPress = false;

            FrontButtonDown     = FrontButton && !_prevFrontButton;
            BackButtonDown      = BackButton  && !_prevBackButton;
            _prevFrontButton    = FrontButton;
            _prevBackButton     = BackButton;

            bool buttonTwoHeldNow = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch);
            if (buttonTwoHeldNow && !_buttonTwoHeld)
            {
                _buttonTwoHeld = true;
                _buttonTwoLongPressFired = false;
                _buttonTwoPressStartTime = Time.unscaledTime;
            }
            else if (buttonTwoHeldNow && _buttonTwoHeld && !_buttonTwoLongPressFired)
            {
                if (Time.unscaledTime - _buttonTwoPressStartTime >= ButtonTwoLongPressSeconds)
                {
                    _buttonTwoLongPressFired = true;
                    ButtonTwoLongPress = true;
                }
            }
            else if (!buttonTwoHeldNow && _buttonTwoHeld)
            {
                if (!_buttonTwoLongPressFired)
                    ButtonTwo = true;

                _buttonTwoHeld = false;
                _buttonTwoLongPressFired = false;
            }
        }
    }
}
