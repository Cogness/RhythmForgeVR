using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Raw float buffers from a background synthesis pass.
    /// AudioClip.Create/SetData must be called on the main thread with these.
    /// </summary>
    public sealed class RawSamples
    {
        public string name;
        public float[] left;
        public float[] right;
    }

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

        /// <summary>
        /// Render raw float buffers only — no AudioClip created, safe to call from background threads.
        /// Caller must invoke SynthUtilities.BuildClip on the main thread to finalise.
        /// </summary>
        public static RawSamples RenderRaw(ResolvedVoiceSpec spec)
        {
            // IVoiceRenderer.Render returns an AudioClip which requires Unity API.
            // We bypass that and call the internal synthesizers directly for their float arrays.
            if (spec.patternType == PatternType.RhythmLoop)
                return DrumSynthesizer.RenderRaw(spec);
            return TonalSynthesizer.RenderRaw(spec);
        }
    }
}
