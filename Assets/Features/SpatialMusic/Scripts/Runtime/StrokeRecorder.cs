using System.Collections.Generic;
using UnityEngine;

public sealed class StrokeRecorder
{
    private readonly List<StrokePoint> _points;
    private readonly float _minimumPointDistance;
    private readonly int _maxPoints;
    private float _totalLength;
    private bool _isRecording;

    public StrokeRecorder(int initialCapacity = 256, float minimumPointDistance = 0.0125f, int maxPoints = 512)
    {
        _points = new List<StrokePoint>(initialCapacity);
        _minimumPointDistance = minimumPointDistance;
        _maxPoints = maxPoints;
    }

    public bool IsRecording => _isRecording;
    public IReadOnlyList<StrokePoint> CurrentPoints => _points;

    public void Begin(Vector3 position, float pressure01, float timeSeconds)
    {
        _points.Clear();
        _totalLength = 0f;
        _isRecording = true;
        _points.Add(new StrokePoint(position, pressure01, timeSeconds));
    }

    public bool Append(Vector3 position, float pressure01, float timeSeconds)
    {
        if (!_isRecording || _points.Count >= _maxPoints)
        {
            return false;
        }

        StrokePoint lastPoint = _points[_points.Count - 1];
        float distance = Vector3.Distance(lastPoint.Position, position);
        if (distance < _minimumPointDistance)
        {
            return false;
        }

        _totalLength += distance;
        _points.Add(new StrokePoint(position, pressure01, timeSeconds));
        return true;
    }

    public StrokeData End()
    {
        _isRecording = false;
        return new StrokeData(new List<StrokePoint>(_points), _totalLength);
    }

    public void Cancel()
    {
        _points.Clear();
        _totalLength = 0f;
        _isRecording = false;
    }
}
