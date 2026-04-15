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
        public string genreId;

        /// <summary>
        /// For the "perc" drum lane only: MIDI root to tune the tonal body to.
        /// 0 = no harmonic context available, use neutral frequency.
        /// Baked at resolve time so the cache key changes with the key root.
        /// </summary>
        public int percTuningMidi;

        public bool isNewAge => genreId == "newage";
        public bool isJazz   => genreId == "jazz";
        public bool isElectronic => string.IsNullOrEmpty(genreId) || genreId == "electronic";

        public string GetCacheKey()
        {
            return string.Join("|",
                patternType,
                voiceType ?? string.Empty,
                lane ?? string.Empty,
                genreId ?? string.Empty,
                midi,
                percTuningMidi,
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
}
