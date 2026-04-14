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
                return Mathf.Clamp01(time / Mathf.Max(0.001f, spec.attackSeconds));

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
            phase -= Mathf.Floor(phase);

            switch (waveform)
            {
                case VoiceWaveform.Triangle:
                    return 1f - 4f * Mathf.Abs(phase - 0.5f);
                case VoiceWaveform.Square:
                    return phase < 0.5f ? 1f : -1f;
                case VoiceWaveform.Sawtooth:
                    return phase * 2f - 1f;
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
            return (float)Math.Tanh(x);
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
