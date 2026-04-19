using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Dispatches sequencer events into the runtime procedural voice renderer.
    /// </summary>
    public class AudioEngine : MonoBehaviour, IAudioDispatcher
    {
        [SerializeField] private SamplePlayer _samplePlayer;
        [SerializeField] private bool _enableSpatialRouting = true;

        [Header("Master Settings")]
        [SerializeField] [Range(0f, 1f)] private float _masterVolume = 0.82f;

        private InstanceVoiceRegistry _instanceVoiceRegistry;

        public event Action<string> OnEventScheduled;

        public bool IsReady => _samplePlayer != null;

        private void Awake()
        {
            _instanceVoiceRegistry = InstanceVoiceRegistry.GetShared();
        }

        public void Configure(SamplePlayer samplePlayer)
        {
            _samplePlayer = samplePlayer;
            _instanceVoiceRegistry = InstanceVoiceRegistry.GetShared();
            _instanceVoiceRegistry.SetMixerGroup(_samplePlayer != null ? _samplePlayer.ActiveMixerGroup : null);
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
            _instanceVoiceRegistry = InstanceVoiceRegistry.GetShared();
            _instanceVoiceRegistry.SetMixerGroup(_samplePlayer != null ? _samplePlayer.ActiveMixerGroup : null);
        }

        /// <summary>
        /// Re-routes all pooled AudioSources to the submix group matching <paramref name="genreId"/>.
        /// Call this whenever the active genre changes at runtime.
        /// </summary>
        public void SetGenre(string genreId)
        {
            _samplePlayer?.SetMixerGroup(genreId);
            _instanceVoiceRegistry?.SetMixerGroup(_samplePlayer != null ? _samplePlayer.ActiveMixerGroup : null);
        }

        // Convenience overload — uses default lofi-drums preset.
        public void PlayDrum(string lane, float velocity, float gainTrim, float brightness,
            float reverbSend, float delaySend, SoundProfile soundProfile, string instanceId = null)
        {
            PlayDrum(InstrumentPresets.Get("lofi-drums"), lane, velocity, gainTrim, brightness, reverbSend, delaySend, soundProfile, instanceId);
        }

        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float gainTrim, float brightness,
            float reverbSend, float delaySend, SoundProfile soundProfile, string instanceId = null)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();
            if (!ApplyZoneBias(instanceId, ref gainTrim, ref reverbSend, ref delaySend))
                return;

            float gainAmount = Mathf.Clamp01(
                gainTrim * velocity * velocity * (0.72f + soundProfile.body * 0.42f));

            var spec = VoiceSpecResolver.ResolveDrum(lane, preset, soundProfile, brightness, reverbSend, delaySend);
            PlayResolved(spec, gainAmount, instanceId);
            RaiseEventScheduled(instanceId);
        }

        // Convenience overload — uses default lofi-piano preset.
        public void PlayMelody(int midi, float velocity, float duration,
            float gainTrim, float brightness, float reverbSend, float delaySend,
            SoundProfile soundProfile, float glide = 0f, string instanceId = null)
        {
            PlayMelody(InstrumentPresets.Get("lofi-piano"), midi, velocity, duration,
                gainTrim, brightness, reverbSend, delaySend, soundProfile, glide, instanceId);
        }

        public void PlayMelody(InstrumentPreset preset, int midi, float velocity, float duration,
            float gainTrim, float brightness, float reverbSend, float delaySend,
            SoundProfile soundProfile, float glide = 0f, string instanceId = null)
        {
            if (!IsReady) return;
            soundProfile = soundProfile ?? new SoundProfile();
            if (!ApplyZoneBias(instanceId, ref gainTrim, ref reverbSend, ref delaySend))
                return;

            float gainAmount = Mathf.Clamp01(
                gainTrim * velocity * velocity * (0.72f + soundProfile.body * 0.32f));

            var spec = VoiceSpecResolver.ResolveMelody(
                preset,
                soundProfile,
                midi,
                duration,
                brightness,
                reverbSend,
                delaySend,
                glide);
            PlayResolved(spec, gainAmount * 0.72f, instanceId);
            RaiseEventScheduled(instanceId);
        }

        // Convenience overload — uses default lofi-pad preset.
        public void PlayChord(List<int> chord, float velocity,
            float duration, float gainTrim, float brightness, float reverbSend,
            float delaySend, SoundProfile soundProfile, string instanceId = null)
        {
            PlayChord(InstrumentPresets.Get("lofi-pad"), chord, velocity, duration,
                gainTrim, brightness, reverbSend, delaySend, soundProfile, instanceId);
        }

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity,
            float duration, float gainTrim, float brightness, float reverbSend,
            float delaySend, SoundProfile soundProfile, string instanceId = null)
        {
            if (!IsReady) return;
            if (chord == null) return;
            soundProfile = soundProfile ?? new SoundProfile();
            if (!ApplyZoneBias(instanceId, ref gainTrim, ref reverbSend, ref delaySend))
                return;

            for (int i = 0; i < chord.Count; i++)
            {
                float noteVelocity = velocity * (i == 0 ? 0.9f : 0.72f);
                float noteGain = Mathf.Clamp01(
                    gainTrim * noteVelocity * noteVelocity * (0.72f + soundProfile.body * 0.32f) * 0.66f);
                var spec = VoiceSpecResolver.ResolveHarmony(
                    preset,
                    soundProfile,
                    chord[i],
                    duration,
                    brightness,
                    reverbSend,
                    delaySend);
                PlayResolved(spec, noteGain, instanceId, i * 0.01f);
            }

            RaiseEventScheduled(instanceId);
        }

        private void PlayResolved(ResolvedVoiceSpec spec, float gainAmount, string instanceId, float startDelay = 0f)
        {
            float volume = Mathf.Clamp01(gainAmount * _masterVolume);
            if (_enableSpatialRouting &&
                !string.IsNullOrEmpty(instanceId) &&
                _instanceVoiceRegistry != null &&
                _instanceVoiceRegistry.TryGetPool(instanceId, out _))
            {
                _samplePlayer.RequestClip(spec, clip => PlaySpatialClip(instanceId, clip, volume, startDelay));
                return;
            }

            _samplePlayer.PlayResolved(spec, volume, 0f, startDelay);
        }

        private void PlaySpatialClip(string instanceId, AudioClip clip, float volume, float startDelay)
        {
            if (!_enableSpatialRouting || _instanceVoiceRegistry == null)
                return;

            if (_instanceVoiceRegistry.TryGetPool(instanceId, out var pool))
                pool.Play(clip, volume, startDelay);
        }

        private static bool ApplyZoneBias(string instanceId, ref float gainTrim, ref float reverbSend, ref float delaySend)
        {
            if (string.IsNullOrEmpty(instanceId))
                return true;

            var zone = SpatialZoneController.Shared?.GetZoneFor(instanceId);
            if (zone == null)
                return true;

            SpatialZoneController.Shared.GetLiveBiases(zone.id, out var liveGainMult, out var liveReverbBoost, out var cutActive);
            if (cutActive)
                return false;

            reverbSend = Mathf.Clamp01(reverbSend + zone.reverbBias);
            delaySend = Mathf.Clamp01(delaySend + zone.delayBias);
            gainTrim = Mathf.Clamp(gainTrim * Mathf.Max(0.01f, zone.gainBias), 0.5f, 1.25f);
            reverbSend = Mathf.Clamp01(reverbSend + liveReverbBoost);
            gainTrim = Mathf.Clamp(gainTrim * liveGainMult, 0.5f, 1.25f);
            return true;
        }

        private void RaiseEventScheduled(string instanceId)
        {
            if (!string.IsNullOrEmpty(instanceId))
                OnEventScheduled?.Invoke(instanceId);
        }
    }
}
