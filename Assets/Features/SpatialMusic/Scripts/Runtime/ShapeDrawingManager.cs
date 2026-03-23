using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShapeDrawingManager : MonoBehaviour
{
    public static event System.Action AnyStrokeStarted;

    [SerializeField] private MonoBehaviour[] _inputSourceBehaviours = System.Array.Empty<MonoBehaviour>();
    [SerializeField] private MusicalShapeFactory _shapeFactory;
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private float _minimumStrokeLength = 0.08f;
    [SerializeField] private int _minimumStrokePoints = 5;

    private readonly List<IDrawingInputSource> _inputSources = new();
    private StrokeRecorder _strokeRecorder;
    private VisualStrokeRenderer _previewRenderer;
    private IDrawingInputSource _activeSource;

    private void Awake()
    {
        _strokeRecorder = new StrokeRecorder();
        if (_shapeFactory == null)
        {
            _shapeFactory = GetComponent<MusicalShapeFactory>();
        }

        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }

        CacheInputSources();
        CreatePreviewRenderer();
    }

    private void CacheInputSources()
    {
        _inputSources.Clear();

        if (_inputSourceBehaviours == null || _inputSourceBehaviours.Length == 0)
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            List<MonoBehaviour> collected = new();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDrawingInputSource)
                {
                    collected.Add(behaviours[i]);
                }
            }

            _inputSourceBehaviours = collected.ToArray();
        }

        for (int i = 0; i < _inputSourceBehaviours.Length; i++)
        {
            if (_inputSourceBehaviours[i] is IDrawingInputSource inputSource)
            {
                _inputSources.Add(inputSource);
            }
        }

        _inputSources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    private void CreatePreviewRenderer()
    {
        GameObject previewObject = new("StrokePreview");
        previewObject.transform.SetParent(transform, false);
        _previewRenderer = previewObject.AddComponent<VisualStrokeRenderer>();
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.7f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.35f, 0.8f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.4f, 1f)
            });
        Material material = new(Shader.Find("Universal Render Pipeline/Unlit"));
        _previewRenderer.Configure(material, gradient);
    }

    private void Update()
    {
        TickInputs();
        HandleLifecycleShortcuts();
        SelectActiveSource();
        HandleDrawing();
    }

    private void TickInputs()
    {
        for (int i = 0; i < _inputSources.Count; i++)
        {
            _inputSources[i].Tick();
        }
    }

    private void HandleLifecycleShortcuts()
    {
        for (int i = 0; i < _inputSources.Count; i++)
        {
            if (_inputSources[i].ClearAllRequestedThisFrame)
            {
                _shapeFactory.RemoveAllShapes();
                return;
            }

            if (_inputSources[i].ClearLastRequestedThisFrame)
            {
                _shapeFactory.RemoveLastShape();
                return;
            }
        }
    }

    private void SelectActiveSource()
    {
        if (_activeSource != null && _strokeRecorder.IsRecording)
        {
            return;
        }

        _activeSource = null;
        for (int i = 0; i < _inputSources.Count; i++)
        {
            if (_inputSources[i].DrawStartedThisFrame)
            {
                _activeSource = _inputSources[i];
                return;
            }
        }

        for (int i = 0; i < _inputSources.Count; i++)
        {
            if (_inputSources[i].WantsControl)
            {
                _activeSource = _inputSources[i];
                return;
            }
        }
    }

    private void HandleDrawing()
    {
        if (_activeSource == null)
        {
            return;
        }

        DrawingInputState state = _activeSource.State;
        if (_activeSource.DrawStartedThisFrame && state.HasValidPose)
        {
            _strokeRecorder.Begin(state.WorldPose.position, state.Pressure01, Time.time);
            AnyStrokeStarted?.Invoke();
            _signalSource?.InjectPulse(0.1f, false);
            _previewRenderer.SetPoints(_strokeRecorder.CurrentPoints);
        }

        if (_strokeRecorder.IsRecording && _activeSource.IsDrawing && state.HasValidPose)
        {
            if (_strokeRecorder.Append(state.WorldPose.position, state.Pressure01, Time.time))
            {
                _previewRenderer.SetPoints(_strokeRecorder.CurrentPoints);
            }
        }

        if (_strokeRecorder.IsRecording && _activeSource.DrawEndedThisFrame)
        {
            StrokeData stroke = _strokeRecorder.End();
            _previewRenderer.Clear();
            if (stroke.PointCount >= _minimumStrokePoints && stroke.TotalLength >= _minimumStrokeLength)
            {
                ShapeDescriptor descriptor = ShapeAnalyzer.Analyze(stroke);
                _shapeFactory.CreateShape(stroke, descriptor);
            }
        }
    }
}

