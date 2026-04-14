using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
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
            if (preset?.envelope != null)
                presetAttack = preset.envelope.attack;

            float maxAttack = spec.patternType == PatternType.HarmonyPad ? 1.6f : 0.24f;
            return Mathf.Clamp(presetAttack * (0.35f + spec.attackBias * 1.6f), 0.003f, maxAttack);
        }

        private static float ResolveReleaseSeconds(ResolvedVoiceSpec spec)
        {
            float presetRelease = 0.35f;
            var preset = InstrumentPresets.GetDefaultForVoice(spec.voiceType);
            if (preset?.envelope != null)
                presetRelease = preset.envelope.release;

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
}
