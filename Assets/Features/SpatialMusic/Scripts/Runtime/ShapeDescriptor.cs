using UnityEngine;

public readonly struct ShapeDescriptor
{
    public readonly ShapeClassification Classification;
    public readonly Vector3 Center;
    public readonly Vector3 PlaneNormal;
    public readonly float PathLength;
    public readonly float ClosureRatio;
    public readonly float Directness;
    public readonly float Jaggedness;
    public readonly int DirectionChanges;
    public readonly float RotationDegrees;
    public readonly float AveragePressure;
    public readonly Vector2 Bounds2D;

    public ShapeDescriptor(
        ShapeClassification classification,
        Vector3 center,
        Vector3 planeNormal,
        float pathLength,
        float closureRatio,
        float directness,
        float jaggedness,
        int directionChanges,
        float rotationDegrees,
        float averagePressure,
        Vector2 bounds2D)
    {
        Classification = classification;
        Center = center;
        PlaneNormal = planeNormal;
        PathLength = pathLength;
        ClosureRatio = closureRatio;
        Directness = directness;
        Jaggedness = jaggedness;
        DirectionChanges = directionChanges;
        RotationDegrees = rotationDegrees;
        AveragePressure = averagePressure;
        Bounds2D = bounds2D;
    }

    public float Size01 => Mathf.Clamp01(Mathf.Max(Bounds2D.x, Bounds2D.y) / 2.2f);
    public float Height01 => Mathf.Clamp01((Center.y + 0.25f) / 2.5f);
}
