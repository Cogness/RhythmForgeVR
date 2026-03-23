using System.Collections.Generic;
using UnityEngine;

public static class ShapeAnalyzer
{
    public static ShapeDescriptor Analyze(StrokeData stroke)
    {
        Vector3 center = stroke.GetCentroid();
        Vector3 planeNormal = EstimatePlaneNormal(stroke, center);
        BuildPlaneBasis(stroke, center, planeNormal, out Vector3 axisX, out Vector3 axisY);

        List<Vector2> projected = new(stroke.PointCount);
        for (int i = 0; i < stroke.PointCount; i++)
        {
            Vector3 offset = stroke.Points[i].Position - center;
            projected.Add(new Vector2(Vector3.Dot(offset, axisX), Vector3.Dot(offset, axisY)));
        }

        Vector2 min = projected[0];
        Vector2 max = projected[0];
        float directDistance = Vector3.Distance(stroke.StartPosition, stroke.EndPosition);
        float closureRatio = stroke.TotalLength > 0.001f ? directDistance / stroke.TotalLength : 1f;
        float directness = closureRatio;
        float totalAbsTurn = 0f;
        float totalSignedTurn = 0f;
        int directionChanges = 0;
        Vector2 previousDirection = Vector2.zero;
        bool hasPreviousDirection = false;

        for (int i = 0; i < projected.Count; i++)
        {
            min = Vector2.Min(min, projected[i]);
            max = Vector2.Max(max, projected[i]);

            if (i == 0)
            {
                continue;
            }

            Vector2 segment = projected[i] - projected[i - 1];
            if (segment.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            Vector2 currentDirection = segment.normalized;
            if (hasPreviousDirection)
            {
                float signed = Vector2.SignedAngle(previousDirection, currentDirection);
                float abs = Mathf.Abs(signed);
                totalSignedTurn += signed;
                totalAbsTurn += abs;
                if (abs >= 38f)
                {
                    directionChanges++;
                }
            }

            previousDirection = currentDirection;
            hasPreviousDirection = true;
        }

        Vector2 bounds = max - min;
        float averagePressure = stroke.GetAveragePressure();
        float jaggedness = Mathf.Clamp01(totalAbsTurn / Mathf.Max(1f, (projected.Count - 2) * 90f));
        bool closed = closureRatio <= 0.22f && stroke.PointCount >= 8;
        float aspect = Mathf.Min(bounds.x, bounds.y) / Mathf.Max(0.001f, Mathf.Max(bounds.x, bounds.y));
        float radialVariance = CalculateRadialVariance(projected);

        ShapeClassification classification;
        if (closed && aspect >= 0.55f && radialVariance <= 0.22f && Mathf.Abs(totalSignedTurn) >= 210f)
        {
            classification = ShapeClassification.CircleLoop;
        }
        else if (!closed && directionChanges >= 5)
        {
            classification = ShapeClassification.ZigZag;
        }
        else if (!closed && directness >= 0.83f)
        {
            classification = ShapeClassification.Line;
        }
        else if (Mathf.Abs(totalSignedTurn) >= 90f)
        {
            classification = ShapeClassification.Arc;
        }
        else
        {
            classification = ShapeClassification.Line;
        }

        return new ShapeDescriptor(
            classification,
            center,
            planeNormal,
            stroke.TotalLength,
            closureRatio,
            directness,
            jaggedness,
            directionChanges,
            totalSignedTurn,
            averagePressure,
            bounds);
    }

    private static Vector3 EstimatePlaneNormal(StrokeData stroke, Vector3 center)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < stroke.PointCount - 1; i++)
        {
            Vector3 a = stroke.Points[i].Position - center;
            Vector3 b = stroke.Points[i + 1].Position - center;
            sum += Vector3.Cross(a, b);
        }

        if (sum.sqrMagnitude < 0.0001f)
        {
            return Vector3.up;
        }

        return sum.normalized;
    }

    private static void BuildPlaneBasis(StrokeData stroke, Vector3 center, Vector3 normal, out Vector3 axisX, out Vector3 axisY)
    {
        axisX = stroke.EndPosition - stroke.StartPosition;
        if (axisX.sqrMagnitude < 0.0001f)
        {
            axisX = stroke.Points[0].Position - center;
        }

        if (axisX.sqrMagnitude < 0.0001f)
        {
            axisX = Vector3.Cross(normal, Vector3.up);
        }

        if (axisX.sqrMagnitude < 0.0001f)
        {
            axisX = Vector3.right;
        }

        axisX = Vector3.ProjectOnPlane(axisX, normal).normalized;
        axisY = Vector3.Cross(normal, axisX).normalized;
    }

    private static float CalculateRadialVariance(List<Vector2> points)
    {
        float radiusSum = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            radiusSum += points[i].magnitude;
        }

        float averageRadius = radiusSum / Mathf.Max(1, points.Count);
        if (averageRadius <= 0.0001f)
        {
            return 1f;
        }

        float variance = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            float delta = points[i].magnitude - averageRadius;
            variance += delta * delta;
        }

        return Mathf.Sqrt(variance / Mathf.Max(1, points.Count)) / averageRadius;
    }
}
