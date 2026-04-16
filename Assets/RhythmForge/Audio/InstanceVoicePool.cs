using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// A small per-instance pool of spatialized AudioSources intended to be parented to a
    /// pattern visualizer GameObject. Provides the same three play methods as SamplePlayer
    /// so it can be used as a drop-in replacement in spatial audio contexts.
    ///
    /// The pool is intentionally small (default 3, capped at 4) because one instance exists
    /// per pattern — keeping the total voice count bounded across the scene.
    ///
    /// Pan parameters are accepted for API compatibility but are ignored: spatial position on
    /// the parent GameObject drives stereo/3D placement instead.
    /// </summary>
    public class InstanceVoicePool : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────────────────

        private const int DefaultPoolSize = 3;
        private const int MaxPoolSize     = 4;

        // AudioSource 3-D settings applied to every pooled source.
        private const float SpatialBlend  = 1.0f;
        private const float MinDistance   = 0.4f;
        private const float MaxDistance   = 8.0f;

        // ── State ─────────────────────────────────────────────────────────────────────────

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private int _nextPoolIndex;

        // ── MonoBehaviour lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Build a default pool if Configure() was never called before first use.
            EnsurePool(DefaultPoolSize);
        }

        private void OnDestroy()
        {
            // AudioSource GameObjects are children and will be destroyed with us, but stop
            // any in-flight playback cleanly first so the audio engine is not surprised.
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null && _pool[i].isPlaying)
                    _pool[i].Stop();
            }
            _pool.Clear();
        }

        // ── Configuration ─────────────────────────────────────────────────────────────────

        /// <summary>Initialise (or re-initialise) the pool with the default size of 3.</summary>
        public void Configure()
        {
            Configure(DefaultPoolSize);
        }

        /// <summary>
        /// Initialise (or re-initialise) the pool with <paramref name="poolSize"/> voices,
        /// capped at <see cref="MaxPoolSize"/> to prevent voice explosion.
        /// </summary>
        /// <param name="poolSize">Desired pool size (clamped to 1–4).</param>
        public void Configure(int poolSize)
        {
            int clamped = Mathf.Clamp(poolSize, 1, MaxPoolSize);
            RebuildPool(clamped);
        }

        // ── Public play API (mirrors SamplePlayer) ────────────────────────────────────────

        /// <summary>
        /// Render and play a drum hit through a spatialized source.
        /// The <paramref name="pan"/> value is accepted for signature compatibility but
        /// is ignored — 3-D position on the parent GameObject handles placement.
        /// </summary>
        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan,
            float brightness, float gain, float fxSend, SoundProfile profile)
        {
            var spec = VoiceSpecResolver.ResolveDrum(lane, preset, profile, brightness, fxSend);
            PlayClip(spec, Mathf.Clamp01(velocity * gain), startDelay: 0f);
        }

        /// <summary>
        /// Render and play a melodic or harmony note through a spatialized source.
        /// The <paramref name="pan"/> value is accepted for signature compatibility but ignored.
        /// </summary>
        public void PlayNote(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float gain, float fxSend, SoundProfile profile,
            PatternType type, float glide = 0f, float startDelay = 0f)
        {
            ResolvedVoiceSpec spec = type == PatternType.HarmonyPad
                ? VoiceSpecResolver.ResolveHarmony(preset, profile, midi, duration, brightness, fxSend)
                : VoiceSpecResolver.ResolveMelody(preset, profile, midi, duration, brightness, fxSend, glide);

            float voiceGain = type == PatternType.HarmonyPad ? 0.66f : 0.72f;
            PlayClip(spec, Mathf.Clamp01(velocity * gain * voiceGain), startDelay);
        }

        /// <summary>
        /// Render and play a chord (multiple notes with small stagger) through spatialized sources.
        /// The <paramref name="pan"/> value is accepted for signature compatibility but ignored.
        /// </summary>
        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity, float duration,
            float pan, float brightness, float gain, float fxSend, SoundProfile profile)
        {
            if (chord == null) return;

            for (int i = 0; i < chord.Count; i++)
            {
                // Note: per-note pan offset from SamplePlayer is dropped here intentionally —
                // chord spread is handled by the 3-D scene layout, not stereo pan.
                float chordVelocity = velocity * (i == 0 ? 0.9f : 0.72f);
                float startDelay    = i * 0.01f;
                PlayNote(preset, chord[i], chordVelocity, duration, 0f, brightness, gain,
                    fxSend, profile, PatternType.HarmonyPad, 0f, startDelay);
            }
        }

        // ── Internal helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve, render, and play a clip.  No local clip cache is maintained here;
        /// VoiceRendererRegistry.Render is called directly (a shared cache will be wired
        /// in a later Phase when SamplePlayer exposes its cache for external consumers).
        /// </summary>
        private void PlayClip(ResolvedVoiceSpec spec, float volume, float startDelay)
        {
            AudioClip clip = VoiceRendererRegistry.Render(spec);
            if (clip == null) return;

            var source = GetNextSource();
            source.clip   = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch  = 1f;
            source.time   = 0f;

            if (startDelay > 0f)
                source.PlayDelayed(startDelay);
            else
                source.Play();
        }

        /// <summary>Round-robin through the pool, stopping any still-playing source.</summary>
        private AudioSource GetNextSource()
        {
            EnsurePool(DefaultPoolSize);

            var source = _pool[_nextPoolIndex];
            _nextPoolIndex = (_nextPoolIndex + 1) % _pool.Count;

            if (source.isPlaying)
                source.Stop();

            return source;
        }

        /// <summary>
        /// Create the pool at the requested size if it has not been built yet.
        /// Called defensively from <see cref="Awake"/> and <see cref="GetNextSource"/>.
        /// </summary>
        private void EnsurePool(int size)
        {
            if (_pool.Count > 0) return;
            BuildPool(size);
        }

        /// <summary>
        /// Tear down any existing pool children and create a fresh pool at the given size.
        /// </summary>
        private void RebuildPool(int size)
        {
            // Stop and remove old sources.
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] == null) continue;
                if (_pool[i].isPlaying) _pool[i].Stop();
                var go = _pool[i].gameObject;
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
            _pool.Clear();
            _nextPoolIndex = 0;

            BuildPool(size);
        }

        /// <summary>Create <paramref name="size"/> spatialized AudioSource children.</summary>
        private void BuildPool(int size)
        {
            for (int i = 0; i < size; i++)
            {
                var go     = new GameObject($"SpatialVoice_{i}");
                go.transform.SetParent(transform, worldPositionStays: false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake   = false;
                source.loop          = false;
                source.spatialize    = true;
                source.spatialBlend  = SpatialBlend;
                source.rolloffMode   = AudioRolloffMode.Logarithmic;
                source.minDistance   = MinDistance;
                source.maxDistance   = MaxDistance;
                // Stereo pan is intentionally left at 0 — 3-D position drives placement.
                source.panStereo     = 0f;

                _pool.Add(source);
            }
        }
    }
}
