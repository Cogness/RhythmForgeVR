using UnityEngine;

public interface IMusicalShapeVoice
{
    float LoopDurationSeconds { get; }
    MusicalLoopDefinition Definition { get; }
    void Initialize(MusicalLoopDefinition definition, AudioSource audioSource);
    void Stop();
}
