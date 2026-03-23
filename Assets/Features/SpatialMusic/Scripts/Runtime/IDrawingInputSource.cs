using UnityEngine;

public interface IDrawingInputSource
{
    string SourceName { get; }
    int Priority { get; }
    bool WantsControl { get; }
    bool DrawStartedThisFrame { get; }
    bool IsDrawing { get; }
    bool DrawEndedThisFrame { get; }
    bool ClearLastRequestedThisFrame { get; }
    bool ClearAllRequestedThisFrame { get; }
    DrawingInputState State { get; }
    void Tick();
}
