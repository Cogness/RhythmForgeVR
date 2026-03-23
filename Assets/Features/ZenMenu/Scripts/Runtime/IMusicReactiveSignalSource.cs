using System;

public interface IMusicReactiveSignalSource
{
    float Loudness01 { get; }
    bool HasReceivedPlayerMusic { get; }
    event Action<float> BeatDetected;
    event Action PlayerMusicStarted;
}
