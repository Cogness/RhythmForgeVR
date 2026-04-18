using UnityEngine;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Plays a faint pressure-driven sine tone while the user draws,
    /// giving zero-latency audio confirmation that the pen is being read.
    /// </summary>
    public class PenSidetoneSource : MonoBehaviour
    {
        private AudioSource _source;
        private bool _isDrawing;

        // Frequency per pattern type (Hz)
        private const float FreqRhythm  = 110f;   // A2 — low thud feel
        private const float FreqMelody  = 330f;   // E4 — mid singing feel
        private const float FreqHarmony = 220f;   // A3 — pad warmth

        private const float MaxVolume  = 0.12f;   // subtle, not intrusive
        private const int   SampleRate = 22050;
        private const float ClipLength = 0.25f;   // seconds (loops seamlessly)

        public void Initialize()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.loop        = true;
            _source.spatialBlend = 0f;   // 2D — it's the user's own hand
            _source.volume      = 0f;
            _source.playOnAwake = false;
            _source.clip        = BuildSineClip(FreqMelody); // default; swapped on StartDraw
        }

        public void StartDraw(PatternType type)
        {
            _source.clip = BuildSineClip(FreqForType(type));
            _source.volume = 0f;
            _source.Play();
            _isDrawing = true;
        }

        public void UpdatePressure(float pressure)
        {
            if (!_isDrawing) return;
            _source.volume = pressure * MaxVolume;
        }

        public void StopDraw()
        {
            _isDrawing = false;
            _source.Stop();
            _source.volume = 0f;
        }

        private static float FreqForType(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop:  return FreqRhythm;
                case PatternType.MelodyLine:  return FreqMelody;
                case PatternType.HarmonyPad:  return FreqHarmony;
                default:                      return FreqMelody;
            }
        }

        private static AudioClip BuildSineClip(float frequency)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * ClipLength);
            // Snap to exact wavelength for seamless looping
            float period    = SampleRate / frequency;
            sampleCount     = Mathf.RoundToInt(sampleCount / period) * Mathf.RoundToInt(period);
            sampleCount     = Mathf.Max(sampleCount, Mathf.RoundToInt(period));

            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / SampleRate) * 0.5f;

            var clip = AudioClip.Create($"Sidetone_{frequency:F0}Hz", sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
