using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Runtime procedural clip player with pooled AudioSources and bounded clip caching.
    /// Synthesis runs on background threads; the main thread only calls AudioClip.SetData
    /// after the render task completes — eliminating the per-cycle main-thread stall.
    /// </summary>
    public class SamplePlayer : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private int _poolSize = 24;

        [Header("Cache Settings")]
        [SerializeField] private int _maxCachedClips = 96;

        // ── Voice cache (main thread only) ──────────────────────────────────────
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<string, CachedClip> _voiceCache = new Dictionary<string, CachedClip>();
        private int _nextPoolIndex;
        private long _cacheAge;

        // ── Pending-play queue: specs that arrived while their clip was not yet cached ──
        private readonly List<PendingPlay> _pendingPlays = new List<PendingPlay>();

        // ── Background render pipeline ───────────────────────────────────────────
        // Background tasks write raw float arrays; main thread finalises AudioClips.
        private readonly ConcurrentQueue<CompletedRender> _completedRenders =
            new ConcurrentQueue<CompletedRender>();
        // Keys currently being rendered in background — checked before starting a duplicate task.
        private readonly HashSet<string> _inFlightKeys = new HashSet<string>();

        // ── LRU eviction scratch (reused) ────────────────────────────────────────
        private readonly HashSet<AudioClip> _inUseScratch = new HashSet<AudioClip>();
        private readonly List<KeyValuePair<string, CachedClip>> _evictCandidates =
            new List<KeyValuePair<string, CachedClip>>();

        private sealed class CachedClip
        {
            public AudioClip clip;
            public long lastUsed;
        }

        private struct PendingPlay
        {
            public string cacheKey;
            public float volume;
            public float pan;
            public float startDelay;
        }

        private sealed class CompletedRender
        {
            public string cacheKey;
            public string clipName;
            public float[] left;
            public float[] right;
            // If non-null, this was a WarmClip request (no playback on completion).
            public bool warmOnly;
            // Pending-play context (volume/pan/delay) — filled if warmOnly==false.
            public float volume;
            public float pan;
            public float startDelay;
        }

        public void Configure()
        {
            EnsurePool();
        }

        public void Configure(AudioClip kick, AudioClip snare, AudioClip hat, AudioClip perc, AudioClip tone)
        {
            Configure();
        }

        private void Awake()
        {
            EnsurePool();
        }

        private void Update()
        {
            // Promote completed background renders into the cache and play any pending audio.
            while (_completedRenders.TryDequeue(out var completed))
            {
                _inFlightKeys.Remove(completed.cacheKey);

                if (!_voiceCache.ContainsKey(completed.cacheKey))
                {
                    var clip = SynthUtilities.BuildClip(completed.clipName, completed.left, completed.right);
                    _voiceCache[completed.cacheKey] = new CachedClip { clip = clip, lastUsed = ++_cacheAge };
                    TrimCacheIfNeeded();
                }

                if (!completed.warmOnly && _voiceCache.TryGetValue(completed.cacheKey, out var c))
                    IssuePlay(c.clip, completed.volume, completed.pan, completed.startDelay);
            }

            // Retry pending plays whose clip has since been promoted.
            for (int i = _pendingPlays.Count - 1; i >= 0; i--)
            {
                var p = _pendingPlays[i];
                if (_voiceCache.TryGetValue(p.cacheKey, out var cached))
                {
                    cached.lastUsed = ++_cacheAge;
                    IssuePlay(cached.clip, p.volume, p.pan, p.startDelay);
                    _pendingPlays.RemoveAt(i);
                }
            }
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

        // ── Public play API ──────────────────────────────────────────────────────

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
                float delay = i * 0.01f;
                PlayNote(preset, chord[i], chordVelocity, duration, chordPan, brightness, gain,
                    fxSend, profile, PatternType.HarmonyPad, 0f, delay);
            }
        }

        /// <summary>
        /// Pre-render clips for the given specs on background threads so they are cache-warm
        /// before the sequencer needs them. Safe to call from main thread at bar boundaries.
        /// </summary>
        public void WarmClips(IEnumerable<ResolvedVoiceSpec> specs)
        {
            foreach (var spec in specs)
            {
                string key = spec.GetCacheKey();
                if (_voiceCache.ContainsKey(key) || _inFlightKeys.Contains(key))
                    continue;
                _inFlightKeys.Add(key);
                DispatchBackgroundRender(spec, 0f, 0f, 0f, warmOnly: true);
            }
        }

        // ── Internal play path ────────────────────────────────────────────────────

        private void PlayClip(ResolvedVoiceSpec spec, float volume, float pan, float startDelay)
        {
            string key = spec.GetCacheKey();

            if (_voiceCache.TryGetValue(key, out var cached))
            {
                cached.lastUsed = ++_cacheAge;
                IssuePlay(cached.clip, volume, pan, startDelay);
                return;
            }

            // Clip not ready — enqueue a pending play and start a background render if not already in flight.
            _pendingPlays.Add(new PendingPlay { cacheKey = key, volume = volume, pan = pan, startDelay = startDelay });

            if (!_inFlightKeys.Contains(key))
            {
                _inFlightKeys.Add(key);
                DispatchBackgroundRender(spec, volume, pan, startDelay, warmOnly: false);
            }
        }

        private void DispatchBackgroundRender(ResolvedVoiceSpec spec, float volume, float pan, float startDelay, bool warmOnly)
        {
            string key = spec.GetCacheKey();
            var queue = _completedRenders;
            Task.Run(() =>
            {
                try
                {
                    // Render raw samples on background thread (no Unity API calls allowed here).
                    RawSamples raw = VoiceRendererRegistry.RenderRaw(spec);
                    queue.Enqueue(new CompletedRender
                    {
                        cacheKey  = key,
                        clipName  = raw.name,
                        left      = raw.left,
                        right     = raw.right,
                        warmOnly  = warmOnly,
                        volume    = volume,
                        pan       = pan,
                        startDelay = startDelay
                    });
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[SamplePlayer] Background render failed for {key}: {ex.Message}");
                }
            });
        }

        private void IssuePlay(AudioClip clip, float volume, float pan, float startDelay)
        {
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

        // ── Pool ─────────────────────────────────────────────────────────────────

        private AudioSource GetNextSource()
        {
            EnsurePool();
            var source = _pool[_nextPoolIndex];
            _nextPoolIndex = (_nextPoolIndex + 1) % _pool.Count;
            if (source.isPlaying)
                source.Stop();
            return source;
        }

        private void EnsurePool()
        {
            if (_pool.Count > 0) return;

            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"VoicePool_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialize  = false;
                src.loop        = false;
                _pool.Add(src);
            }
        }

        // ── LRU eviction — O(N) with HashSet for in-use detection ───────────────

        private void TrimCacheIfNeeded()
        {
            if (_voiceCache.Count <= _maxCachedClips)
                return;

            // Build in-use set once — O(pool)
            _inUseScratch.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null && _pool[i].isPlaying && _pool[i].clip != null)
                    _inUseScratch.Add(_pool[i].clip);
            }

            // Collect eviction candidates — O(cache)
            _evictCandidates.Clear();
            foreach (var pair in _voiceCache)
            {
                if (!_inUseScratch.Contains(pair.Value.clip))
                    _evictCandidates.Add(pair);
            }

            // Sort ascending by lastUsed so we evict oldest first
            _evictCandidates.Sort((a, b) => a.Value.lastUsed.CompareTo(b.Value.lastUsed));

            int toRemove = _voiceCache.Count - _maxCachedClips;
            for (int i = 0; i < _evictCandidates.Count && toRemove > 0; i++, toRemove--)
            {
                _voiceCache.Remove(_evictCandidates[i].Key);
                ReleaseClip(_evictCandidates[i].Value.clip);
            }
        }

        private static void ReleaseClip(AudioClip clip)
        {
            if (clip == null) return;
            if (Application.isPlaying)
                Destroy(clip);
            else
                DestroyImmediate(clip);
        }
    }
}
