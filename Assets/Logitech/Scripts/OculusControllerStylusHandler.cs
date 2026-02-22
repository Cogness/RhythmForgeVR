using UnityEngine;

public class OculusControllerStylusHandler : StylusHandler
{
    public enum Handedness
    {
        Left,
        Right
    }

    [SerializeField] private Handedness _handedness = Handedness.Right;
    [SerializeField] private Transform _trackingSpaceOverride;
    [SerializeField] private Vector3 _positionOffset = Vector3.zero;
    [SerializeField] private Vector3 _rotationOffsetEuler = Vector3.zero;
    [SerializeField] private float _doubleTapWindow = 0.3f;

    private float _lastTapTime = -10f;
    private Transform _cachedTrackingSpace;

    public Handedness ControllerHand
    {
        get { return _handedness; }
        set { _handedness = value; }
    }

    public override bool CanDraw()
    {
        return _stylus.isActive;
    }

    private void Update()
    {
        OVRInput.Update();

        OVRInput.Controller controller = _handedness == Handedness.Right
            ? OVRInput.Controller.RTouch
            : OVRInput.Controller.LTouch;

        bool isConnected = OVRInput.IsControllerConnected(controller);
        bool positionTracked = OVRInput.GetControllerPositionTracked(controller);
        bool orientationTracked = OVRInput.GetControllerOrientationTracked(controller);

        _stylus.isActive = isConnected && (positionTracked || orientationTracked);
        _stylus.isOnRightHand = _handedness == Handedness.Right;
        _stylus.positionIsTracked = positionTracked;
        _stylus.positionIsValid = positionTracked;
        _stylus.docked = false;

        Vector3 localPosition = OVRInput.GetLocalControllerPosition(controller);
        Quaternion localRotation = OVRInput.GetLocalControllerRotation(controller);

        Transform trackingSpace = _trackingSpaceOverride != null ? _trackingSpaceOverride : GetTrackingSpace();
        if (trackingSpace != null)
        {
            transform.position = trackingSpace.TransformPoint(localPosition);
            transform.rotation = trackingSpace.rotation * localRotation;
        }
        else
        {
            transform.position = localPosition;
            transform.rotation = localRotation;
        }

        if (_positionOffset != Vector3.zero)
        {
            transform.position += transform.rotation * _positionOffset;
        }
        if (_rotationOffsetEuler != Vector3.zero)
        {
            transform.rotation = transform.rotation * Quaternion.Euler(_rotationOffsetEuler);
        }

        OVRInput.Axis1D indexAxis = _handedness == Handedness.Right
            ? OVRInput.Axis1D.PrimaryIndexTrigger
            : OVRInput.Axis1D.SecondaryIndexTrigger;
        OVRInput.Axis1D gripAxis = _handedness == Handedness.Right
            ? OVRInput.Axis1D.PrimaryHandTrigger
            : OVRInput.Axis1D.SecondaryHandTrigger;

        _stylus.tip_value = OVRInput.Get(indexAxis, controller);
        _stylus.cluster_middle_value = OVRInput.Get(gripAxis, controller);

        _stylus.cluster_front_value = OVRInput.Get(OVRInput.Button.One, controller);
        _stylus.cluster_back_value = OVRInput.Get(OVRInput.Button.Two, controller);

        bool doubleTap = false;
        if (OVRInput.GetDown(OVRInput.Button.Two, controller))
        {
            float now = Time.time;
            if (now - _lastTapTime <= _doubleTapWindow)
            {
                doubleTap = true;
            }
            _lastTapTime = now;
        }
        _stylus.cluster_back_double_tap_value = doubleTap;

        _stylus.any = _stylus.tip_value > 0f || _stylus.cluster_middle_value > 0f ||
                      _stylus.cluster_front_value || _stylus.cluster_back_value ||
                      _stylus.cluster_back_double_tap_value;

        _stylus.inkingPose.position = transform.position;
        _stylus.inkingPose.rotation = transform.rotation;
    }

    private Transform GetTrackingSpace()
    {
        if (_cachedTrackingSpace != null)
        {
            return _cachedTrackingSpace;
        }

        OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null && rig.trackingSpace != null)
        {
            _cachedTrackingSpace = rig.trackingSpace;
            return _cachedTrackingSpace;
        }

        OVRManager manager = FindObjectOfType<OVRManager>();
        if (manager != null)
        {
            Transform trackingSpace = manager.transform.Find("TrackingSpace");
            if (trackingSpace != null)
            {
                _cachedTrackingSpace = trackingSpace;
                return _cachedTrackingSpace;
            }
        }

        return null;
    }
}
