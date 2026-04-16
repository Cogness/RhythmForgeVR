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

        private Dictionary<string, InstanceVoicePool> _instancePools = new Dictionary<string, InstanceVoicePool>();

        public bool IsReady => _samplePlayer != null;

        public void Configure(SamplePlayer samplePlayer)
        {
            _samplePlayer = samplePlayer;
        }

        public void RegisterInstancePool(string instanceId, InstanceVoicePool pool)
        {
            _instancePools[instanceId] = pool;
        }

        public void UnregisterInstancePool(string instanceId)
        {
            _instancePools.Remove(instanceId);
        }

        // Convenience overload — uses default lofi-drums preset.
        public void PlayDrum(string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile, string instanceId = null)
        {
            PlayDrum(InstrumentPresets.Get("lofi-drums"), lane, velocity, pan, brightness, depth, fxSend, soundProfile, instanceId);
        }

        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile, string instanceId = null)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.45f) * velocity * (0.72f + soundProfile.body * 0.42f));

            if (instanceId != null && _instancePools.TryGetValue(instanceId, out var drumPool))
            {
                drumPool.PlayDrum(preset, lane, velocity, pan, brightness, gainAmount * _masterVolume, fxSend, soundProfile);
                return;
            }

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
            SoundProfile soundProfile, float glide = 0f, string instanceId = null)
        {
            PlayMelody(InstrumentPresets.Get("lofi-piano"), midi, velocity, duration,
                pan, brightness, depth, fxSend, soundProfile, glide, instanceId);
        }

        public void PlayMelody(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f, string instanceId = null)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.4f) * velocity * (0.72f + soundProfile.body * 0.32f));

            if (instanceId != null && _instancePools.TryGetValue(instanceId, out var melodyPool))
            {
                melodyPool.PlayNote(preset, midi, velocity, duration, pan, brightness, gainAmount * _masterVolume, fxSend, soundProfile, PatternType.MelodyLine, glide);
                return;
            }

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

        // Convenience overload — uses default lofi-pad preset.
        public void PlayChord(List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile, string instanceId = null)
        {
            PlayChord(InstrumentPresets.Get("lofi-pad"), chord, velocity, duration,
                pan, brightness, depth, fxSend, soundProfile, instanceId);
        }

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile, string instanceId = null)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.4f) * velocity * (0.72f + soundProfile.body * 0.32f));

            if (instanceId != null && _instancePools.TryGetValue(instanceId, out var chordPool))
            {
                chordPool.PlayChord(preset, chord, velocity, duration, pan, brightness, gainAmount * _masterVolume, fxSend, soundProfile);
                return;
            }

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
