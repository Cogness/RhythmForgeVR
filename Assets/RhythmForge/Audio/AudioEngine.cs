using System.Collections.Generic;
using UnityEngine;
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

        public void PlayDrumEvent(string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile)
        {
            PlayDrumEvent(InstrumentPresets.Get("lofi-drums"), lane, velocity, pan, brightness, depth, fxSend, soundProfile);
        }

        public void PlayDrumEvent(InstrumentPreset preset, string lane, float velocity, float pan, float brightness,
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

        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile)
        {
            PlayDrumEvent(preset, lane, velocity, pan, brightness, depth, fxSend, soundProfile);
        }

        public void PlayMelodyNote(int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f)
        {
            PlayMelodyNote(InstrumentPresets.Get("lofi-piano"), midi, velocity, duration,
                pan, brightness, depth, fxSend, soundProfile, glide);
        }

        public void PlayMelodyNote(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f)
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
                glide);
        }

        public void PlayMelody(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f)
        {
            PlayMelodyNote(preset, midi, velocity, duration, pan, brightness, depth, fxSend, soundProfile, glide);
        }

        public void PlayHarmonyChord(List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile)
        {
            PlayHarmonyChord(InstrumentPresets.Get("lofi-pad"), chord, velocity, duration,
                pan, brightness, depth, fxSend, soundProfile);
        }

        public void PlayHarmonyChord(InstrumentPreset preset, List<int> chord, float velocity,
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

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile)
        {
            PlayHarmonyChord(preset, chord, velocity, duration, pan, brightness, depth, fxSend, soundProfile);
        }
    }
}
