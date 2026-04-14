using UnityEngine;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Backwards-compatible facade over the extracted synthesis pipeline.
    /// </summary>
    public static class ProceduralSynthesizer
    {
        public const int SampleRate = 44100;

        public static AudioClip GenerateKick(float duration = 0.32f, float drive = 1.4f)
            => DrumSynthesizer.GenerateKick(duration, drive);

        public static AudioClip GenerateSnare(float duration = 0.22f)
            => DrumSynthesizer.GenerateSnare(duration);

        public static AudioClip GenerateHat(float duration = 0.08f)
            => DrumSynthesizer.GenerateHat(duration);

        public static AudioClip GeneratePerc(float freq = 380f, float duration = 0.12f)
            => TonalSynthesizer.GeneratePerc(freq, duration);

        public static AudioClip GenerateTone(float freq = 261.63f, float duration = 2.0f)
            => TonalSynthesizer.GenerateTone(freq, duration);

        public static AudioClip GeneratePad(float freq = 261.63f, float duration = 3.0f)
            => TonalSynthesizer.GeneratePad(freq, duration);

        public static AudioClip RenderDrum(ResolvedVoiceSpec spec)
            => DrumSynthesizer.Render(spec);

        public static AudioClip RenderTone(ResolvedVoiceSpec spec)
            => TonalSynthesizer.Render(spec);
    }
}
