using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class DesktopDrawingInputSource : MonoBehaviour, IDrawingInputSource
{
    [SerializeField] private Camera _referenceCamera;
    [SerializeField] private float _drawDistance = 2.4f;
    [SerializeField] private Vector2 _planeOffset = new(0f, -0.1f);

    private bool _wasDrawing;
    private DrawingInputState _state;

    public string SourceName => "Desktop";
    public int Priority => 10;
    public bool WantsControl => _referenceCamera != null || Camera.main != null;
    public bool DrawStartedThisFrame { get; private set; }
    public bool IsDrawing { get; private set; }
    public bool DrawEndedThisFrame { get; private set; }
    public bool ClearLastRequestedThisFrame { get; private set; }
    public bool ClearAllRequestedThisFrame { get; private set; }
    public DrawingInputState State => _state;

    private void Awake()
    {
        if (_referenceCamera == null)
        {
            _referenceCamera = Camera.main;
        }
    }

    public void Tick()
    {
        DrawStartedThisFrame = false;
        DrawEndedThisFrame = false;
        ClearLastRequestedThisFrame = false;
        ClearAllRequestedThisFrame = false;

        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        Camera cameraToUse = _referenceCamera != null ? _referenceCamera : Camera.main;
        if (mouse == null || cameraToUse == null)
        {
            _state = default;
            IsDrawing = false;
            _wasDrawing = false;
            return;
        }

        Vector3 screenPoint = mouse.position.ReadValue();
        Ray ray = cameraToUse.ScreenPointToRay(screenPoint);
        Vector3 planeOrigin = cameraToUse.transform.position + (cameraToUse.transform.forward * _drawDistance) + new Vector3(_planeOffset.x, _planeOffset.y, 0f);
        Plane plane = new(cameraToUse.transform.forward, planeOrigin);
        bool hasPose = plane.Raycast(ray, out float enter);
        Vector3 worldPosition = hasPose ? ray.GetPoint(enter) : planeOrigin;
        Quaternion worldRotation = Quaternion.LookRotation(cameraToUse.transform.forward, Vector3.up);

        bool drawPressed = mouse.leftButton.isPressed;
        DrawStartedThisFrame = drawPressed && !_wasDrawing;
        DrawEndedThisFrame = !drawPressed && _wasDrawing;
        IsDrawing = drawPressed;
        _wasDrawing = drawPressed;

        ClearLastRequestedThisFrame = keyboard != null && keyboard.backspaceKey.wasPressedThisFrame && !keyboard.leftShiftKey.isPressed && !keyboard.rightShiftKey.isPressed;
        ClearAllRequestedThisFrame = keyboard != null && keyboard.backspaceKey.wasPressedThisFrame && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

        _state = new DrawingInputState(new Pose(worldPosition, worldRotation), drawPressed ? 1f : 0.75f, hasPose);
    }
}
