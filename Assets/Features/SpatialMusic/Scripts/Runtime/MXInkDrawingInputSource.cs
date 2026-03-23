using UnityEngine;

[DisallowMultipleComponent]
public class MXInkDrawingInputSource : MonoBehaviour, IDrawingInputSource
{
    [SerializeField] private StylusHandler _stylusHandler;
    [SerializeField] private float _drawThreshold = 0.08f;

    private bool _wasDrawing;
    private bool _wasBackPressed;
    private bool _wasDoubleTapPressed;
    private DrawingInputState _state;

    public string SourceName => "MX Ink";
    public int Priority => 100;
    public bool WantsControl => _stylusHandler != null && _stylusHandler.CurrentState.isActive;
    public bool DrawStartedThisFrame { get; private set; }
    public bool IsDrawing { get; private set; }
    public bool DrawEndedThisFrame { get; private set; }
    public bool ClearLastRequestedThisFrame { get; private set; }
    public bool ClearAllRequestedThisFrame { get; private set; }
    public DrawingInputState State => _state;

    private void Awake()
    {
        if (_stylusHandler == null)
        {
            _stylusHandler = FindFirstObjectByType<StylusHandler>();
        }
    }

    public void Tick()
    {
        DrawStartedThisFrame = false;
        DrawEndedThisFrame = false;
        ClearLastRequestedThisFrame = false;
        ClearAllRequestedThisFrame = false;

        if (_stylusHandler == null)
        {
            _state = default;
            IsDrawing = false;
            _wasDrawing = false;
            return;
        }

        StylusInputs stylus = _stylusHandler.CurrentState;
        float pressure = Mathf.Clamp01(Mathf.Max(stylus.tip_value, stylus.cluster_middle_value));
        bool drawPressed = stylus.isActive && pressure >= _drawThreshold && _stylusHandler.CanDraw();

        DrawStartedThisFrame = drawPressed && !_wasDrawing;
        DrawEndedThisFrame = !drawPressed && _wasDrawing;
        IsDrawing = drawPressed;
        _wasDrawing = drawPressed;

        bool backPressed = stylus.cluster_back_value;
        bool doubleTapPressed = stylus.cluster_back_double_tap_value;
        ClearLastRequestedThisFrame = backPressed && !_wasBackPressed;
        ClearAllRequestedThisFrame = doubleTapPressed && !_wasDoubleTapPressed;
        _wasBackPressed = backPressed;
        _wasDoubleTapPressed = doubleTapPressed;

        Pose worldPose = new(_stylusHandler.transform.position, _stylusHandler.transform.rotation);
        _state = new DrawingInputState(worldPose, pressure, stylus.isActive);
    }
}
