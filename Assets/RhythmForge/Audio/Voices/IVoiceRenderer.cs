using UnityEngine;

namespace RhythmForge.Audio
{
    public interface IVoiceRenderer
    {
        string VoiceId { get; }
        bool CanRender(ResolvedVoiceSpec spec);
        AudioClip Render(ResolvedVoiceSpec spec);
    }
}
