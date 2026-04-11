using UnityEngine;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Generates AudioClips entirely from math at runtime.
    /// No .wav files required. All sounds are synthesized on first Play.
    /// </summary>
    public static class ProceduralSynthesizer
    {
        private const int SampleRate = 44100;

        // ───────────────────────────── DRUMS ─────────────────────────────

        /// <summary>
        /// Sine sweep 80→28 Hz with exponential amplitude decay.
        /// Optional drive (soft-clip) for character.
        /// </summary>
        public static AudioClip GenerateKick(float duration = 0.32f, float drive = 1.4f)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;
                float decay = Mathf.Exp(-progress * 10f);

                // Frequency sweep: 80 Hz → 28 Hz
                float freq = Mathf.Lerp(80f, 28f, 1f - Mathf.Exp(-progress * 9f));
                float phase = 2f * Mathf.PI * freq * t;

                float s = Mathf.Sin(phase) * decay;

                // Click transient: extra high-freq component for first 8ms
                if (t < 0.008f)
                {
                    float clickDecay = Mathf.Exp(-t * 700f);
                    s += Mathf.Sin(2f * Mathf.PI * 340f * t) * clickDecay * 0.5f;
                }

                // Soft clip / drive
                s = SoftClip(s * drive);
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }

            return BuildClip("Kick", data);
        }

        /// <summary>
        /// Short sine body + white noise tail, shaped like a snare.
        /// </summary>
        public static AudioClip GenerateSnare(float duration = 0.22f)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                // Tonal body: 200 Hz sine, fast decay
                float bodyDecay = Mathf.Exp(-progress * 32f);
                float body = Mathf.Sin(2f * Mathf.PI * 200f * t) * bodyDecay * 0.6f;

                // Noise tail: white noise through envelope
                float noiseDecay = Mathf.Exp(-progress * 14f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * noiseDecay;

                // Click transient
                float click = 0f;
                if (t < 0.005f)
                    click = Mathf.Sin(2f * Mathf.PI * 600f * t) * Mathf.Exp(-t * 1200f) * 0.8f;

                data[i] = Mathf.Clamp(body + noise * 0.7f + click, -1f, 1f);
            }

            return BuildClip("Snare", data);
        }

        /// <summary>
        /// Highpass-filtered white noise with very fast decay — classic closed hi-hat.
        /// </summary>
        public static AudioClip GenerateHat(float duration = 0.08f)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(7);

            // Simple one-pole highpass state
            float prev = 0f;
            float alpha = 0.93f; // ~3 kHz cutoff at 44100

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float decay = Mathf.Exp(-t / duration * 6f);

                float noise = (float)rng.NextDouble() * 2f - 1f;
                float hp = noise - prev * alpha;
                prev = noise;

                // Add some metallic shimmer (inharmonic partials)
                float shimmer = Mathf.Sin(2f * Mathf.PI * 8372f * t) * 0.15f
                              + Mathf.Sin(2f * Mathf.PI * 10548f * t) * 0.1f;

                data[i] = Mathf.Clamp((hp * 0.7f + shimmer) * decay, -1f, 1f);
            }

            return BuildClip("Hat", data);
        }

        /// <summary>
        /// Short pitched tone with fast attack/decay — works as tom/conga/perc.
        /// </summary>
        public static AudioClip GeneratePerc(float freq = 380f, float duration = 0.12f)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random(13);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                float decay = Mathf.Exp(-progress * 18f);
                float freqSweep = freq * (1f + Mathf.Exp(-progress * 22f) * 1.2f);

                float tone = Mathf.Sin(2f * Mathf.PI * freqSweep * t) * decay;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-progress * 40f) * 0.18f;

                data[i] = Mathf.Clamp(tone + noise, -1f, 1f);
            }

            return BuildClip("Perc", data);
        }

        // ───────────────────────────── TONAL ─────────────────────────────

        /// <summary>
        /// Sustained sine tone at the given frequency with gentle attack and release.
        /// Use as a C4 reference for pitch-shifting melody/harmony notes.
        /// </summary>
        public static AudioClip GenerateTone(float freq = 261.63f, float duration = 2.0f)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];

            float attackTime = 0.012f;
            float releaseTime = 0.25f;
            float attackSamples = attackTime * SampleRate;
            float releaseSamples = releaseTime * SampleRate;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                // Amplitude envelope
                float env;
                if (i < attackSamples)
                    env = i / attackSamples;
                else if (i > samples - releaseSamples)
                    env = (samples - i) / releaseSamples;
                else
                    env = 1f;

                // Fundamental + subtle 2nd harmonic for warmth
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.8f
                        + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.12f
                        + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.04f;

                data[i] = Mathf.Clamp(s * env * 0.7f, -1f, 1f);
            }

            return BuildClip("ToneC4", data);
        }

        /// <summary>
        /// Pad-like tone with slow attack — use as harmony voice.
        /// </summary>
        public static AudioClip GeneratePad(float freq = 261.63f, float duration = 3.0f)
        {
            int samples = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[samples];

            float attackTime = 0.32f;
            float releaseTime = 0.6f;
            float attackSamples = attackTime * SampleRate;
            float releaseSamples = releaseTime * SampleRate;

            var rng = new System.Random(99);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                float env;
                if (i < attackSamples)
                    env = i / attackSamples;
                else if (i > samples - releaseSamples)
                    env = (samples - i) / releaseSamples;
                else
                    env = 1f;

                // Slightly detuned dual oscillator for thickness
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * freq * 1.0035f * t) * 0.4f
                        + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.08f;

                // Slow filter breathe via LFO on volume
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.3f * t);
                env *= 0.8f + lfo * 0.2f;

                data[i] = Mathf.Clamp(s * env * 0.65f, -1f, 1f);
            }

            return BuildClip("Pad", data);
        }

        // ───────────────────────────── HELPERS ─────────────────────────────

        private static float SoftClip(float x)
        {
            if (x > 1f) return 1f - Mathf.Exp(-(x - 1f));
            if (x < -1f) return -1f + Mathf.Exp((x + 1f));
            return x;
        }

        private static AudioClip BuildClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
