using System;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    public enum VoiceWaveform
    {
        Sine,
        Triangle,
        Square,
        Sawtooth
    }

    public enum VoiceFilterMode
    {
        LowPass,
        HighPass,
        BandPass
    }

    /// <summary>
    /// Runtime-only audio render description derived from preset family, pattern type,
    /// and effective sound profile. This is not persisted to session state.
    /// </summary>
    public struct ResolvedVoiceSpec
    {
        public PatternType patternType;
        public string voiceType;
        public string lane;
        public int midi;
        public float durationSeconds;
        public float glide;
        public float positionBrightness;
        public float brightness;
        public float body;
        public float drive;
        public float resonance;
        public float filterMotion;
        public float modDepth;
        public float detune;
        public float stereoSpread;
        public float transientSharpness;
        public float releaseBias;
        public float attackBias;
        public float waveMorph;
        public float delayBias;
        public float reverbBias;
        public float fxSend;
        public float attackSeconds;
        public float releaseSeconds;
        public VoiceWaveform waveA;
        public VoiceWaveform waveB;
        public VoiceFilterMode filterMode;
        public bool useOscillatorB;
        public bool isBass;
        public bool isBell;
        public bool isLoFi;
        public bool isTrap;
        public bool isDream;

        public string GetCacheKey()
        {
            return string.Join("|",
                patternType,
                voiceType ?? string.Empty,
                lane ?? string.Empty,
                midi,
                Quantize(durationSeconds, 24f),
                Quantize(glide + 2f, 12f),
                Quantize(positionBrightness, 12f),
                Quantize(brightness, 12f),
                Quantize(body, 12f),
                Quantize(drive, 12f),
                Quantize(resonance, 12f),
                Quantize(filterMotion, 12f),
                Quantize(modDepth, 12f),
                Quantize(detune, 12f),
                Quantize(stereoSpread, 12f),
                Quantize(transientSharpness, 12f),
                Quantize(releaseBias, 12f),
                Quantize(attackBias, 12f),
                Quantize(waveMorph, 12f),
                Quantize(delayBias, 12f),
                Quantize(reverbBias, 12f),
                Quantize(fxSend, 16f),
                Quantize(attackSeconds, 40f),
                Quantize(releaseSeconds, 20f),
                (int)waveA,
                (int)waveB,
                (int)filterMode);
        }

        private static int Quantize(float value, float scale)
        {
            return Mathf.RoundToInt(value * scale);
        }
    }

    public static class VoiceSpecResolver
    {
        public static ResolvedVoiceSpec ResolveDrum(
            string lane,
            InstrumentPreset preset,
            SoundProfile profile,
            float positionBrightness,
            float fxSend)
        {
            var spec = CreateBaseSpec(PatternType.RhythmLoop, preset, profile, positionBrightness, fxSend);
            spec.lane = lane ?? "perc";
            spec.durationSeconds = spec.lane == "kick"
                ? 0.34f
                : spec.lane == "snare" ? 0.24f
                : spec.lane == "perc" ? 0.18f : 0.10f;
            spec.attackSeconds = 0.002f + spec.attackBias * 0.004f;
            spec.releaseSeconds = 0.05f + spec.releaseBias * 0.22f;
            spec.useOscillatorB = false;
            return spec;
        }

        public static ResolvedVoiceSpec ResolveMelody(
            InstrumentPreset preset,
            SoundProfile profile,
            int midi,
            float duration,
            float positionBrightness,
            float fxSend,
            float glide = 0f)
        {
            var spec = CreateBaseSpec(PatternType.MelodyLine, preset, profile, positionBrightness, fxSend);
            spec.midi = midi;
            spec.durationSeconds = QuantizeDuration(duration, 0.04f, 0.08f, 1.6f);
            spec.glide = QuantizeSigned(glide, 20f);
            ResolveTonalDetails(ref spec);
            return spec;
        }

        public static ResolvedVoiceSpec ResolveHarmony(
            InstrumentPreset preset,
            SoundProfile profile,
            int midi,
            float duration,
            float positionBrightness,
            float fxSend)
        {
            var spec = CreateBaseSpec(PatternType.HarmonyPad, preset, profile, positionBrightness, fxSend);
            spec.midi = midi;
            spec.durationSeconds = QuantizeDuration(duration, 0.08f, 0.12f, 4.6f);
            spec.glide = 0f;
            ResolveTonalDetails(ref spec);
            return spec;
        }

        private static ResolvedVoiceSpec CreateBaseSpec(
            PatternType type,
            InstrumentPreset preset,
            SoundProfile profile,
            float positionBrightness,
            float fxSend)
        {
            preset = preset ?? InstrumentPresets.All[0];
            profile = profile ?? new SoundProfile();

            string voiceType = preset.voiceType ?? string.Empty;
            string voiceDescriptor = voiceType.ToLowerInvariant();
            string familyIdentity = $"{preset.groupId ?? string.Empty} {voiceType}".ToLowerInvariant();

            return new ResolvedVoiceSpec
            {
                patternType = type,
                voiceType = voiceType,
                positionBrightness = Quantize01(positionBrightness),
                brightness = Quantize01(profile.brightness),
                body = Quantize01(profile.body),
                drive = Quantize01(profile.drive),
                resonance = Quantize01(profile.resonance),
                filterMotion = Quantize01(profile.filterMotion),
                modDepth = Quantize01(profile.modDepth),
                detune = Quantize01(profile.detune),
                stereoSpread = Quantize01(profile.stereoSpread),
                transientSharpness = Quantize01(profile.transientSharpness),
                releaseBias = Quantize01(profile.releaseBias),
                attackBias = Quantize01(profile.attackBias),
                waveMorph = Quantize01(profile.waveMorph),
                delayBias = Quantize01(profile.delayBias),
                reverbBias = Quantize01(profile.reverbBias),
                fxSend = QuantizeFxSend(fxSend),
                isBass = voiceDescriptor.Contains("bass"),
                isBell = voiceDescriptor.Contains("bell"),
                isLoFi = familyIdentity.Contains("lofi"),
                isTrap = familyIdentity.Contains("trap"),
                isDream = familyIdentity.Contains("dream")
            };
        }

        private static void ResolveTonalDetails(ref ResolvedVoiceSpec spec)
        {
            ResolveWaveforms(ref spec);
            spec.filterMode = ResolveFilterMode(spec);
            spec.attackSeconds = ResolveAttackSeconds(spec);
            spec.releaseSeconds = ResolveReleaseSeconds(spec);
            spec.useOscillatorB = !spec.isBass || spec.patternType == PatternType.HarmonyPad;
        }

        private static void ResolveWaveforms(ref ResolvedVoiceSpec spec)
        {
            if (spec.patternType == PatternType.HarmonyPad)
            {
                spec.waveA = spec.waveMorph > 0.55f ? VoiceWaveform.Sawtooth : VoiceWaveform.Triangle;
                spec.waveB = spec.body > 0.58f ? VoiceWaveform.Triangle : VoiceWaveform.Sine;
                return;
            }

            if (spec.isBass)
            {
                spec.waveA = spec.body > 0.60f ? VoiceWaveform.Triangle : VoiceWaveform.Square;
                spec.waveB = VoiceWaveform.Sine;
                return;
            }

            if (spec.isBell)
            {
                spec.waveA = VoiceWaveform.Triangle;
                spec.waveB = spec.waveMorph > 0.58f ? VoiceWaveform.Sine : VoiceWaveform.Square;
                return;
            }

            spec.waveA = spec.waveMorph > 0.58f
                ? VoiceWaveform.Square
                : spec.body > 0.60f ? VoiceWaveform.Triangle : VoiceWaveform.Sine;
            spec.waveB = spec.brightness > 0.60f ? VoiceWaveform.Triangle : VoiceWaveform.Sine;
        }

        private static VoiceFilterMode ResolveFilterMode(ResolvedVoiceSpec spec)
        {
            if (spec.patternType == PatternType.HarmonyPad)
                return spec.filterMotion > 0.62f ? VoiceFilterMode.BandPass : VoiceFilterMode.LowPass;

            if (spec.brightness > 0.74f && spec.body < 0.45f)
                return VoiceFilterMode.HighPass;

            return VoiceFilterMode.LowPass;
        }

        private static float ResolveAttackSeconds(ResolvedVoiceSpec spec)
        {
            float presetAttack = 0.01f;
            var preset = InstrumentPresets.GetDefaultForVoice(spec.voiceType);
            if (preset?.envelope != null) presetAttack = preset.envelope.attack;

            float maxAttack = spec.patternType == PatternType.HarmonyPad ? 1.6f : 0.24f;
            return Mathf.Clamp(presetAttack * (0.35f + spec.attackBias * 1.6f), 0.003f, maxAttack);
        }

        private static float ResolveReleaseSeconds(ResolvedVoiceSpec spec)
        {
            float presetRelease = 0.35f;
            var preset = InstrumentPresets.GetDefaultForVoice(spec.voiceType);
            if (preset?.envelope != null) presetRelease = preset.envelope.release;

            float maxRelease = spec.patternType == PatternType.HarmonyPad ? 4.2f : 1.4f;
            return Mathf.Clamp(presetRelease * (0.45f + spec.releaseBias * 1.9f), 0.08f, maxRelease);
        }

        private static float Quantize01(float value)
        {
            return Mathf.Round(Mathf.Clamp01(value) * 12f) / 12f;
        }

        private static float QuantizeFxSend(float value)
        {
            return Mathf.Round(Mathf.Clamp01(value) * 16f) / 16f;
        }

        private static float QuantizeDuration(float value, float bucket, float min, float max)
        {
            float clamped = Mathf.Clamp(value, min, max);
            return Mathf.Max(min, Mathf.Round(clamped / bucket) * bucket);
        }

        private static float QuantizeSigned(float value, float scale)
        {
            return Mathf.Round(value * scale) / scale;
        }
    }

    /// <summary>
    /// Generates procedural AudioClips from resolved voice specs at runtime.
    /// Clips are rendered per-event or reused from cache by SamplePlayer.
    /// </summary>
    public static class ProceduralSynthesizer
    {
        public const int SampleRate = 44100;

        public static AudioClip GenerateKick(float duration = 0.32f, float drive = 1.4f)
        {
            var preset = InstrumentPresets.Get("lofi-drums");
            var spec = VoiceSpecResolver.ResolveDrum(
                "kick",
                preset,
                CreateProfile(body: 0.72f, brightness: 0.34f, drive: Mathf.Clamp01(0.18f + (drive - 1f) * 0.3f),
                    releaseBias: Mathf.Clamp01(duration / 0.5f), transientSharpness: 0.46f, resonance: 0.24f),
                0.55f,
                preset.fxSend);
            return RenderDrum(spec);
        }

        public static AudioClip GenerateSnare(float duration = 0.22f)
        {
            var preset = InstrumentPresets.Get("lofi-drums");
            var spec = VoiceSpecResolver.ResolveDrum(
                "snare",
                preset,
                CreateProfile(body: 0.52f, brightness: 0.58f, drive: 0.28f,
                    releaseBias: Mathf.Clamp01(duration / 0.4f), transientSharpness: 0.58f, resonance: 0.36f),
                0.6f,
                preset.fxSend);
            return RenderDrum(spec);
        }

        public static AudioClip GenerateHat(float duration = 0.08f)
        {
            var preset = InstrumentPresets.Get("lofi-drums");
            var spec = VoiceSpecResolver.ResolveDrum(
                "hat",
                preset,
                CreateProfile(body: 0.18f, brightness: 0.8f, drive: 0.18f,
                    releaseBias: Mathf.Clamp01(duration / 0.2f), transientSharpness: 0.74f, resonance: 0.52f),
                0.78f,
                preset.fxSend);
            return RenderDrum(spec);
        }

        public static AudioClip GeneratePerc(float freq = 380f, float duration = 0.12f)
        {
            var preset = InstrumentPresets.Get("dream-perc");
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(freq / 440f, 2f));
            var spec = VoiceSpecResolver.ResolveHarmony(
                preset,
                CreateProfile(body: 0.42f, brightness: 0.54f, drive: 0.22f,
                    releaseBias: Mathf.Clamp01(duration / 0.3f), transientSharpness: 0.42f,
                    resonance: 0.44f, stereoSpread: 0.26f, modDepth: 0.18f),
                midi,
                duration,
                0.52f,
                preset.fxSend * 0.5f);
            return RenderTone(spec);
        }

        public static AudioClip GenerateTone(float freq = 261.63f, float duration = 2.0f)
        {
            var preset = InstrumentPresets.Get("lofi-piano");
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(freq / 440f, 2f));
            var spec = VoiceSpecResolver.ResolveMelody(
                preset,
                CreateProfile(body: 0.54f, brightness: 0.46f, drive: 0.24f, releaseBias: 0.48f,
                    attackBias: 0.36f, resonance: 0.34f, detune: 0.18f,
                    modDepth: 0.22f, stereoSpread: 0.3f, waveMorph: 0.42f,
                    filterMotion: 0.28f, transientSharpness: 0.34f),
                midi,
                duration,
                0.56f,
                preset.fxSend,
                0f);
            return RenderTone(spec);
        }

        public static AudioClip GeneratePad(float freq = 261.63f, float duration = 3.0f)
        {
            var preset = InstrumentPresets.Get("dream-pad");
            int midi = Mathf.RoundToInt(69f + 12f * Mathf.Log(freq / 440f, 2f));
            var spec = VoiceSpecResolver.ResolveHarmony(
                preset,
                CreateProfile(body: 0.7f, brightness: 0.32f, drive: 0.14f, releaseBias: 0.74f,
                    transientSharpness: 0.22f,
                    attackBias: 0.22f, resonance: 0.3f, detune: 0.34f,
                    modDepth: 0.42f, stereoSpread: 0.76f, waveMorph: 0.6f,
                    filterMotion: 0.5f, reverbBias: 0.72f, delayBias: 0.36f),
                midi,
                duration,
                0.48f,
                preset.fxSend);
            return RenderTone(spec);
        }

        public static AudioClip RenderDrum(ResolvedVoiceSpec spec)
        {
            int sampleCount = SecondsToSamples(spec.durationSeconds + spec.releaseSeconds * 0.45f + GetAmbienceTail(spec) * 0.35f);
            var left = new float[sampleCount];
            var right = new float[sampleCount];
            int seed = ComputeSeed(spec.GetCacheKey());

            switch (spec.lane)
            {
                case "kick":
                    RenderKick(spec, left, right, seed);
                    break;
                case "snare":
                    RenderSnare(spec, left, right, seed);
                    break;
                case "hat":
                    RenderHat(spec, left, right, seed);
                    break;
                default:
                    RenderPercussion(spec, left, right, seed);
                    break;
            }

            ApplyVoiceChain(spec, left, right);
            ApplyAmbience(spec, left, right);
            NormalizeStereo(left, right);
            return BuildClip($"Drum_{spec.lane}", left, right);
        }

        public static AudioClip RenderTone(ResolvedVoiceSpec spec)
        {
            int sampleCount = SecondsToSamples(spec.durationSeconds + spec.releaseSeconds + GetAmbienceTail(spec));
            var left = new float[sampleCount];
            var right = new float[sampleCount];

            float targetFrequency = MidiToFrequency(spec.midi);
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
                float t = (float)i / SampleRate;
                float env = EnvelopeAtTime(spec, t, releaseStart);
                if (env <= 0.0001f)
                    continue;

                float frequency = ExponentialLerp(startFrequency, targetFrequency, glideTime <= 0f ? 1f : Mathf.Clamp01(t / glideTime));
                float wow = wowDepth > 0f ? Mathf.Sin(Mathf.PI * 2f * wowRate * t) * wowDepth : 0f;
                float flutter = flutterDepth > 0f ? Mathf.Sin(Mathf.PI * 2f * flutterRate * t) * flutterDepth : 0f;
                float vibratoCents = vibratoDepthCents > 0f ? Mathf.Sin(Mathf.PI * 2f * vibratoRate * t) * vibratoDepthCents : 0f;
                float oscAFrequency = frequency * (1f + wow + flutter) * Mathf.Pow(2f, vibratoCents / 1200f);
                float oscASample = SampleWave(spec.waveA, phaseA) * oscAGain * env;
                phaseA = AdvancePhase(phaseA, oscAFrequency);

                float leftSample = 0f;
                float rightSample = 0f;
                MixStereo(ref leftSample, ref rightSample, oscASample, -spread * 0.35f);

                if (spec.useOscillatorB)
                {
                    float oscBFrequency = frequency * (spec.isBell ? 2f : 1.003f + spec.detune * 0.02f);
                    float detuneCents = (spec.isDream ? 6f : 10f) + spec.detune * 24f;
                    oscBFrequency *= Mathf.Pow(2f, detuneCents / 1200f);
                    float oscBSample = SampleWave(spec.waveB, phaseB) * oscBGain * env;
                    phaseB = AdvancePhase(phaseB, oscBFrequency);
                    MixStereo(ref leftSample, ref rightSample, oscBSample, spread * 0.35f);
                }
                else if (spread > 0.18f)
                {
                    MixStereo(ref leftSample, ref rightSample, oscASample * 0.24f, spread * 0.16f);
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

            ApplyVoiceChain(spec, left, right);
            ApplyAmbience(spec, left, right);
            NormalizeStereo(left, right);
            return BuildClip($"{spec.patternType}_{spec.voiceType}_{spec.midi}", left, right);
        }

        private static void RenderKick(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            float phase = 0f;
            float clickLp = 0f;

            float startFrequency = (spec.isTrap ? 122f : 96f) + spec.body * 52f;
            float endFrequency = 34f + spec.body * 18f;
            float pitchDuration = 0.12f + spec.releaseBias * 0.12f;
            float bodyDecay = 0.14f + spec.releaseBias * 0.18f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SampleRate;
                float pitchProgress = pitchDuration <= 0f ? 1f : Mathf.Clamp01(t / pitchDuration);
                float frequency = ExponentialLerp(startFrequency, endFrequency, pitchProgress);
                VoiceWaveform waveform = spec.transientSharpness > 0.58f
                    ? VoiceWaveform.Triangle
                    : spec.body > 0.62f ? VoiceWaveform.Sine : VoiceWaveform.Triangle;

                float body = SampleWave(waveform, phase) * Mathf.Exp(-t / Mathf.Max(0.04f, bodyDecay));
                phase = AdvancePhase(phase, frequency);

                float sub = spec.body > 0.52f
                    ? Mathf.Sin(Mathf.PI * 2f * (frequency * 0.5f) * t) * 0.18f * Mathf.Exp(-t / (bodyDecay * 1.6f))
                    : 0f;

                float click = 0f;
                if (t <= 0.03f)
                {
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    clickLp += 0.18f * (noise - clickLp);
                    float high = noise - clickLp;
                    float clickCut = 1800f + spec.transientSharpness * 5000f;
                    click = high * (0.12f + spec.transientSharpness * 0.2f) *
                        Mathf.Exp(-t / Mathf.Max(0.004f, 0.008f + 2000f / Mathf.Max(2000f, clickCut * 2f)));
                }

                float sample = body + sub + click;
                left[i] = sample;
                right[i] = sample;
            }
        }

        private static void RenderSnare(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float phase = 0f;
            float noiseCutoff = (spec.isTrap ? 1700f : 1100f) + spec.brightness * 2800f;
            float q = 0.7f + spec.resonance * 0.35f;
            float bodyFrequency = 180f + spec.body * 120f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SampleRate;
                float noiseEnv = EnvelopeDecay(t, 0.08f + spec.releaseBias * 0.14f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * noiseEnv;
                float filteredLeft = ProcessFilter(ref filterLeft, noise, noiseCutoff, q, VoiceFilterMode.HighPass);
                float filteredRight = ProcessFilter(ref filterRight, noise, noiseCutoff * 1.02f, q, VoiceFilterMode.HighPass);

                float sampleLeft = filteredLeft;
                float sampleRight = filteredRight;

                if (spec.body > 0.44f)
                {
                    float tone = SampleWave(VoiceWaveform.Triangle, phase) * 0.34f * EnvelopeDecay(t, 0.1f);
                    phase = AdvancePhase(phase, bodyFrequency);
                    sampleLeft += tone;
                    sampleRight += tone;
                }

                left[i] = sampleLeft;
                right[i] = sampleRight;
            }
        }

        private static void RenderHat(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = (spec.isDream ? 6200f : 7600f) + spec.brightness * 2400f;
            float q = 0.8f + spec.resonance * 0.4f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = EnvelopeDecay(t, 0.03f + spec.transientSharpness * 0.06f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float shimmer = Mathf.Sin(Mathf.PI * 2f * 8372f * t) * 0.15f
                              + Mathf.Sin(Mathf.PI * 2f * 10548f * t) * 0.1f;
                float source = (noise * 0.78f + shimmer) * env;
                left[i] = ProcessFilter(ref filterLeft, source, cutoff, q, VoiceFilterMode.HighPass);
                right[i] = ProcessFilter(ref filterRight, source, cutoff * 1.01f, q, VoiceFilterMode.HighPass);
            }
        }

        private static void RenderPercussion(ResolvedVoiceSpec spec, float[] left, float[] right, int seed)
        {
            var rng = new System.Random(seed);
            var filterLeft = new SvfState();
            var filterRight = new SvfState();
            float cutoff = 1000f + spec.body * 600f + spec.brightness * 2200f;
            float q = 0.8f + spec.resonance * 0.42f;
            float phase = 0f;
            float toneFrequency = 180f + spec.body * 220f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = EnvelopeDecay(t, 0.08f + spec.releaseBias * 0.12f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env;
                float tone = SampleWave(VoiceWaveform.Triangle, phase) * 0.28f * EnvelopeDecay(t, 0.11f);
                phase = AdvancePhase(phase, toneFrequency);

                float source = noise * 0.54f + tone;
                left[i] = ProcessFilter(ref filterLeft, source, cutoff, q, VoiceFilterMode.BandPass);
                right[i] = ProcessFilter(ref filterRight, source, cutoff * 1.03f, q, VoiceFilterMode.BandPass);
            }
        }

        private static void ApplyVoiceChain(ResolvedVoiceSpec spec, float[] left, float[] right)
        {
            var leftState = new SvfState();
            var rightState = new SvfState();
            float baseCutoff = (spec.patternType == PatternType.RhythmLoop
                    ? 500f
                    : spec.patternType == PatternType.HarmonyPad ? 320f : 700f)
                + (spec.positionBrightness * 0.4f + spec.brightness * 0.6f)
                * (spec.patternType == PatternType.HarmonyPad ? 4200f : 6400f);

            if (spec.isLoFi) baseCutoff *= 0.82f;
            if (spec.isTrap) baseCutoff *= 1.06f;
            if (spec.isDream) baseCutoff *= 0.94f;

            float resonanceQ = 0.18f + spec.resonance * 0.8f;
            float motionRate = spec.patternType == PatternType.RhythmLoop
                ? 3f + spec.modDepth * 10f
                : spec.patternType == PatternType.MelodyLine ? 1.2f + spec.modDepth * 7f
                : 0.25f + spec.modDepth * 2.2f;
            float motionDepth = spec.filterMotion > 0.08f
                ? 0.04f + spec.filterMotion * 0.18f
                : 0f;
            float driveAmount = 1f + spec.drive * 1.8f + (spec.isTrap ? 0.32f : spec.isLoFi ? 0.12f : 0f);
            float spreadOffset = 1f + spec.stereoSpread * 0.08f;

            for (int i = 0; i < left.Length; i++)
            {
                float t = (float)i / SampleRate;
                float motion = motionDepth > 0f
                    ? 1f + Mathf.Sin(Mathf.PI * 2f * motionRate * t) * motionDepth
                    : 1f;
                float cutoffLeft = Mathf.Clamp(baseCutoff * motion / spreadOffset, 90f, 12000f);
                float cutoffRight = Mathf.Clamp(baseCutoff * motion * spreadOffset, 90f, 12000f);

                float processedLeft = ProcessFilter(ref leftState, left[i], cutoffLeft, resonanceQ, spec.filterMode);
                float processedRight = ProcessFilter(ref rightState, right[i], cutoffRight, resonanceQ, spec.filterMode);

                left[i] = SoftClip(processedLeft * driveAmount);
                right[i] = SoftClip(processedRight * driveAmount);
            }
        }

        private static void ApplyAmbience(ResolvedVoiceSpec spec, float[] left, float[] right)
        {
            float delayMix = spec.fxSend * (spec.patternType == PatternType.RhythmLoop
                ? 0.04f + spec.delayBias * 0.08f
                : 0.07f + spec.delayBias * 0.14f);
            float bloomMix = spec.fxSend * (spec.patternType == PatternType.HarmonyPad
                ? 0.12f + spec.reverbBias * 0.2f
                : 0.05f + spec.reverbBias * 0.12f);

            if (delayMix <= 0.001f && bloomMix <= 0.001f)
                return;

            int shortDelay = SecondsToSamples(spec.patternType == PatternType.RhythmLoop ? 0.045f : 0.075f + spec.delayBias * 0.1f);
            int longDelay = SecondsToSamples(spec.patternType == PatternType.HarmonyPad ? 0.16f + spec.delayBias * 0.16f : 0.11f + spec.delayBias * 0.08f);
            int bloomA = SecondsToSamples(0.028f + spec.reverbBias * 0.02f);
            int bloomB = SecondsToSamples(0.061f + spec.reverbBias * 0.05f);
            int bloomC = SecondsToSamples(0.11f + spec.reverbBias * 0.08f);

            for (int i = 0; i < left.Length; i++)
            {
                if (delayMix > 0.001f)
                {
                    if (i + shortDelay < left.Length)
                    {
                        left[i + shortDelay] += left[i] * delayMix * 0.22f;
                        right[i + shortDelay] += right[i] * delayMix * 0.18f;
                    }
                    if (i + longDelay < left.Length)
                    {
                        left[i + longDelay] += right[i] * delayMix * 0.12f;
                        right[i + longDelay] += left[i] * delayMix * 0.12f;
                    }
                }

                if (bloomMix > 0.001f)
                {
                    if (i + bloomA < left.Length)
                    {
                        left[i + bloomA] += left[i] * bloomMix * 0.14f;
                        right[i + bloomA] += right[i] * bloomMix * 0.14f;
                    }
                    if (i + bloomB < left.Length)
                    {
                        left[i + bloomB] += right[i] * bloomMix * 0.09f;
                        right[i + bloomB] += left[i] * bloomMix * 0.09f;
                    }
                    if (i + bloomC < left.Length)
                    {
                        left[i + bloomC] += left[i] * bloomMix * 0.06f;
                        right[i + bloomC] += right[i] * bloomMix * 0.06f;
                    }
                }
            }
        }

        private static float GetAmbienceTail(ResolvedVoiceSpec spec)
        {
            return spec.fxSend * (0.08f + spec.reverbBias * 0.18f + spec.delayBias * 0.16f)
                + (spec.patternType == PatternType.HarmonyPad ? 0.12f : 0.04f);
        }

        private static float EnvelopeAtTime(ResolvedVoiceSpec spec, float time, float releaseStart)
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

        private static float EnvelopeDecay(float time, float decaySeconds)
        {
            return Mathf.Exp(-time / Mathf.Max(0.01f, decaySeconds));
        }

        private static float ProcessFilter(ref SvfState state, float input, float cutoffHz, float resonance, VoiceFilterMode mode)
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

        private static float SampleWave(VoiceWaveform waveform, float phase)
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

        private static float AdvancePhase(float phase, float frequency)
        {
            phase += frequency / SampleRate;
            if (phase >= 1f)
                phase -= Mathf.Floor(phase);
            return phase;
        }

        private static float ExponentialLerp(float start, float end, float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (start <= 0f || end <= 0f)
                return Mathf.Lerp(start, end, progress);

            return Mathf.Exp(Mathf.Lerp(Mathf.Log(start), Mathf.Log(end), progress));
        }

        private static float MidiToFrequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        private static void MixStereo(ref float left, ref float right, float sample, float pan)
        {
            pan = Mathf.Clamp(pan, -1f, 1f);
            float leftGain = Mathf.Sqrt(0.5f * (1f - pan));
            float rightGain = Mathf.Sqrt(0.5f * (1f + pan));
            left += sample * leftGain;
            right += sample * rightGain;
        }

        private static float SoftClip(float x)
        {
            return (float)Math.Tanh(x);
        }

        private static void NormalizeStereo(float[] left, float[] right)
        {
            float max = 0f;
            for (int i = 0; i < left.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(left[i]), Mathf.Abs(right[i]));
            }

            if (max <= 0.96f)
                return;

            float scale = 0.96f / max;
            for (int i = 0; i < left.Length; i++)
            {
                left[i] *= scale;
                right[i] *= scale;
            }
        }

        private static AudioClip BuildClip(string name, float[] left, float[] right)
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

        private static int SecondsToSamples(float seconds)
        {
            return Mathf.Max(64, Mathf.RoundToInt(seconds * SampleRate));
        }

        private static int ComputeSeed(string key)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < key.Length; i++)
                    hash = hash * 31 + key[i];
                return hash;
            }
        }

        private static SoundProfile CreateProfile(
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

        private struct SvfState
        {
            public float low;
            public float band;
        }
    }

}
