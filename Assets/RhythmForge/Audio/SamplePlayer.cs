using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Runtime procedural clip player with pooled AudioSources and bounded clip caching.
    /// </summary>
    public class SamplePlayer : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private int _poolSize = 24;

        [Header("Cache Settings")]
        [SerializeField] private int _maxCachedClips = 96;

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<string, CachedClip> _voiceCache = new Dictionary<string, CachedClip>();
        private int _nextPoolIndex;
        private long _cacheAge;

        private sealed class CachedClip
        {
            public AudioClip clip;
            public long lastUsed;
        }

        public void Configure()
        {
            // Intentionally empty. The richer procedural path renders clips on demand.
            EnsurePool();
        }

        public void Configure(AudioClip kick, AudioClip snare, AudioClip hat, AudioClip perc, AudioClip tone)
        {
            // Legacy compatibility: bootstrap code used to inject pre-baked clips.
            Configure();
        }

        private void Awake()
        {
            EnsurePool();
        }

        private void OnDestroy()
        {
            foreach (var entry in _voiceCache.Values)
            {
                if (entry?.clip != null)
                    ReleaseClip(entry.clip);
            }
            _voiceCache.Clear();
        }

        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan,
            float brightness, float gain, float fxSend, SoundProfile profile)
        {
            var spec = VoiceSpecResolver.ResolveDrum(lane, preset, profile, brightness, fxSend);
            PlayClip(spec, Mathf.Clamp01(velocity * gain), pan, 0f);
        }

        public void PlayNote(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float gain, float fxSend, SoundProfile profile,
            PatternType type, float glide = 0f, float startDelay = 0f)
        {
            ResolvedVoiceSpec spec = type == PatternType.HarmonyPad
                ? VoiceSpecResolver.ResolveHarmony(preset, profile, midi, duration, brightness, fxSend)
                : VoiceSpecResolver.ResolveMelody(preset, profile, midi, duration, brightness, fxSend, glide);

            float voiceGain = type == PatternType.HarmonyPad ? 0.66f : 0.72f;
            PlayClip(spec, Mathf.Clamp01(velocity * gain * voiceGain), pan, startDelay);
        }

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity, float duration,
            float pan, float brightness, float gain, float fxSend, SoundProfile profile)
        {
            if (chord == null) return;

            for (int i = 0; i < chord.Count; i++)
            {
                float chordPan = Mathf.Clamp(pan + (i - 1.5f) * 0.12f, -1f, 1f);
                float chordVelocity = velocity * (i == 0 ? 0.9f : 0.72f);
                float startDelay = i * 0.01f;
                PlayNote(preset, chord[i], chordVelocity, duration, chordPan, brightness, gain,
                    fxSend, profile, PatternType.HarmonyPad, 0f, startDelay);
            }
        }

        private void PlayClip(ResolvedVoiceSpec spec, float volume, float pan, float startDelay)
        {
            AudioClip clip = GetOrCreateClip(spec);
            if (clip == null) return;

            var source = GetNextSource();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.pitch = 1f;
            source.time = 0f;

            if (startDelay > 0f)
                source.PlayDelayed(startDelay);
            else
                source.Play();
        }

        private AudioSource GetNextSource()
        {
            EnsurePool();

            var source = _pool[_nextPoolIndex];
            _nextPoolIndex = (_nextPoolIndex + 1) % _pool.Count;

            if (source.isPlaying)
                source.Stop();

            return source;
        }

        private AudioClip GetOrCreateClip(ResolvedVoiceSpec spec)
        {
            string cacheKey = spec.GetCacheKey();
            if (_voiceCache.TryGetValue(cacheKey, out var cached))
            {
                cached.lastUsed = ++_cacheAge;
                return cached.clip;
            }

            AudioClip clip = VoiceRendererRegistry.Render(spec);

            _voiceCache[cacheKey] = new CachedClip
            {
                clip = clip,
                lastUsed = ++_cacheAge
            };

            TrimCacheIfNeeded();
            return clip;
        }

        private void TrimCacheIfNeeded()
        {
            while (_voiceCache.Count > _maxCachedClips)
            {
                string oldestKey = null;
                long oldestAge = long.MaxValue;

                foreach (var pair in _voiceCache)
                {
                    if (pair.Value.lastUsed < oldestAge && !IsClipInUse(pair.Value.clip))
                    {
                        oldestAge = pair.Value.lastUsed;
                        oldestKey = pair.Key;
                    }
                }

                if (oldestKey == null) return;

                var clip = _voiceCache[oldestKey].clip;
                if (IsClipInUse(clip))
                    return;

                _voiceCache.Remove(oldestKey);
                if (clip != null)
                    ReleaseClip(clip);
            }
        }

        private void EnsurePool()
        {
            if (_pool.Count > 0) return;

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

        private bool IsClipInUse(AudioClip clip)
        {
            if (clip == null)
                return false;

            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null && _pool[i].isPlaying && _pool[i].clip == clip)
                    return true;
            }

            return false;
        }

        private static void ReleaseClip(AudioClip clip)
        {
            if (clip == null)
                return;

            if (Application.isPlaying)
                Destroy(clip);
            else
                DestroyImmediate(clip);
        }
    }
}
