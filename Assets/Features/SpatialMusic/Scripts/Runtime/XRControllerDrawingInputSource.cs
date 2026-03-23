using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class XRControllerDrawingInputSource : MonoBehaviour, IDrawingInputSource
{
    [SerializeField] private XRNode _preferredHand = XRNode.RightHand;
    [SerializeField] private float _drawThreshold = 0.12f;
    [SerializeField] private string[] _rightAnchorNames = { "RightHandAnchor", "RightControllerAnchor", "right_touch_controller_world" };
    [SerializeField] private string[] _leftAnchorNames = { "LeftHandAnchor", "LeftControllerAnchor", "left_touch_controller_world" };

    private readonly List<InputDevice> _deviceCache = new(2);
    private bool _wasDrawing;
    private bool _wasPrimaryPressed;
    private XRNode _activeHand;
    private float _clearAllHoldTimer;
    private DrawingInputState _state;
    private Transform _rightAnchor;
    private Transform _leftAnchor;

    public string SourceName => "XR Controller";
    public int Priority => 50;
    public bool WantsControl => GetHandDevice(XRNode.RightHand).isValid || GetHandDevice(XRNode.LeftHand).isValid;
    public bool DrawStartedThisFrame { get; private set; }
    public bool IsDrawing { get; private set; }
    public bool DrawEndedThisFrame { get; private set; }
    public bool ClearLastRequestedThisFrame { get; private set; }
    public bool ClearAllRequestedThisFrame { get; private set; }
    public DrawingInputState State => _state;

    private void Awake()
    {
        _rightAnchor = FindFirstMatchingTransform(_rightAnchorNames);
        _leftAnchor = FindFirstMatchingTransform(_leftAnchorNames);
        _activeHand = _preferredHand;
    }

    public void Tick()
    {
        DrawStartedThisFrame = false;
        DrawEndedThisFrame = false;
        ClearLastRequestedThisFrame = false;
        ClearAllRequestedThisFrame = false;

        InputDevice rightDevice = GetHandDevice(XRNode.RightHand);
        InputDevice leftDevice = GetHandDevice(XRNode.LeftHand);
        XRNode handToUse = ResolveHand(rightDevice, leftDevice);
        InputDevice activeDevice = handToUse == XRNode.LeftHand ? leftDevice : rightDevice;

        if (!activeDevice.isValid)
        {
            _state = default;
            IsDrawing = false;
            _wasDrawing = false;
            _wasPrimaryPressed = false;
            _clearAllHoldTimer = 0f;
            return;
        }

        float trigger = GetAxis(activeDevice, CommonUsages.trigger);
        bool drawPressed = trigger >= _drawThreshold;
        DrawStartedThisFrame = drawPressed && !_wasDrawing;
        DrawEndedThisFrame = !drawPressed && _wasDrawing;
        IsDrawing = drawPressed;
        _wasDrawing = drawPressed;
        if (drawPressed)
        {
            _activeHand = handToUse;
        }

        bool primaryPressed = GetButton(activeDevice, CommonUsages.primaryButton);
        ClearLastRequestedThisFrame = primaryPressed && !_wasPrimaryPressed;
        _wasPrimaryPressed = primaryPressed;

        bool secondaryHeld = GetButton(activeDevice, CommonUsages.secondaryButton);
        if (secondaryHeld)
        {
            _clearAllHoldTimer += Time.deltaTime;
            if (_clearAllHoldTimer >= 0.6f)
            {
                ClearAllRequestedThisFrame = true;
                _clearAllHoldTimer = -999f;
            }
        }
        else
        {
            _clearAllHoldTimer = 0f;
        }

        bool hasPose = TryGetWorldPose(handToUse, activeDevice, out Pose worldPose);
        _state = new DrawingInputState(worldPose, trigger, hasPose);
    }

    private XRNode ResolveHand(InputDevice rightDevice, InputDevice leftDevice)
    {
        if (_wasDrawing)
        {
            return _activeHand;
        }

        bool rightDrawing = rightDevice.isValid && GetAxis(rightDevice, CommonUsages.trigger) >= _drawThreshold;
        bool leftDrawing = leftDevice.isValid && GetAxis(leftDevice, CommonUsages.trigger) >= _drawThreshold;
        if (rightDrawing)
        {
            return XRNode.RightHand;
        }

        if (leftDrawing)
        {
            return XRNode.LeftHand;
        }

        if (_preferredHand == XRNode.LeftHand && leftDevice.isValid)
        {
            return XRNode.LeftHand;
        }

        return rightDevice.isValid ? XRNode.RightHand : XRNode.LeftHand;
    }

    private bool TryGetWorldPose(XRNode hand, InputDevice device, out Pose pose)
    {
        Transform anchor = hand == XRNode.LeftHand ? _leftAnchor : _rightAnchor;
        if (anchor != null)
        {
            pose = new Pose(anchor.position, anchor.rotation);
            return true;
        }

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
        pose = new Pose(localPosition, hasRotation ? localRotation : Quaternion.identity);
        return hasPosition || hasRotation;
    }

    private InputDevice GetHandDevice(XRNode node)
    {
        _deviceCache.Clear();
        InputDevices.GetDevicesAtXRNode(node, _deviceCache);
        return _deviceCache.Count > 0 ? _deviceCache[0] : default;
    }

    private static float GetAxis(InputDevice device, InputFeatureUsage<float> usage)
    {
        return device.TryGetFeatureValue(usage, out float value) ? value : 0f;
    }

    private static bool GetButton(InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.TryGetFeatureValue(usage, out bool value) && value;
    }

    private static Transform FindFirstMatchingTransform(IEnumerable<string> candidateNames)
    {
        foreach (string candidateName in candidateNames)
        {
            GameObject match = GameObject.Find(candidateName);
            if (match != null)
            {
                return match.transform;
            }
        }

        return null;
    }
}
