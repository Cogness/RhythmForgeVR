using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    internal static class TonalSynthesizer
    {
        public static AudioClip GeneratePerc(float freq = 380f, float duration = 0.12f)
        {
            var preset = InstrumentPresets.Get("dream-perc");
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(freq / 440f, 2f));
            var spec = VoiceSpecResolver.ResolveHarmony(
                preset,
                SynthUtilities.CreateProfile(
                    body: 0.42f,
                    brightness: 0.54f,
                    drive: 0.22f,
                    releaseBias: Mathf.Clamp01(duration / 0.3f),
                    transientSharpness: 0.42f,
                    resonance: 0.44f,
                    stereoSpread: 0.26f,
                    modDepth: 0.18f),
                midi,
                duration,
                0.52f,
                preset.fxSend * 0.5f);
            return Render(spec);
        }

        public static AudioClip GenerateTone(float freq = 261.63f, float duration = 2.0f)
        {
            var preset = InstrumentPresets.Get("lofi-piano");
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(freq / 440f, 2f));
            var spec = VoiceSpecResolver.ResolveMelody(
                preset,
                SynthUtilities.CreateProfile(
                    body: 0.54f,
                    brightness: 0.46f,
                    drive: 0.24f,
                    releaseBias: 0.48f,
                    attackBias: 0.36f,
                    resonance: 0.34f,
                    detune: 0.18f,
                    modDepth: 0.22f,
                    stereoSpread: 0.3f,
                    waveMorph: 0.42f,
                    filterMotion: 0.28f,
                    transientSharpness: 0.34f),
                midi,
                duration,
                0.56f,
                preset.fxSend,
                0f);
            return Render(spec);
        }

        public static AudioClip GeneratePad(float freq = 261.63f, float duration = 3.0f)
        {
            var preset = InstrumentPresets.Get("dream-pad");
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(freq / 440f, 2f));
            var spec = VoiceSpecResolver.ResolveHarmony(
                preset,
                SynthUtilities.CreateProfile(
                    body: 0.7f,
                    brightness: 0.32f,
                    drive: 0.14f,
                    releaseBias: 0.74f,
                    transientSharpness: 0.22f,
                    attackBias: 0.22f,
                    resonance: 0.3f,
                    detune: 0.34f,
                    modDepth: 0.42f,
                    stereoSpread: 0.76f,
                    waveMorph: 0.6f,
                    filterMotion: 0.5f,
                    reverbBias: 0.72f,
                    delayBias: 0.36f),
                midi,
                duration,
                0.48f,
                preset.fxSend);
            return Render(spec);
        }

        public static AudioClip Render(ResolvedVoiceSpec spec)
        {
            int sampleCount = SynthUtilities.SecondsToSamples(
                spec.durationSeconds + spec.releaseSeconds + AudioEffectsChain.GetAmbienceTail(spec));
            var left = new float[sampleCount];
            var right = new float[sampleCount];

            float targetFrequency = SynthUtilities.MidiToFrequency(spec.midi);
            float startFrequency = targetFrequency * Mathf.Pow(2f, spec.glide * (0.18f + spec.filterMotion * 0.24f) / 12f);
            float glideTime = Mathf.Max(0.015f, spec.attackSeconds * 1.4f);
            float spread = 0.08f + spec.stereoSpread * (spec.patternType == PatternType.HarmonyPad ? 0.42f : 0.28f);
            float oscAGain = spec.patternType == PatternType.HarmonyPad ? 0.54f : 0.72f;
            float oscBGain = spec.isBell
                ? 0.14f + spec.brightness * 0.06f
                : 0.16f + spec.body * 0.14f;

            float phaseA = spec.isDream ? 0.37f : 0.11f;
            float phaseB = spec.isDream ? 0.19f : 0.31f;
            float wowRate = spec.isLoFi ? 0.55f + spec.modDepth * 0.8f : 0f;
            float wowDepth = spec.isLoFi ? 0.0018f + spec.detune * 0.0024f : 0f;
            float flutterRate = spec.isLoFi ? 3.8f + spec.modDepth * 3.2f : 0f;
            float flutterDepth = spec.isLoFi ? 0.0006f + spec.detune * 0.0011f : 0f;
            float vibratoRate = spec.modDepth > 0.1f
                ? (spec.patternType == PatternType.HarmonyPad ? 3.2f + spec.modDepth * 2.5f : 3.2f + spec.modDepth * 6.5f)
                : 0f;
            float vibratoDepthCents = spec.modDepth > 0.1f
                ? (spec.isBass ? 4f : 10f) + spec.modDepth * 26f
                : 0f;

            float releaseStart = Mathf.Max(spec.attackSeconds, spec.durationSeconds);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeAtTime(spec, t, releaseStart);
                if (env <= 0.0001f)
                    continue;

                float frequency = SynthUtilities.ExponentialLerp(
                    startFrequency,
                    targetFrequency,
                    glideTime <= 0f ? 1f : Mathf.Clamp01(t / glideTime));
                float wow = wowDepth > 0f ? Mathf.Sin(Mathf.PI * 2f * wowRate * t) * wowDepth : 0f;
                float flutter = flutterDepth > 0f ? Mathf.Sin(Mathf.PI * 2f * flutterRate * t) * flutterDepth : 0f;
                float vibratoCents = vibratoDepthCents > 0f ? Mathf.Sin(Mathf.PI * 2f * vibratoRate * t) * vibratoDepthCents : 0f;
                float oscAFrequency = frequency * (1f + wow + flutter) * Mathf.Pow(2f, vibratoCents / 1200f);
                float oscASample = SynthUtilities.SampleWave(spec.waveA, phaseA) * oscAGain * env;
                phaseA = SynthUtilities.AdvancePhase(phaseA, oscAFrequency);

                float leftSample = 0f;
                float rightSample = 0f;
                SynthUtilities.MixStereo(ref leftSample, ref rightSample, oscASample, -spread * 0.35f);

                if (spec.useOscillatorB)
                {
                    float oscBFrequency = frequency * (spec.isBell ? 2f : 1.003f + spec.detune * 0.02f);
                    float detuneCents = (spec.isDream ? 6f : 10f) + spec.detune * 24f;
                    oscBFrequency *= Mathf.Pow(2f, detuneCents / 1200f);
                    float oscBSample = SynthUtilities.SampleWave(spec.waveB, phaseB) * oscBGain * env;
                    phaseB = SynthUtilities.AdvancePhase(phaseB, oscBFrequency);
                    SynthUtilities.MixStereo(ref leftSample, ref rightSample, oscBSample, spread * 0.35f);
                }
                else if (spread > 0.18f)
                {
                    SynthUtilities.MixStereo(ref leftSample, ref rightSample, oscASample * 0.24f, spread * 0.16f);
                }

                if (spec.isLoFi)
                {
                    float hiss = (Mathf.PerlinNoise(t * 12f, 0.5f) - 0.5f) * 0.012f * env;
                    leftSample += hiss * 0.8f;
                    rightSample += hiss * 0.6f;
                }

                left[i] = leftSample;
                right[i] = rightSample;
            }

            AudioEffectsChain.ApplyVoiceChain(spec, left, right);
            AudioEffectsChain.ApplyAmbience(spec, left, right);
            AudioEffectsChain.NormalizeStereo(left, right);
            return SynthUtilities.BuildClip($"{spec.patternType}_{spec.voiceType}_{spec.midi}", left, right);
        }
    }
}
