using UnityEngine;

public readonly struct DrawingInputState
{
    public readonly Pose WorldPose;
    public readonly float Pressure01;
    public readonly bool HasValidPose;

    public DrawingInputState(Pose worldPose, float pressure01, bool hasValidPose)
    {
        WorldPose = worldPose;
        Pressure01 = pressure01;
        HasValidPose = hasValidPose;
    }
}
