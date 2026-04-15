using System.Text;
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

        [System.ThreadStatic]
        private static StringBuilder _keyBuilder;

        public string GetCacheKey()
        {
            if (_keyBuilder == null)
                _keyBuilder = new StringBuilder(128);

            var sb = _keyBuilder;
            sb.Clear();

            sb.Append((int)patternType);  sb.Append('|');
            sb.Append(voiceType ?? string.Empty);  sb.Append('|');
            sb.Append(lane ?? string.Empty);        sb.Append('|');
            sb.Append(genreId ?? string.Empty);     sb.Append('|');
            sb.Append(midi);                        sb.Append('|');
            sb.Append(percTuningMidi);              sb.Append('|');
            sb.Append(Quantize(durationSeconds, 24f));      sb.Append('|');
            sb.Append(Quantize(glide + 2f, 12f));           sb.Append('|');
            sb.Append(Quantize(positionBrightness, 12f));   sb.Append('|');
            sb.Append(Quantize(brightness, 12f));           sb.Append('|');
            sb.Append(Quantize(body, 12f));                 sb.Append('|');
            sb.Append(Quantize(drive, 12f));                sb.Append('|');
            sb.Append(Quantize(resonance, 12f));            sb.Append('|');
            sb.Append(Quantize(filterMotion, 12f));         sb.Append('|');
            sb.Append(Quantize(modDepth, 12f));             sb.Append('|');
            sb.Append(Quantize(detune, 12f));               sb.Append('|');
            sb.Append(Quantize(stereoSpread, 12f));         sb.Append('|');
            sb.Append(Quantize(transientSharpness, 12f));   sb.Append('|');
            sb.Append(Quantize(releaseBias, 12f));          sb.Append('|');
            sb.Append(Quantize(attackBias, 12f));           sb.Append('|');
            sb.Append(Quantize(waveMorph, 12f));            sb.Append('|');
            sb.Append(Quantize(delayBias, 12f));            sb.Append('|');
            sb.Append(Quantize(reverbBias, 12f));           sb.Append('|');
            sb.Append(Quantize(fxSend, 16f));               sb.Append('|');
            sb.Append(Quantize(attackSeconds, 40f));        sb.Append('|');
            sb.Append(Quantize(releaseSeconds, 20f));       sb.Append('|');
            sb.Append((int)waveA);  sb.Append('|');
            sb.Append((int)waveB);  sb.Append('|');
            sb.Append((int)filterMode);

            return sb.ToString();
        }

        private static int Quantize(float value, float scale)
        {
            return Mathf.RoundToInt(value * scale);
        }
    }
}
