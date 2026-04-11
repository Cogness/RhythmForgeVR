using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Analysis
{
    public static class PresetBiasResolver
    {
        public static SoundProfile GetPresetBias(InstrumentPreset preset, PatternType type)
        {
            var b = new SoundProfile
            {
                brightness = 0.28f,
                resonance = 0.24f,
                drive = 0.18f,
                attackBias = 0.28f,
                releaseBias = 0.32f,
                detune = 0.16f,
                modDepth = 0.2f,
                stereoSpread = 0.2f,
                grooveInstability = 0.14f,
                delayBias = 0.14f,
                reverbBias = 0.18f,
                waveMorph = 0.24f,
                filterMotion = 0.18f,
                transientSharpness = 0.28f,
                body = 0.28f
            };

            string voice = preset.voiceType;

            if (voice.Contains("trap"))
            {
                b.brightness += 0.18f;
                b.drive += 0.2f;
                b.transientSharpness += 0.16f;
                b.delayBias += 0.08f;
                b.releaseBias -= 0.08f;
            }
            if (voice.Contains("dream"))
            {
                b.reverbBias += 0.24f;
                b.modDepth += 0.18f;
                b.stereoSpread += 0.18f;
                b.releaseBias += 0.2f;
            }
            if (voice.Contains("bass"))
            {
                b.body += 0.32f;
                b.brightness -= 0.14f;
                b.detune -= 0.08f;
                b.attackBias += 0.08f;
            }
            if (voice.Contains("pad"))
            {
                b.releaseBias += 0.24f;
                b.reverbBias += 0.2f;
                b.stereoSpread += 0.12f;
                b.body += 0.1f;
            }
            if (voice.Contains("bell"))
            {
                b.brightness += 0.24f;
                b.transientSharpness += 0.18f;
                b.resonance += 0.12f;
                b.delayBias += 0.1f;
            }
            if (voice.Contains("piano"))
            {
                b.body += 0.12f;
                b.attackBias += 0.08f;
                b.waveMorph -= 0.04f;
            }
            if (voice.Contains("drums") || type == PatternType.RhythmLoop)
            {
                b.transientSharpness += 0.16f;
                b.grooveInstability += 0.08f;
                b.body += 0.1f;
            }
            if (voice.Contains("perc"))
            {
                b.brightness += 0.08f;
                b.transientSharpness += 0.08f;
            }

            // Clamp all values
            b.brightness = Mathf.Clamp01(b.brightness);
            b.resonance = Mathf.Clamp01(b.resonance);
            b.drive = Mathf.Clamp01(b.drive);
            b.attackBias = Mathf.Clamp01(b.attackBias);
            b.releaseBias = Mathf.Clamp01(b.releaseBias);
            b.detune = Mathf.Clamp01(b.detune);
            b.modDepth = Mathf.Clamp01(b.modDepth);
            b.stereoSpread = Mathf.Clamp01(b.stereoSpread);
            b.grooveInstability = Mathf.Clamp01(b.grooveInstability);
            b.delayBias = Mathf.Clamp01(b.delayBias);
            b.reverbBias = Mathf.Clamp01(b.reverbBias);
            b.waveMorph = Mathf.Clamp01(b.waveMorph);
            b.filterMotion = Mathf.Clamp01(b.filterMotion);
            b.transientSharpness = Mathf.Clamp01(b.transientSharpness);
            b.body = Mathf.Clamp01(b.body);

            return b;
        }

        /// <summary>
        /// Blends geometry-derived sound profile with preset bias.
        /// effective = geometry * 0.78 + presetBias * 0.42, clamped to [0,1]
        /// </summary>
        public static SoundProfile ResolveEffective(SoundProfile geometry, SoundProfile presetBias)
        {
            return new SoundProfile
            {
                brightness = Mathf.Clamp01(geometry.brightness * 0.78f + presetBias.brightness * 0.42f),
                resonance = Mathf.Clamp01(geometry.resonance * 0.78f + presetBias.resonance * 0.42f),
                drive = Mathf.Clamp01(geometry.drive * 0.78f + presetBias.drive * 0.42f),
                attackBias = Mathf.Clamp01(geometry.attackBias * 0.78f + presetBias.attackBias * 0.42f),
                releaseBias = Mathf.Clamp01(geometry.releaseBias * 0.78f + presetBias.releaseBias * 0.42f),
                detune = Mathf.Clamp01(geometry.detune * 0.78f + presetBias.detune * 0.42f),
                modDepth = Mathf.Clamp01(geometry.modDepth * 0.78f + presetBias.modDepth * 0.42f),
                stereoSpread = Mathf.Clamp01(geometry.stereoSpread * 0.78f + presetBias.stereoSpread * 0.42f),
                grooveInstability = Mathf.Clamp01(geometry.grooveInstability * 0.78f + presetBias.grooveInstability * 0.42f),
                delayBias = Mathf.Clamp01(geometry.delayBias * 0.78f + presetBias.delayBias * 0.42f),
                reverbBias = Mathf.Clamp01(geometry.reverbBias * 0.78f + presetBias.reverbBias * 0.42f),
                waveMorph = Mathf.Clamp01(geometry.waveMorph * 0.78f + presetBias.waveMorph * 0.42f),
                filterMotion = Mathf.Clamp01(geometry.filterMotion * 0.78f + presetBias.filterMotion * 0.42f),
                transientSharpness = Mathf.Clamp01(geometry.transientSharpness * 0.78f + presetBias.transientSharpness * 0.42f),
                body = Mathf.Clamp01(geometry.body * 0.78f + presetBias.body * 0.42f)
            };
        }

        public static string SummarizeShapeDNA(PatternType type, ShapeProfile sp, SoundProfile sound)
        {
            string shapeWord = sp.angularity > 0.7f ? "spiky" : sp.angularity > 0.45f ? "creased" : "smooth";
            string balanceWord = sp.symmetry > 0.65f ? "balanced" : sp.symmetry < 0.35f ? "off-balance" : "tilted";

            switch (type)
            {
                case PatternType.RhythmLoop:
                    string bodyWord = sound.body > 0.65f ? "heavy body"
                        : sound.transientSharpness > 0.68f ? "hard transient edge" : "tight body";
                    return $"{shapeWord} {balanceWord} loop with {bodyWord}";

                case PatternType.MelodyLine:
                    string motionWord = sound.modDepth > 0.6f ? "singing modulation"
                        : sound.transientSharpness > 0.6f ? "biting attack" : "soft contour";
                    string registerWord = sp.verticalSpan > 0.55f ? "wide register reach" : "contained register";
                    return $"{shapeWord} line with {motionWord} and {registerWord}";

                default:
                    string bloomWord = sound.reverbBias > 0.6f ? "wide bloom"
                        : sound.filterMotion > 0.6f ? "moving filter color" : "steady bed";
                    return $"{shapeWord} pad with {bloomWord} and {balanceWord} voicing";
            }
        }
    }
}
