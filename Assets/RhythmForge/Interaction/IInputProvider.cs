using UnityEngine;

namespace RhythmForge.Interaction
{
    public interface IInputProvider
    {
        float DrawPressure { get; }
        bool IsDrawing { get; }
        bool FrontButtonDown { get; }
        bool BackButtonDown { get; }
        bool BackDoubleTap { get; }
        Pose StylusPose { get; }
        Vector2 LeftThumbstick { get; }
        bool ButtonTwo { get; }
        bool FrontButtonConsumed { get; set; }
        bool BackButton { get; }
        bool IsStylusActive { get; }
        bool LeftTrigger { get; }
        bool LeftTriggerDown { get; }
        bool LeftTriggerUp { get; }
        bool LeftGrip { get; }
    }
}
