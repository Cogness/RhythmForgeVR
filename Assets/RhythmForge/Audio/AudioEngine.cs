using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Placeholder audio engine that dispatches playback events to a SamplePlayer.
    /// Will be replaced with full synthesis (FMOD/Unity DSP) in a future phase.
    /// </summary>
    public class AudioEngine : MonoBehaviour
    {
        [SerializeField] private SamplePlayer _samplePlayer;

        [Header("Master Settings")]
        [SerializeField] [Range(0f, 1f)] private float _masterVolume = 0.82f;

        public bool IsReady => _samplePlayer != null;

        public void PlayDrumEvent(string lane, float velocity, float pan, float brightness,
            float depth, float fxSend, SoundProfile soundProfile)
        {
            if (!IsReady) return;

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.45f) * velocity * (0.72f + soundProfile.body * 0.42f));

            _samplePlayer.PlayDrum(lane, velocity, pan, gainAmount * _masterVolume, soundProfile);
        }

        public void PlayMelodyNote(int midi, float velocity, float duration,
            float pan, float brightness, float depth, float fxSend,
            SoundProfile soundProfile, float glide = 0f)
        {
            if (!IsReady) return;

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.4f) * velocity * (0.72f + soundProfile.body * 0.32f));

            _samplePlayer.PlayNote(midi, velocity, duration, pan, gainAmount * _masterVolume, soundProfile);
        }

        public void PlayHarmonyChord(System.Collections.Generic.List<int> chord, float velocity,
            float duration, float pan, float brightness, float depth,
            float fxSend, SoundProfile soundProfile)
        {
            if (!IsReady) return;

            float gainAmount = Mathf.Clamp01(
                (1.02f - depth * 0.4f) * velocity * (0.72f + soundProfile.body * 0.32f));

            _samplePlayer.PlayChord(chord, velocity, duration, pan, gainAmount * _masterVolume, soundProfile);
        }
    }
}
