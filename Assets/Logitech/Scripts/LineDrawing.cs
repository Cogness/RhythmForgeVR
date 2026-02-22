using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LineDrawing : MonoBehaviour
{
    private List<GameObject> _lines = new List<GameObject>();
    private List<LoopSound> _loopSounds = new List<LoopSound>();
    private LineRenderer _currentLine;
    private List<float> _currentLineWidths = new List<float>(); //list to store line widths
    private List<Vector3> _currentLinePoints = new List<Vector3>();
    private List<float> _currentLinePressures = new List<float>();
    private List<float> _currentLineTilts = new List<float>();
    private float _currentLineLength = 0f;

    [SerializeField] float _maxLineWidth = 0.01f;
    [SerializeField] float _minLineWidth = 0.0005f;

    [SerializeField] Material _material;

    [SerializeField] private Color _currentColor;
    public Color CurrentColor
    {
        get { return _currentColor; }
        set
        {
            _currentColor = value;
        }
    }

    public float MaxLineWidth
    {
        get { return _maxLineWidth; }
        set { _maxLineWidth = value; }
    }

    private bool _lineWidthIsFixed = false;
    public bool LineWidthIsFixed
    {
        get { return _lineWidthIsFixed; }
        set { _lineWidthIsFixed = value; }
    }

    [Header("Loop Audio")]
    [SerializeField] private bool _enableAudioLoops = true;
    [SerializeField] private float _closeLoopDistance = 0.02f;
    [SerializeField] private int _minLoopPoints = 12;
    [SerializeField] private float _detailAttachDistance = 0.05f;
    [SerializeField] private LoopSoundSettings _loopSoundSettings = LoopSoundSettings.CreateDefault();

    [Header("Tilt Twist")]
    [SerializeField] private bool _applyTwistOffset = true;
    [SerializeField] private float _twistOffsetStrength = 0.003f;
    [SerializeField] private Transform _tiltReference;
    [SerializeField] private Vector3 _penAxis = Vector3.forward;

    private bool _isDrawing = false;
    private bool _doubleTapDetected = false;

    [SerializeField]
    private float longPressDuration = 1.0f;
    private float buttonPressedTimestamp = 0;

    [SerializeField]
    private StylusHandler _stylusHandler;
    [SerializeField]
    private StylusHandler _fallbackHandler;
    [SerializeField]
    private bool _autoCreateOculusFallback = true;

    private Vector3 _previousLinePoint;
    private const float _minDistanceBetweenLinePoints = 0.0005f;

    private void Awake()
    {
        _loopSoundSettings = LoopSoundSettings.Sanitize(_loopSoundSettings);
        if (_closeLoopDistance <= 0f)
        {
            _closeLoopDistance = 0.02f;
        }
        if (_minLoopPoints < 4)
        {
            _minLoopPoints = 4;
        }
        if (_detailAttachDistance <= 0f)
        {
            _detailAttachDistance = 0.05f;
        }
        if (_twistOffsetStrength < 0f)
        {
            _twistOffsetStrength = 0.003f;
        }
        if (_penAxis == Vector3.zero)
        {
            _penAxis = Vector3.forward;
        }

        if (_fallbackHandler == null && _autoCreateOculusFallback)
        {
            OculusControllerStylusHandler existing = GetComponent<OculusControllerStylusHandler>();
            if (existing == null)
            {
                existing = gameObject.AddComponent<OculusControllerStylusHandler>();
            }
            _fallbackHandler = existing;
        }
    }

    private void StartNewLine()
    {
        var gameObject = new GameObject("line");
        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
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
        _lines.Add(gameObject);
        _previousLinePoint = Vector3.positiveInfinity;
        _currentLinePoints = new List<Vector3>();
        _currentLinePressures = new List<float>();
        _currentLineTilts = new List<float>();
        _currentLineLength = 0f;
    }

    private void TriggerHaptics(StylusHandler handler)
    {
        const float dampingFactor = 0.6f;
        const float duration = 0.01f;
        if (handler == null)
        {
            return;
        }
        float middleButtonPressure = handler.CurrentState.cluster_middle_value * dampingFactor;
        if (handler is VrStylusHandler vrStylus)
        {
            vrStylus.TriggerHapticPulse(middleButtonPressure, duration);
        }
    }

    private float GetTiltNormalized(StylusHandler handler)
    {
        if (handler == null)
        {
            return 0f;
        }
        Vector3 penDirection = handler.transform.TransformDirection(_penAxis);
        if (penDirection.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }
        Vector3 referenceNormal = _tiltReference != null ? _tiltReference.up : Vector3.up;
        if (referenceNormal.sqrMagnitude <= 0.0001f)
        {
            referenceNormal = Vector3.up;
        }
        penDirection.Normalize();
        referenceNormal.Normalize();
        float dot = Mathf.Clamp01(Mathf.Abs(Vector3.Dot(penDirection, referenceNormal)));
        return 1f - dot;
    }

    private Vector3 ApplyTwistOffset(Vector3 position, Vector3 direction, float tiltNormalized)
    {
        if (!_applyTwistOffset || _twistOffsetStrength <= 0f)
        {
            return position;
        }
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return position;
        }
        Vector3 referenceNormal = _tiltReference != null ? _tiltReference.up : Vector3.up;
        if (referenceNormal.sqrMagnitude <= 0.0001f)
        {
            referenceNormal = Vector3.up;
        }
        Vector3 right = Vector3.Cross(direction.normalized, referenceNormal.normalized);
        if (right.sqrMagnitude <= 0.0001f)
        {
            return position;
        }
        right.Normalize();
        float offset = (tiltNormalized - 0.5f) * 2f * _twistOffsetStrength;
        return position + right * offset;
    }

    private void AddPoint(StylusHandler handler, Vector3 position, float pressureNormalized, float tiltNormalized)
    {
        bool isFirstPoint = _currentLine.positionCount == 0;
        if (isFirstPoint || Vector3.Distance(position, _previousLinePoint) > _minDistanceBetweenLinePoints)
        {
            TriggerHaptics(handler);
            Vector3 direction = isFirstPoint ? Vector3.zero : (position - _previousLinePoint).normalized;
            Vector3 finalPosition = ApplyTwistOffset(position, direction, tiltNormalized);
            float segmentLength = isFirstPoint ? 0f : Vector3.Distance(_previousLinePoint, finalPosition);
            _previousLinePoint = finalPosition;
            _currentLine.positionCount++;
            _currentLineWidths.Add(Math.Max(pressureNormalized * _maxLineWidth, _minLineWidth));
            _currentLine.SetPosition(_currentLine.positionCount - 1, finalPosition);
            _currentLinePoints.Add(finalPosition);
            _currentLinePressures.Add(pressureNormalized);
            _currentLineTilts.Add(tiltNormalized);
            _currentLineLength += segmentLength;

            //create a new AnimationCurve
            AnimationCurve curve = new AnimationCurve();

            //populate the curve with keyframes based on the widths list
            if (_currentLineWidths.Count > 1)
            {
                for (int i = 0; i < _currentLineWidths.Count; i++)
                {
                    curve.AddKey(i / (float)(_currentLineWidths.Count - 1), _currentLineWidths[i]);
                }
            }
            else
            {
                curve.AddKey(0, _currentLineWidths[0]);
            }

            //assign the curve to the widthCurve
            _currentLine.widthCurve = curve;
        }
    }

    private void FinalizeLine()
    {
        if (_currentLine == null || _currentLinePoints.Count < 2)
        {
            return;
        }

        bool isLoop = _currentLinePoints.Count >= _minLoopPoints &&
                      Vector3.Distance(_currentLinePoints[0], _currentLinePoints[_currentLinePoints.Count - 1]) <= _closeLoopDistance;

        float totalLength = _currentLineLength;
        if (isLoop)
        {
            _currentLine.loop = true;
            totalLength += Vector3.Distance(_currentLinePoints[_currentLinePoints.Count - 1], _currentLinePoints[0]);
        }

        LineStrokeData strokeData = new LineStrokeData(_currentLinePoints, _currentLinePressures, _currentLineTilts, totalLength);

        if (_enableAudioLoops)
        {
            if (isLoop)
            {
                LoopSound loopSound = _currentLine.gameObject.AddComponent<LoopSound>();
                loopSound.Initialize(strokeData, _loopSoundSettings);
                _loopSounds.Add(loopSound);
            }
            else
            {
                LoopSound closestLoop = FindClosestLoop(strokeData);
                if (closestLoop != null)
                {
                    List<float> addedSpikes = closestLoop.AddDetailStroke(strokeData);
                    if (addedSpikes.Count > 0)
                    {
                        LoopDetailStroke detailStroke = _currentLine.gameObject.AddComponent<LoopDetailStroke>();
                        detailStroke.Initialize(closestLoop, addedSpikes);
                    }
                }
            }
        }
    }

    private LoopSound FindClosestLoop(LineStrokeData strokeData)
    {
        LoopSound closest = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < _loopSounds.Count; i++)
        {
            LoopSound loop = _loopSounds[i];
            if (loop == null)
            {
                continue;
            }
            float distanceToCenter = Vector3.Distance(strokeData.Centroid, loop.Center);
            float attachRange = loop.AverageRadius + _detailAttachDistance;
            if (distanceToCenter <= attachRange && distanceToCenter < closestDistance)
            {
                closest = loop;
                closestDistance = distanceToCenter;
            }
        }
        return closest;
    }

    private void RemoveLastLine()
    {
        if (_lines.Count == 0)
        {
            return;
        }
        GameObject lastLine = _lines[_lines.Count - 1];
        _lines.RemoveAt(_lines.Count - 1);

        if (lastLine != null)
        {
            LoopSound loopSound = lastLine.GetComponent<LoopSound>();
            if (loopSound != null)
            {
                _loopSounds.Remove(loopSound);
            }
        }
        Destroy(lastLine);
    }

    private void ClearAllLines()
    {
        foreach (var line in _lines)
        {
            Destroy(line);
        }
        _lines.Clear();
        _loopSounds.Clear();
    }

    void Update()
    {
        StylusHandler activeHandler = ResolveActiveHandler();
        if (activeHandler == null)
        {
            return;
        }

        float analogInput = Mathf.Max(activeHandler.CurrentState.tip_value, activeHandler.CurrentState.cluster_middle_value);
        float pressure = Mathf.Clamp01(analogInput);
        float tiltNormalized = GetTiltNormalized(activeHandler);

        if (pressure > 0 && activeHandler.CanDraw())
        {
            if (!_isDrawing)
            {
                StartNewLine();
                _isDrawing = true;
            }
            AddPoint(activeHandler, activeHandler.CurrentState.inkingPose.position, _lineWidthIsFixed ? 1.0f : pressure, tiltNormalized);
        }
        else
        {
            if (_isDrawing)
            {
                FinalizeLine();
            }
            _isDrawing = false;
        }

        //Undo by double tapping or clicking on cluster_back button on stylus
        if (activeHandler.CurrentState.cluster_back_double_tap_value ||
        activeHandler.CurrentState.cluster_back_value)
        {
            if (_lines.Count > 0 && !_doubleTapDetected)
            {
                buttonPressedTimestamp = Time.time;
                RemoveLastLine();
            }
            _doubleTapDetected = true;
            if (_lines.Count > 0 && Time.time >= (buttonPressedTimestamp + longPressDuration))
            {
                ClearAllLines();
            }
        }
        else
        {
            _doubleTapDetected = false;
        }
    }

    private StylusHandler ResolveActiveHandler()
    {
        if (IsHandlerActive(_stylusHandler))
        {
            return _stylusHandler;
        }
        if (IsHandlerActive(_fallbackHandler))
        {
            return _fallbackHandler;
        }
        if (_stylusHandler != null)
        {
            return _stylusHandler;
        }
        return _fallbackHandler;
    }

    private bool IsHandlerActive(StylusHandler handler)
    {
        return handler != null && handler.CurrentState.isActive;
    }
}
