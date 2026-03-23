using System.Collections.Generic;
using UnityEngine;

public sealed class StrokeData
{
    public StrokeData(List<StrokePoint> points, float totalLength)
    {
        Points = points;
        TotalLength = totalLength;
    }

    public List<StrokePoint> Points { get; }
    public float TotalLength { get; }
    public int PointCount => Points.Count;
    public Vector3 StartPosition => Points[0].Position;
    public Vector3 EndPosition => Points[Points.Count - 1].Position;

    public Vector3 GetCentroid()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < Points.Count; i++)
        {
            sum += Points[i].Position;
        }

        return sum / Mathf.Max(1, Points.Count);
    }

    public float GetAveragePressure()
    {
        float sum = 0f;
        for (int i = 0; i < Points.Count; i++)
        {
            sum += Points[i].Pressure01;
        }

        return sum / Mathf.Max(1, Points.Count);
    }
}
