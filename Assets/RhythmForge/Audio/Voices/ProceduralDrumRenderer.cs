using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    internal sealed class ProceduralDrumRenderer : IVoiceRenderer
    {
        public string VoiceId => "procedural-drum";

        public bool CanRender(ResolvedVoiceSpec spec)
        {
            return spec.patternType == PatternType.RhythmLoop;
        }

        public AudioClip Render(ResolvedVoiceSpec spec)
        {
            return DrumSynthesizer.Render(spec);
        }
    }
}
