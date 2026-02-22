using System.Collections.Generic;
using UnityEngine;

public class LineStrokeData
{
    public readonly List<Vector3> Points;
    public readonly List<float> Pressures;
    public readonly List<float> Tilts;
    public readonly float TotalLength;
    public readonly Vector3 Centroid;
    public readonly float AveragePressure;
    public readonly float AverageTilt;

    public int Count => Points.Count;

    public LineStrokeData(List<Vector3> points, List<float> pressures, List<float> tilts, float totalLength)
    {
        Points = new List<Vector3>(points);
        Pressures = new List<float>(pressures);
        Tilts = new List<float>(tilts);
        TotalLength = totalLength;

        int count = Points.Count;
        Vector3 centroid = Vector3.zero;
        float pressureSum = 0f;
        float tiltSum = 0f;

        for (int i = 0; i < count; i++)
        {
            centroid += Points[i];
            if (i < Pressures.Count)
            {
                pressureSum += Pressures[i];
            }
            if (i < Tilts.Count)
            {
                tiltSum += Tilts[i];
            }
        }

        if (count > 0)
        {
            Centroid = centroid / count;
            AveragePressure = pressureSum / count;
            AverageTilt = tiltSum / count;
        }
        else
        {
            Centroid = Vector3.zero;
            AveragePressure = 0f;
            AverageTilt = 0f;
        }
    }
}
