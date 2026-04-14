using System;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    internal static class SynthUtilities
    {
        public const int SampleRate = ProceduralSynthesizer.SampleRate;

        public static float EnvelopeAtTime(ResolvedVoiceSpec spec, float time, float releaseStart)
        {
            if (time < 0f)
                return 0f;

            if (time < spec.attackSeconds)
            {
                // Exponential-curve attack: fast rise with smooth shoulder (sounds more natural than linear)
                float progress = Mathf.Clamp01(time / Mathf.Max(0.001f, spec.attackSeconds));
                return 1f - Mathf.Exp(-5f * progress);
            }

            if (time <= releaseStart)
                return 1f;

            float releaseTime = time - releaseStart;
            return Mathf.Exp(-releaseTime / Mathf.Max(0.04f, spec.releaseSeconds));
        }

        public static float EnvelopeDecay(float time, float decaySeconds)
        {
            return Mathf.Exp(-time / Mathf.Max(0.01f, decaySeconds));
        }

        public static float ProcessFilter(ref SvfState state, float input, float cutoffHz, float resonance, VoiceFilterMode mode)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(cutoffHz, 40f, SampleRate * 0.45f) / SampleRate);
            float q = Mathf.Lerp(1.35f, 0.15f, Mathf.Clamp01(resonance));

            state.low += f * state.band;
            float high = input - state.low - q * state.band;
            state.band += f * high;

            switch (mode)
            {
                case VoiceFilterMode.HighPass:
                    return high;
                case VoiceFilterMode.BandPass:
                    return state.band;
                default:
                    return state.low;
            }
        }

        public static float SampleWave(VoiceWaveform waveform, float phase)
        {
            return SampleWaveWithDt(waveform, phase, 0f);
        }

        // Band-limited waveform with PolyBLEP correction (pass dt = frequency/SampleRate).
        public static float SampleWaveWithDt(VoiceWaveform waveform, float phase, float dt)
        {
            phase -= Mathf.Floor(phase);

            switch (waveform)
            {
                case VoiceWaveform.Triangle:
                    // Triangle has no hard discontinuity — integrated square is already band-limited enough.
                    return 1f - 4f * Mathf.Abs(phase - 0.5f);
                case VoiceWaveform.Square:
                {
                    float s = phase < 0.5f ? 1f : -1f;
                    if (dt > 0f)
                    {
                        s += PolyBlep(phase, dt);
                        s -= PolyBlep((phase + 0.5f) % 1f, dt);
                    }
                    return s;
                }
                case VoiceWaveform.Sawtooth:
                {
                    float s = phase * 2f - 1f;
                    if (dt > 0f)
                        s -= PolyBlep(phase, dt);
                    return s;
                }
                default:
                    return Mathf.Sin(Mathf.PI * 2f * phase);
            }
        }

        public static float AdvancePhase(float phase, float frequency)
        {
            phase += frequency / SampleRate;
            if (phase >= 1f)
                phase -= Mathf.Floor(phase);
            return phase;
        }

        public static float ExponentialLerp(float start, float end, float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (start <= 0f || end <= 0f)
                return Mathf.Lerp(start, end, progress);

            return Mathf.Exp(Mathf.Lerp(Mathf.Log(start), Mathf.Log(end), progress));
        }

        public static float MidiToFrequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        public static void MixStereo(ref float left, ref float right, float sample, float pan)
        {
            pan = Mathf.Clamp(pan, -1f, 1f);
            float leftGain = Mathf.Sqrt(0.5f * (1f - pan));
            float rightGain = Mathf.Sqrt(0.5f * (1f + pan));
            left += sample * leftGain;
            right += sample * rightGain;
        }

        public static float SoftClip(float x)
        {
            // Cubic soft-knee clip: linear below 0.7, smooth shoulder up to 1.0.
            // Much less harsh than tanh on near-unity signals.
            float ax = Math.Abs(x);
            if (ax <= 0.7f)
                return x;
            if (ax >= 1.4f)
                return x < 0f ? -1f : 1f;
            float t = (ax - 0.7f) / 0.7f;
            float knee = 0.7f + 0.3f * (t * (3f - 2f * t));
            return x < 0f ? -knee : knee;
        }

        // PolyBLEP correction term — eliminates aliasing step/ramp discontinuities.
        private static float PolyBlep(float t, float dt)
        {
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        // Sum a set of harmonic partials with independent amplitudes for a given phase.
        // harmonicAmps[0] = fundamental, [1] = 2nd harmonic, etc.
        public static float SampleAdditive(float[] harmonicAmps, float fundamentalPhase)
        {
            float sample = 0f;
            for (int h = 0; h < harmonicAmps.Length; h++)
            {
                if (harmonicAmps[h] == 0f) continue;
                float hp = (fundamentalPhase * (h + 1f)) % 1f;
                sample += Mathf.Sin(Mathf.PI * 2f * hp) * harmonicAmps[h];
            }
            return sample;
        }

        public static AudioClip BuildClip(string name, float[] left, float[] right)
        {
            int samples = Mathf.Min(left.Length, right.Length);
            var interleaved = new float[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                interleaved[i * 2] = Mathf.Clamp(left[i], -1f, 1f);
                interleaved[i * 2 + 1] = Mathf.Clamp(right[i], -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 2, SampleRate, false);
            clip.SetData(interleaved, 0);
            return clip;
        }

        public static int SecondsToSamples(float seconds)
        {
            return Mathf.Max(64, Mathf.RoundToInt(seconds * SampleRate));
        }

        public static int ComputeSeed(string key)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < key.Length; i++)
                    hash = hash * 31 + key[i];
                return hash;
            }
        }

        public static SoundProfile CreateProfile(
            float body,
            float brightness,
            float drive,
            float releaseBias,
            float transientSharpness,
            float resonance,
            float attackBias = 0.28f,
            float detune = 0.18f,
            float modDepth = 0.2f,
            float stereoSpread = 0.2f,
            float waveMorph = 0.24f,
            float filterMotion = 0.18f,
            float delayBias = 0.14f,
            float reverbBias = 0.18f)
        {
            return new SoundProfile
            {
                body = body,
                brightness = brightness,
                drive = drive,
                releaseBias = releaseBias,
                transientSharpness = transientSharpness,
                resonance = resonance,
                attackBias = attackBias,
                detune = detune,
                modDepth = modDepth,
                stereoSpread = stereoSpread,
                waveMorph = waveMorph,
                filterMotion = filterMotion,
                delayBias = delayBias,
                reverbBias = reverbBias
            };
        }
    }

    internal struct SvfState
    {
        public float low;
        public float band;
    }
}
