using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LineDrawing : MonoBehaviour
{
    public static event Action<Vector3> AnyLineStarted;

    private readonly List<GameObject> _lines = new();
    private LineRenderer _currentLine;
    private List<float> _currentLineWidths = new();

    [SerializeField] private float _maxLineWidth = 0.01f;
    [SerializeField] private float _minLineWidth = 0.0005f;
    [SerializeField] private Material _material;
    [SerializeField] private Color _currentColor;
    [SerializeField] private float longPressDuration = 1.0f;
    [SerializeField] private StylusHandler _stylusHandler;

    private bool _lineWidthIsFixed;
    private bool _isDrawing;
    private bool _doubleTapDetected;
    private float _buttonPressedTimestamp;
    private Vector3 _previousLinePoint;

    private const float MinDistanceBetweenLinePoints = 0.0005f;

    public Color CurrentColor
    {
        get => _currentColor;
        set => _currentColor = value;
    }

    public float MaxLineWidth
    {
        get => _maxLineWidth;
        set => _maxLineWidth = value;
    }

    public bool LineWidthIsFixed
    {
        get => _lineWidthIsFixed;
        set => _lineWidthIsFixed = value;
    }

    public int LineCount => _lines.Count;
    public bool HasDrawnAnyLine => _lines.Count > 0;

    private void StartNewLine()
    {
        var lineObject = new GameObject("line");
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        _currentLine = lineRenderer;
        _currentLine.positionCount = 0;
        _currentLine.material = _material;
        _currentLine.material.color = _currentColor;
        _currentLine.loop = false;
        _currentLine.startWidth = _minLineWidth;
        _currentLine.endWidth = _minLineWidth;
        _currentLine.useWorldSpace = true;
        _currentLine.alignment = LineAlignment.View;
        _currentLine.widthCurve = new AnimationCurve();
        _currentLineWidths = new List<float>();
        _currentLine.shadowCastingMode = ShadowCastingMode.Off;
        _currentLine.receiveShadows = false;
        _lines.Add(lineObject);
        _previousLinePoint = Vector3.zero;
        AnyLineStarted?.Invoke(_stylusHandler.CurrentState.inkingPose.position);
    }

    private void TriggerHaptics()
    {
        if (_stylusHandler is not VrStylusHandler vrStylusHandler)
        {
            return;
        }

        const float dampingFactor = 0.6f;
        const float duration = 0.01f;
        float middleButtonPressure = _stylusHandler.CurrentState.cluster_middle_value * dampingFactor;
        vrStylusHandler.TriggerHapticPulse(middleButtonPressure, duration);
    }

    private void AddPoint(Vector3 position, float width)
    {
        if (Vector3.Distance(position, _previousLinePoint) <= MinDistanceBetweenLinePoints)
        {
            return;
        }

        TriggerHaptics();
        _previousLinePoint = position;
        _currentLine.positionCount++;
        _currentLineWidths.Add(Math.Max(width * _maxLineWidth, _minLineWidth));
        _currentLine.SetPosition(_currentLine.positionCount - 1, position);

        AnimationCurve curve = new();
        if (_currentLineWidths.Count > 1)
        {
            for (int i = 0; i < _currentLineWidths.Count; i++)
            {
                curve.AddKey(i / (float)(_currentLineWidths.Count - 1), _currentLineWidths[i]);
            }
        }
        else
        {
            curve.AddKey(0f, _currentLineWidths[0]);
        }

        _currentLine.widthCurve = curve;
    }

    private void RemoveLastLine()
    {
        GameObject lastLine = _lines[_lines.Count - 1];
        _lines.RemoveAt(_lines.Count - 1);
        Destroy(lastLine);
    }

    private void ClearAllLines()
    {
        foreach (GameObject line in _lines)
        {
            Destroy(line);
        }

        _lines.Clear();
    }

    private void Update()
    {
        if (_stylusHandler == null)
        {
            return;
        }

        float analogInput = Mathf.Max(_stylusHandler.CurrentState.tip_value, _stylusHandler.CurrentState.cluster_middle_value);
        if (analogInput > 0f && _stylusHandler.CanDraw())
        {
            if (!_isDrawing)
            {
                StartNewLine();
                _isDrawing = true;
            }

            AddPoint(_stylusHandler.CurrentState.inkingPose.position, _lineWidthIsFixed ? 1.0f : analogInput);
        }
        else
        {
            _isDrawing = false;
        }

        if (_stylusHandler.CurrentState.cluster_back_double_tap_value || _stylusHandler.CurrentState.cluster_back_value)
        {
            if (_lines.Count > 0 && !_doubleTapDetected)
            {
                _buttonPressedTimestamp = Time.time;
                RemoveLastLine();
            }

            _doubleTapDetected = true;
            if (_lines.Count > 0 && Time.time >= (_buttonPressedTimestamp + longPressDuration))
            {
                ClearAllLines();
            }
        }
        else
        {
            _doubleTapDetected = false;
        }
    }
}
