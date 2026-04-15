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

        public static RawSamples RenderRaw(ResolvedVoiceSpec spec)
        {
            int sampleCount = SynthUtilities.SecondsToSamples(
                spec.durationSeconds + spec.releaseSeconds + AudioEffectsChain.GetAmbienceTail(spec));
            var left  = new float[sampleCount];
            var right = new float[sampleCount];

            RenderIntoBuffers(spec, left, right, sampleCount);

            string name = $"{spec.patternType}_{spec.voiceType}_{spec.midi}";
            return new RawSamples { name = name, left = left, right = right };
        }

        public static AudioClip Render(ResolvedVoiceSpec spec)
        {
            int sampleCount = SynthUtilities.SecondsToSamples(
                spec.durationSeconds + spec.releaseSeconds + AudioEffectsChain.GetAmbienceTail(spec));
            var left  = new float[sampleCount];
            var right = new float[sampleCount];

            RenderIntoBuffers(spec, left, right, sampleCount);
            return SynthUtilities.BuildClip($"{spec.patternType}_{spec.voiceType}_{spec.midi}", left, right);
        }

        private static void RenderIntoBuffers(ResolvedVoiceSpec spec, float[] left, float[] right, int sampleCount)
        {
            float targetFreq = SynthUtilities.MidiToFrequency(spec.midi);
            float startFreq  = targetFreq * Mathf.Pow(2f, spec.glide * (0.18f + spec.filterMotion * 0.24f) / 12f);
            float glideTime  = Mathf.Max(0.015f, spec.attackSeconds * 1.4f);
            float releaseStart = Mathf.Max(spec.attackSeconds, spec.durationSeconds);

            // ── Layer identity ─────────────────────────────────────────────────
            bool isPad    = spec.patternType == PatternType.HarmonyPad;
            bool isMelody = spec.patternType == PatternType.MelodyLine;
            bool isKalimba  = spec.isNewAge && isMelody;  // NewAge melody → kalimba pluck
            bool isDronepad = spec.isNewAge && isPad;      // NewAge harmony → long drone

            float spread = 0.08f + spec.stereoSpread * (isPad ? 0.52f : 0.28f);

            // ── Modulation parameters ──────────────────────────────────────────
            // Wow/flutter only on lofi melody — pads use multi-voice chorus detuning instead
            float wowRate      = (spec.isLoFi && isMelody) ? 0.55f + spec.modDepth * 0.8f   : 0f;
            float wowDepth     = (spec.isLoFi && isMelody) ? 0.0018f + spec.detune * 0.0024f : 0f;
            float flutterRate  = (spec.isLoFi && isMelody) ? 3.8f + spec.modDepth * 3.2f    : 0f;
            float flutterDepth = (spec.isLoFi && isMelody) ? 0.0006f + spec.detune * 0.0011f : 0f;
            // Breath: NewAge drone pad = slow swell; kalimba = faster resonance flutter
            float breathRate  = spec.isNewAge ? (isKalimba ? 3.4f + spec.modDepth * 2.0f
                                                            : 0.18f + spec.modDepth * 0.4f) : 0f;
            float breathDepth = spec.isNewAge
                ? (isDronepad ? 0.0012f + spec.detune * 0.002f : 0.0006f + spec.detune * 0.001f)
                : 0f;
            float jazzVibRate     = spec.isJazz ? 4.2f + spec.modDepth * 2.8f  : 0f;
            float jazzVibCentsAmp = spec.isJazz && spec.modDepth > 0.2f ? 6f + spec.modDepth * 14f : 0f;
            float vibRate  = spec.modDepth > 0.1f
                ? (isPad ? 3.2f + spec.modDepth * 2.5f : 3.2f + spec.modDepth * 6.5f)
                : 0f;
            float vibCentsAmp = spec.modDepth > 0.1f
                ? (spec.isBass ? 4f : 10f) + spec.modDepth * 26f
                : 0f;

            // ── Additive partial amplitudes ────────────────────────────────────
            int numPartials = isPad ? 8 : isMelody ? 6 : 4;
            float[] partialGain  = new float[numPartials];
            float[] partialDecay = new float[numPartials];
            float   noteDecay    = spec.durationSeconds + spec.releaseSeconds;

            if (isPad)
            {
                // Lush pad: rich harmonics, very slow per-harmonic decay for sustained warmth
                float[] padAmps = { 1.0f, 0.55f, 0.28f, 0.18f, 0.10f, 0.06f, 0.03f, 0.015f };
                for (int h = 0; h < numPartials; h++)
                {
                    partialGain[h]  = padAmps[h] * (0.7f + spec.body * 0.3f);
                    partialDecay[h] = noteDecay * Mathf.Pow(0.82f, h);
                }
            }
            else if (isKalimba)
            {
                // Kalimba: fast-decaying bell spectrum — fundamentally different from piano or pad
                float[] kalAmps = { 1.0f, 0.38f, 0.14f, 0.06f, 0.02f, 0.008f };
                float kalBase = 0.12f + spec.releaseBias * 0.16f; // raised floor: fundamental doesn't vanish instantly
                for (int h = 0; h < numPartials; h++)
                {
                    partialGain[h]  = kalAmps[h];
                    partialDecay[h] = kalBase * Mathf.Pow(0.48f, h);
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

            // Phase accumulators per partial — pads get dream-style offsets for width
            float[] partialPhase = new float[numPartials];
            for (int h = 0; h < numPartials; h++)
                partialPhase[h] = (spec.isDream || isPad) ? 0.37f * (h + 1f) % 1f : 0.11f * (h + 1f) % 1f;

            // Osc B (chorus): pads start from a wider offset for immediate stereo image
            float phaseB   = (spec.isDream || isPad) ? 0.19f : 0.31f;
            float oscBGain = spec.isBell ? 0.14f + spec.brightness * 0.06f : 0.16f + spec.body * 0.14f;

            // Pad extra chorus voices C + D for lush multi-voice width
            // chorusWidthScale = 1/(1+roleIndex) so role-1+ pads sit narrower than role-0
            float phaseC = 0.53f, phaseD = 0.77f;
            float chorusW = (isPad && spec.chorusWidthScale > 0f) ? spec.chorusWidthScale : 1f;
            float padChorusGain = isPad ? (0.11f + spec.detune * 0.09f) * chorusW : 0f;
            float padCentC = isPad ? -(8f  + spec.detune * 18f) : 0f;
            float padCentD = isPad ?  (5f  + spec.detune * 14f) : 0f;

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
                float wow     = wowDepth     > 0f ? Mathf.Sin(Mathf.PI * 2f * wowRate     * t) * wowDepth     : 0f;
                float flutter = flutterDepth > 0f ? Mathf.Sin(Mathf.PI * 2f * flutterRate * t) * flutterDepth : 0f;
                float breath  = breathDepth  > 0f ? Mathf.Sin(Mathf.PI * 2f * breathRate  * t) * breathDepth  : 0f;
                float vibCents = vibCentsAmp    > 0f ? Mathf.Sin(Mathf.PI * 2f * vibRate     * t) * vibCentsAmp    : 0f;
                float jvCents  = jazzVibCentsAmp > 0f ? Mathf.Sin(Mathf.PI * 2f * jazzVibRate * t) * jazzVibCentsAmp : 0f;
                float modFreq  = frequency * (1f + wow + flutter + breath)
                               * Mathf.Pow(2f, (vibCents + jvCents) / 1200f);

                // ── Additive synthesis: sum partials ──────────────────────────
                float mono = 0f;
                for (int h = 0; h < numPartials; h++)
                {
                    float partFreq = modFreq * (h + 1f);
                    if (partFreq >= SynthUtilities.SampleRate * 0.47f) break;
                    float partEnv = env * partialGain[h]
                                  * Mathf.Exp(-t / Mathf.Max(0.001f, partialDecay[h]));
                    mono += Mathf.Sin(Mathf.PI * 2f * partialPhase[h]) * partEnv;
                    partialPhase[h] = SynthUtilities.AdvancePhase(partialPhase[h], partFreq);
                }

                float leftSample  = mono;
                float rightSample = mono;

                // ── Chorus/detune layer ────────────────────────────────────────
                // Kalimba: no chorus — pure dry pluck
                if (spec.useOscillatorB && !isKalimba)
                {
                    float oscBFreq = frequency * (spec.isBell ? 2f : 1.003f + spec.detune * 0.02f);
                    float detuneCents = (spec.isDream || isPad ? 6f : 10f) + spec.detune * 24f;
                    oscBFreq *= Mathf.Pow(2f, detuneCents / 1200f);
                    float oscBSample = SynthUtilities.SampleWaveWithDt(spec.waveB, phaseB,
                                           oscBFreq / SynthUtilities.SampleRate) * oscBGain * env;
                    phaseB = SynthUtilities.AdvancePhase(phaseB, oscBFreq);
                    // Pads: spread chorus wide; melody: subtle centre fill
                    SynthUtilities.MixStereo(ref leftSample, ref rightSample, oscBSample,
                        isPad ? spread * 0.55f : spread * 0.22f);

                    // ── Pad extra voices C + D ────────────────────────────────
                    if (padChorusGain > 0f)
                    {
                        float freqC = frequency * Mathf.Pow(2f, padCentC / 1200f);
                        float freqD = frequency * Mathf.Pow(2f, padCentD / 1200f);
                        float sC = SynthUtilities.SampleWaveWithDt(spec.waveB, phaseC,
                                       freqC / SynthUtilities.SampleRate) * padChorusGain * env;
                        float sD = SynthUtilities.SampleWaveWithDt(spec.waveA, phaseD,
                                       freqD / SynthUtilities.SampleRate) * padChorusGain * 0.85f * env;
                        phaseC = SynthUtilities.AdvancePhase(phaseC, freqC);
                        phaseD = SynthUtilities.AdvancePhase(phaseD, freqD);
                        SynthUtilities.MixStereo(ref leftSample, ref rightSample, sC, -spread * 0.6f);
                        SynthUtilities.MixStereo(ref leftSample, ref rightSample, sD,  spread * 0.7f);
                    }
                }
                else if (spread > 0.18f && !isKalimba)
                {
                    SynthUtilities.MixStereo(ref leftSample, ref rightSample, mono * 0.24f, spread * 0.16f);
                }

                // Stereo spread: pan additive layer slightly
                leftSample  *= 1f + spread * 0.12f;
                rightSample *= 1f - spread * 0.08f;

                // Lo-Fi hiss: melody only — pads get chorus texture instead
                if (spec.isLoFi && isMelody)
                {
                    float hiss = (Mathf.PerlinNoise(t * 12f, 0.5f) - 0.5f) * 0.012f * env;
                    leftSample  += hiss * 0.8f;
                    rightSample += hiss * 0.6f;
                }

                // ── Melody strike transient ────────────────────────────────────
                // Kalimba: 150 Hz sine body thump (finger on tine base) — warm, not clacky.
                // Other melody (piano/Rhodes): short noise burst for hammer/pluck character.
                if (isMelody && t < 0.032f)
                {
                    if (isKalimba)
                    {
                        float thumpEnv  = Mathf.Exp(-t / 0.020f);
                        float thumpGain = 0.08f * (0.5f + spec.body * 0.5f);
                        float thump     = Mathf.Sin(Mathf.PI * 2f * 150f * t) * thumpGain * thumpEnv;
                        leftSample  += thump;
                        rightSample += thump * 0.92f;
                    }
                    else
                    {
                        float transGain  = 0.035f + spec.transientSharpness * 0.075f;
                        float transEnv   = Mathf.Exp(-t / 0.005f);
                        float genreScale = spec.isJazz ? 1.4f : 1.0f;
                        float transNoise = (Mathf.PerlinNoise(t * 9000f, spec.midi * 0.019f) - 0.5f) * 2f;
                        leftSample  += transNoise * transGain * transEnv * genreScale;
                        rightSample += transNoise * transGain * transEnv * genreScale * 0.88f;
                    }
                }

                // ── Jazz Rhodes tine ───────────────────────────────────────────
                // Inharmonic metallic partial — melody only (comp pads should be diffuse, not tine-y)
                if (spec.isJazz && isMelody)
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

            // Apply per-mode gain staging (baked in so the cached clip already has the right level)
            if (spec.velocityScale > 0f && spec.velocityScale < 0.999f)
            {
                for (int i = 0; i < left.Length; i++)
                {
                    left[i]  *= spec.velocityScale;
                    right[i] *= spec.velocityScale;
                }
            }

            AudioEffectsChain.ApplyVoiceChain(spec, left, right);
            AudioEffectsChain.ApplyAmbience(spec, left, right);
            AudioEffectsChain.NormalizeStereo(left, right);
        }
    }
}
