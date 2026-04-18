using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Sequencing;

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
            return ResolveDrum(lane, preset, profile, positionBrightness, fxSend, fxSend);
        }

        public static ResolvedVoiceSpec ResolveDrum(
            string lane,
            InstrumentPreset preset,
            SoundProfile profile,
            float positionBrightness,
            float reverbSend,
            float delaySend)
        {
            var spec = CreateBaseSpec(PatternType.RhythmLoop, preset, profile, positionBrightness, reverbSend, delaySend);
            spec.lane = lane ?? "perc";
            spec.durationSeconds = spec.lane == "kick"
                ? 0.34f
                : spec.lane == "snare" ? 0.24f
                : spec.lane == "perc" ? 0.18f : 0.10f;
            spec.attackSeconds = 0.002f + spec.attackBias * 0.004f;
            spec.releaseSeconds = 0.05f + spec.releaseBias * 0.22f;
            spec.useOscillatorB = false;
            if (spec.lane == "perc")
            {
                var harmCtx = RhythmForge.Core.Sequencing.HarmonicContextProvider.Current;
                spec.percTuningMidi = harmCtx.HasChord ? harmCtx.rootMidi : 0;
            }
            return spec;
        }

        // Per-mode gain offsets (in linear amplitude): harmony sits under melody, melody under rhythm.
        // NewAge: harmony −5 dB (0.562), melody −2 dB (0.794). Other genres: −4 dB / −1 dB.
        private static float ModeGainLinear(PatternType type, bool isNewAge)
        {
            switch (type)
            {
                case PatternType.HarmonyPad:  return isNewAge ? 0.562f : 0.631f;
                case PatternType.MelodyLine:  return isNewAge ? 0.794f : 0.891f;
                default:                       return 1.0f;
            }
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
            return ResolveMelody(preset, profile, midi, duration, positionBrightness, fxSend, fxSend, glide);
        }

        public static ResolvedVoiceSpec ResolveMelody(
            InstrumentPreset preset,
            SoundProfile profile,
            int midi,
            float duration,
            float positionBrightness,
            float reverbSend,
            float delaySend,
            float glide = 0f)
        {
            var spec = CreateBaseSpec(PatternType.MelodyLine, preset, profile, positionBrightness, reverbSend, delaySend);
            spec.midi = midi;
            spec.durationSeconds = QuantizeDuration(duration, 0.04f, 0.08f, 1.6f);
            spec.glide = QuantizeSigned(glide, 20f);
            ResolveTonalDetails(ref spec);
            spec.velocityScale = ModeGainLinear(PatternType.MelodyLine, spec.isNewAge);
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
            return ResolveHarmony(preset, profile, midi, duration, positionBrightness, fxSend, fxSend);
        }

        public static ResolvedVoiceSpec ResolveHarmony(
            InstrumentPreset preset,
            SoundProfile profile,
            int midi,
            float duration,
            float positionBrightness,
            float reverbSend,
            float delaySend)
        {
            var spec = CreateBaseSpec(PatternType.HarmonyPad, preset, profile, positionBrightness, reverbSend, delaySend);
            spec.midi = midi;
            spec.durationSeconds = QuantizeDuration(duration, 0.08f, 0.12f, 4.6f);
            spec.glide = 0f;
            ResolveTonalDetails(ref spec);
            spec.velocityScale = ModeGainLinear(PatternType.HarmonyPad, spec.isNewAge);
            // §4c: role-1+ pads are narrower in the stereo field so role-0 owns the wide image
            int padRoleIdx = ShapeRoleProvider.Current.index;
            spec.chorusWidthScale = padRoleIdx > 0 ? 1f / (1f + padRoleIdx) : 1f;
            return spec;
        }

        private static ResolvedVoiceSpec CreateBaseSpec(
            PatternType type,
            InstrumentPreset preset,
            SoundProfile profile,
            float positionBrightness,
            float reverbSend,
            float delaySend)
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
                reverbSend = QuantizeReverbSend(preset.fxSend + reverbSend),
                delaySend = QuantizeDelaySend(preset.fxSend + delaySend),
                isBass = voiceDescriptor.Contains("bass"),
                isBell = voiceDescriptor.Contains("bell"),
                isLoFi = familyIdentity.Contains("lofi"),
                isTrap = familyIdentity.Contains("trap"),
                isDream = familyIdentity.Contains("dream"),
                genreId = GenreRegistry.GetActive().Id
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
            // New Age: always use soft sine/triangle — no harsh waveforms
            if (spec.isNewAge)
            {
                spec.waveA = spec.body > 0.55f ? VoiceWaveform.Triangle : VoiceWaveform.Sine;
                spec.waveB = VoiceWaveform.Sine;
                return;
            }

            // Jazz: warm triangle/sine — avoid square/saw for organic Rhodes feel
            if (spec.isJazz)
            {
                spec.waveA = VoiceWaveform.Triangle;
                spec.waveB = spec.isBell ? VoiceWaveform.Sine : VoiceWaveform.Triangle;
                return;
            }

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

        private static float QuantizeReverbSend(float value)
        {
            value = Mathf.Clamp01(value);
            if (value < 0.17f)
                return 0f;
            if (value < 0.67f)
                return 0.5f;
            return 1f;
        }

        private static float QuantizeDelaySend(float value)
        {
            return Mathf.Round(Mathf.Clamp01(value) * 4f) / 4f;
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
