using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    public static class VoiceRendererRegistry
    {
        private static readonly List<IVoiceRenderer> _renderers = new List<IVoiceRenderer>();

        static VoiceRendererRegistry()
        {
            Register(new ProceduralDrumRenderer());
            Register(new ProceduralTonalRenderer());
        }

        public static void Register(IVoiceRenderer renderer)
        {
            if (renderer == null)
                return;

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i].VoiceId == renderer.VoiceId)
                {
                    _renderers[i] = renderer;
                    return;
                }
            }

            _renderers.Add(renderer);
        }

        public static AudioClip Render(ResolvedVoiceSpec spec)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer.CanRender(spec))
                    return renderer.Render(spec);
            }

            return spec.patternType == PatternType.RhythmLoop
                ? ProceduralSynthesizer.RenderDrum(spec)
                : ProceduralSynthesizer.RenderTone(spec);
        }
    }
}
