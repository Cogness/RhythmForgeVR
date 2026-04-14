using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    internal sealed class ProceduralTonalRenderer : IVoiceRenderer
    {
        public string VoiceId => "procedural-tonal";

        public bool CanRender(ResolvedVoiceSpec spec)
        {
            return spec.patternType != PatternType.RhythmLoop;
        }

        public AudioClip Render(ResolvedVoiceSpec spec)
        {
            return TonalSynthesizer.Render(spec);
        }
    }
}
