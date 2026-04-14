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
            var left  = new float[sampleCount];
            var right = new float[sampleCount];

            float targetFreq = SynthUtilities.MidiToFrequency(spec.midi);
            float startFreq  = targetFreq * Mathf.Pow(2f, spec.glide * (0.18f + spec.filterMotion * 0.24f) / 12f);
            float glideTime  = Mathf.Max(0.015f, spec.attackSeconds * 1.4f);
            float spread     = 0.08f + spec.stereoSpread * (spec.patternType == PatternType.HarmonyPad ? 0.42f : 0.28f);
            float releaseStart = Mathf.Max(spec.attackSeconds, spec.durationSeconds);
            float dt         = targetFreq / SynthUtilities.SampleRate; // PolyBLEP increment

            // ── Modulation parameters ──────────────────────────────────────────
            float wowRate    = spec.isLoFi ? 0.55f + spec.modDepth * 0.8f  : 0f;
            float wowDepth   = spec.isLoFi ? 0.0018f + spec.detune * 0.0024f : 0f;
            float flutterRate  = spec.isLoFi ? 3.8f + spec.modDepth * 3.2f  : 0f;
            float flutterDepth = spec.isLoFi ? 0.0006f + spec.detune * 0.0011f : 0f;
            float breathRate   = spec.isNewAge ? 0.18f + spec.modDepth * 0.4f : 0f;
            float breathDepth  = spec.isNewAge && spec.patternType == PatternType.HarmonyPad
                ? 0.0012f + spec.detune * 0.002f : 0f;
            float jazzVibRate  = spec.isJazz ? 4.2f + spec.modDepth * 2.8f  : 0f;
            float jazzVibCentsAmp = spec.isJazz && spec.modDepth > 0.2f ? 6f + spec.modDepth * 14f : 0f;
            float vibRate  = spec.modDepth > 0.1f
                ? (spec.patternType == PatternType.HarmonyPad ? 3.2f + spec.modDepth * 2.5f : 3.2f + spec.modDepth * 6.5f)
                : 0f;
            float vibCentsAmp = spec.modDepth > 0.1f
                ? (spec.isBass ? 4f : 10f) + spec.modDepth * 26f
                : 0f;

            // ── Additive partial amplitudes ───────────────────────────────────
            // Build per-partial gain tables once; each partial decays at its own rate.
            // Real instruments: upper partials decay faster than fundamental.
            bool isPad    = spec.patternType == PatternType.HarmonyPad;
            bool isMelody = spec.patternType == PatternType.MelodyLine;
            int numPartials = isPad ? 8 : isMelody ? 6 : 4;

            float[] partialGain  = new float[numPartials];
            float[] partialDecay = new float[numPartials]; // seconds
            float   noteDecay    = spec.durationSeconds + spec.releaseSeconds;

            if (isPad)
            {
                // Organ/string pad: rich lower harmonics, gentle rolloff
                float[] padAmps = { 1.0f, 0.55f, 0.28f, 0.18f, 0.10f, 0.06f, 0.03f, 0.015f };
                for (int h = 0; h < numPartials; h++)
                {
                    partialGain[h]  = padAmps[h] * (0.7f + spec.body * 0.3f);
                    partialDecay[h] = noteDecay * Mathf.Pow(0.72f, h); // each harmonic decays faster
                }
            }
            else if (isMelody)
            {
                // Piano-like: strong 1st–3rd, faster decay on upper partials
                float bright = 0.4f + spec.brightness * 0.6f;
                float[] melAmps = { 1.0f, 0.45f * bright, 0.25f * bright, 0.14f, 0.07f, 0.03f };
                for (int h = 0; h < numPartials; h++)
                {
                    partialGain[h]  = melAmps[h];
                    partialDecay[h] = noteDecay * Mathf.Pow(0.65f, h);
                }
            }
            else
            {
                // Bass / bell: fundamental-heavy
                float[] bassAmps = { 1.0f, 0.28f, 0.12f, 0.05f };
                for (int h = 0; h < numPartials; h++)
                {
                    partialGain[h]  = bassAmps[h];
                    partialDecay[h] = noteDecay * Mathf.Pow(0.60f, h);
                }
            }

            // Phase accumulators per partial
            float[] partialPhase = new float[numPartials];
            for (int h = 0; h < numPartials; h++)
                partialPhase[h] = spec.isDream ? 0.37f * (h + 1f) % 1f : 0.11f * (h + 1f) % 1f;

            // Detuned oscillator B phase (chorus layer)
            float phaseB     = spec.isDream ? 0.19f : 0.31f;
            float oscBGain   = spec.isBell ? 0.14f + spec.brightness * 0.06f : 0.16f + spec.body * 0.14f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t   = (float)i / SynthUtilities.SampleRate;
                float env = SynthUtilities.EnvelopeAtTime(spec, t, releaseStart);
                if (env <= 0.0001f)
                    continue;

                float frequency = SynthUtilities.ExponentialLerp(
                    startFreq, targetFreq,
                    glideTime <= 0f ? 1f : Mathf.Clamp01(t / glideTime));

                // Modulation
                float wow     = wowDepth   > 0f ? Mathf.Sin(Mathf.PI * 2f * wowRate    * t) * wowDepth    : 0f;
                float flutter = flutterDepth > 0f ? Mathf.Sin(Mathf.PI * 2f * flutterRate  * t) * flutterDepth : 0f;
                float breath  = breathDepth  > 0f ? Mathf.Sin(Mathf.PI * 2f * breathRate   * t) * breathDepth  : 0f;
                float vibCents = vibCentsAmp > 0f ? Mathf.Sin(Mathf.PI * 2f * vibRate * t) * vibCentsAmp : 0f;
                float jvCents  = jazzVibCentsAmp > 0f ? Mathf.Sin(Mathf.PI * 2f * jazzVibRate * t) * jazzVibCentsAmp : 0f;
                float modFreq  = frequency * (1f + wow + flutter + breath)
                               * Mathf.Pow(2f, (vibCents + jvCents) / 1200f);

                // ── Additive synthesis: sum partials ──────────────────────────
                float mono = 0f;
                for (int h = 0; h < numPartials; h++)
                {
                    float partFreq = modFreq * (h + 1f);
                    // Skip partials above Nyquist
                    if (partFreq >= SynthUtilities.SampleRate * 0.47f) break;
                    float partEnv = env * partialGain[h]
                                  * Mathf.Exp(-t / Mathf.Max(0.02f, partialDecay[h]));
                    mono += Mathf.Sin(Mathf.PI * 2f * partialPhase[h]) * partEnv;
                    partialPhase[h] = SynthUtilities.AdvancePhase(partialPhase[h], partFreq);
                }

                float leftSample  = mono;
                float rightSample = mono;

                // ── Detuned chorus layer (osc B) ──────────────────────────────
                if (spec.useOscillatorB)
                {
                    float oscBFreq = frequency * (spec.isBell ? 2f : 1.003f + spec.detune * 0.02f);
                    float detuneCents = (spec.isDream ? 6f : 10f) + spec.detune * 24f;
                    oscBFreq *= Mathf.Pow(2f, detuneCents / 1200f);
                    float oscBdt     = oscBFreq / SynthUtilities.SampleRate;
                    float oscBSample = SynthUtilities.SampleWaveWithDt(spec.waveB, phaseB, oscBdt)
                                     * oscBGain * env;
                    phaseB = SynthUtilities.AdvancePhase(phaseB, oscBFreq);
                    SynthUtilities.MixStereo(ref leftSample, ref rightSample, oscBSample, spread * 0.35f);
                }
                else if (spread > 0.18f)
                {
                    SynthUtilities.MixStereo(ref leftSample, ref rightSample, mono * 0.24f, spread * 0.16f);
                }

                // Stereo spread: pan additive layer slightly
                leftSample  *= 1f + spread * 0.12f;
                rightSample *= 1f - spread * 0.08f;

                // Lo-Fi hiss
                if (spec.isLoFi && !spec.isNewAge)
                {
                    float hiss = (Mathf.PerlinNoise(t * 12f, 0.5f) - 0.5f) * 0.012f * env;
                    leftSample  += hiss * 0.8f;
                    rightSample += hiss * 0.6f;
                }

                // Jazz Rhodes tine: inharmonic partial with fast decay (electric piano character)
                if (spec.isJazz && spec.patternType != PatternType.RhythmLoop)
                {
                    float tineEnv  = Mathf.Exp(-t / Mathf.Max(0.02f, spec.attackSeconds * 0.5f));
                    float tineFreq = targetFreq * 3.2f;
                    float tine     = Mathf.Sin(Mathf.PI * 2f * tineFreq * t) * 0.14f * tineEnv * env;
                    leftSample  += tine * 0.9f;
                    rightSample += tine * 0.8f;
                }

                left[i]  = leftSample;
                right[i] = rightSample;
            }

            AudioEffectsChain.ApplyVoiceChain(spec, left, right);
            AudioEffectsChain.ApplyAmbience(spec, left, right);
            AudioEffectsChain.NormalizeStereo(left, right);
            return SynthUtilities.BuildClip($"{spec.patternType}_{spec.voiceType}_{spec.midi}", left, right);
        }
    }
}
