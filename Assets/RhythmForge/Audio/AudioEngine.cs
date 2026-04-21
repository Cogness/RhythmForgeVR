using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Dispatches sequencer events into the runtime procedural voice renderer.
    /// </summary>
    public class AudioEngine : MonoBehaviour, IAudioDispatcher
    {
        [SerializeField] private SamplePlayer _samplePlayer;

        [Header("Master Settings")]
        [SerializeField] [Range(0f, 1f)] private float _masterVolume = 0.82f;

        public bool IsReady => _samplePlayer != null;

        public void Configure(SamplePlayer samplePlayer)
        {
            _samplePlayer = samplePlayer;
        }

        /// <summary>
        /// Extended configure: also injects the AudioMixer so the SamplePlayer can route
        /// pooled AudioSources to the correct genre submix group.
        /// </summary>
        public void Configure(SamplePlayer samplePlayer, AudioMixer mixer)
        {
            _samplePlayer = samplePlayer;
            if (mixer != null)
                _samplePlayer.Configure(mixer);
        }

        /// <summary>
        /// Re-routes all pooled AudioSources to the submix group matching <paramref name="genreId"/>.
        /// Call this whenever the active genre changes at runtime.
        /// </summary>
        public void SetGenre(string genreId)
        {
            _samplePlayer?.SetMixerGroup(genreId);
        }

        // Convenience overload — uses default lofi-drums preset.
        public void PlayDrum(string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile)
        {
            PlayDrum(InstrumentPresets.Get("lofi-drums"), lane, velocity, pan, brightness, depth, fxSend, soundProfile);
        }

        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.45f) * velocity * (0.72f + soundProfile.body * 0.42f));

            _samplePlayer.PlayDrum(
                preset,
                lane,
                velocity,
                pan,
                brightness,
                gainAmount * _masterVolume,
                fxSend,
                soundProfile);
        }

        // Convenience overload — uses default lofi-piano preset.
        public void PlayMelody(int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f, float startDelay = 0f)
        {
            PlayMelody(InstrumentPresets.Get("lofi-piano"), midi, velocity, duration,
                pan, brightness, depth, fxSend, soundProfile, glide, startDelay);
        }

        public void PlayMelody(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f, float startDelay = 0f)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.4f) * velocity * (0.72f + soundProfile.body * 0.32f));

            _samplePlayer.PlayNote(
                preset,
                midi,
                velocity,
                duration,
                pan,
                brightness,
                gainAmount * _masterVolume,
                fxSend,
                soundProfile,
                PatternType.MelodyLine,
                glide,
                startDelay);
        }

        // Convenience overload — uses default lofi-pad preset.
        public void PlayChord(List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile)
        {
            PlayChord(InstrumentPresets.Get("lofi-pad"), chord, velocity, duration,
                pan, brightness, depth, fxSend, soundProfile);
        }

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.4f) * velocity * (0.72f + soundProfile.body * 0.32f));

            _samplePlayer.PlayChord(
                preset,
                chord,
                velocity,
                duration,
                pan,
                brightness,
                gainAmount * _masterVolume,
                fxSend,
                soundProfile);
        }
    }
}
