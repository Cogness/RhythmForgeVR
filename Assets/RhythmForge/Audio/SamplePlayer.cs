using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Placeholder audio player using AudioClip samples.
    /// Manages a pool of AudioSources for polyphonic playback.
    /// </summary>
    public class SamplePlayer : MonoBehaviour
    {
        [Header("Drum Samples")]
        [SerializeField] private AudioClip _kickClip;
        [SerializeField] private AudioClip _snareClip;
        [SerializeField] private AudioClip _hatClip;
        [SerializeField] private AudioClip _percClip;

        [Header("Tonal Sample")]
        [SerializeField] private AudioClip _toneClip; // C4 reference tone

        [Header("Pool Settings")]
        [SerializeField] private int _poolSize = 16;

        private List<AudioSource> _pool = new List<AudioSource>();
        private int _nextPoolIndex;

        /// <summary>Called by RhythmForgeBootstrapper to inject synthesized clips at runtime.</summary>
        public void Configure(AudioClip kick, AudioClip snare, AudioClip hat,
            AudioClip perc, AudioClip tone)
        {
            _kickClip  = kick;
            _snareClip = snare;
            _hatClip   = hat;
            _percClip  = perc;
            _toneClip  = tone;
        }

        private void Awake()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"VoicePool_{i}");
                go.transform.SetParent(transform);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialize = false;
                source.loop = false;
                _pool.Add(source);
            }
        }

        private AudioSource GetNextSource()
        {
            var source = _pool[_nextPoolIndex];
            _nextPoolIndex = (_nextPoolIndex + 1) % _pool.Count;

            // If the source is still playing, stop it
            if (source.isPlaying)
                source.Stop();

            return source;
        }

        public void PlayDrum(string lane, float velocity, float pan, float gain, SoundProfile profile)
        {
            AudioClip clip = GetDrumClip(lane);
            if (clip == null) return;

            var source = GetNextSource();
            source.clip = clip;
            source.volume = Mathf.Clamp01(velocity * gain);
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);

            // Slight pitch variation driven by sound profile
            float pitchVariation = 1f + (profile.brightness - 0.5f) * 0.15f;
            source.pitch = Mathf.Clamp(pitchVariation, 0.8f, 1.3f);

            source.Play();
        }

        public void PlayNote(int midi, float velocity, float duration, float pan, float gain, SoundProfile profile)
        {
            if (_toneClip == null) return;

            var source = GetNextSource();
            source.clip = _toneClip;
            source.volume = Mathf.Clamp01(velocity * gain * 0.7f);
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);

            // Pitch the C4 sample to target MIDI note
            // C4 = MIDI 60, so semitone offset = midi - 60
            float semitones = midi - 60f;
            source.pitch = Mathf.Pow(2f, semitones / 12f);

            source.Play();
        }

        public void PlayChord(List<int> chord, float velocity, float duration, float pan, float gain, SoundProfile profile)
        {
            if (chord == null) return;

            for (int i = 0; i < chord.Count; i++)
            {
                float chordPan = Mathf.Clamp(pan + (i - 1.5f) * 0.12f, -1f, 1f);
                float chordVel = velocity * (i == 0 ? 0.9f : 0.72f);
                PlayNote(chord[i], chordVel, duration, chordPan, gain, profile);
            }
        }

        private AudioClip GetDrumClip(string lane)
        {
            switch (lane)
            {
                case "kick": return _kickClip;
                case "snare": return _snareClip;
                case "hat": return _hatClip;
                case "perc": return _percClip;
                default: return _percClip;
            }
        }
    }
}
