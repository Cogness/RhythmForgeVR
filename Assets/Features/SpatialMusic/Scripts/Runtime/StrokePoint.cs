using UnityEngine;

public readonly struct StrokePoint
{
    public readonly Vector3 Position;
    public readonly float Pressure01;
    public readonly float TimeSeconds;

    public StrokePoint(Vector3 position, float pressure01, float timeSeconds)
    {
        Position = position;
        Pressure01 = pressure01;
        TimeSeconds = timeSeconds;
    }
}
