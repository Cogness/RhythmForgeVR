using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
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

        [Header("Mixer Routing")]
        [SerializeField] private AudioMixer _mixer;
        private AudioMixerGroup _activeGroup;

        // ── Voice cache (main thread only) ──────────────────────────────────────
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<string, CachedClip> _voiceCache = new Dictionary<string, CachedClip>();
        private int _nextPoolIndex;
        private long _cacheAge;

        // ── Pending-play queue: specs that arrived while their clip was not yet cached ──
        private readonly List<PendingPlay> _pendingPlays = new List<PendingPlay>();
        private readonly List<PendingClipRequest> _pendingClipRequests = new List<PendingClipRequest>();

        // ── Background render pipeline ───────────────────────────────────────────
        // Background tasks write raw float arrays; main thread finalises AudioClips.
        private readonly ConcurrentQueue<CompletedRender> _completedRenders =
            new ConcurrentQueue<CompletedRender>();
        // Keys currently being rendered in background for the current generation.
        private readonly Dictionary<string, int> _inFlightKeys = new Dictionary<string, int>();

        // ── Background render concurrency cap ─────────────────────────────────────
        private const int MaxConcurrentRenders = 4;
        private int _activeRenderCount;
        private int _renderGeneration;
        private readonly Queue<QueuedRender> _renderQueue = new Queue<QueuedRender>();

        // ── LRU eviction scratch (reused) ────────────────────────────────────────
        private readonly HashSet<AudioClip> _inUseScratch = new HashSet<AudioClip>();
        private readonly List<KeyValuePair<string, CachedClip>> _evictCandidates =
            new List<KeyValuePair<string, CachedClip>>();
        private readonly List<string> _invalidateScratch = new List<string>();

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

        private struct PendingClipRequest
        {
            public string cacheKey;
            public System.Action<AudioClip> onReady;
        }

        private struct QueuedRender
        {
            public ResolvedVoiceSpec spec;
            public float volume;
            public float pan;
            public float startDelay;
            public bool warmOnly;
            public int generation;
        }

        private sealed class CompletedRender
        {
            public string cacheKey;
            public string clipName;
            public float[] left;
            public float[] right;
            public bool warmOnly;
            public float volume;
            public float pan;
            public float startDelay;
            public int generation;
            public bool failed;
        }

        public void Configure()
        {
            EnsurePool();
        }

        /// <summary>Inject the AudioMixer asset and immediately route all pool sources to the genre group.</summary>
        public void Configure(AudioMixer mixer)
        {
            _mixer = mixer;
            EnsurePool();
        }

        public AudioMixerGroup ActiveMixerGroup => _activeGroup;

        public void Configure(AudioClip kick, AudioClip snare, AudioClip hat, AudioClip perc, AudioClip tone)
        {
            Configure();
        }

        /// <summary>
        /// Re-routes every pooled AudioSource to the AudioMixerGroup whose name matches <paramref name="genreId"/>.
        /// Call this whenever the active genre changes (e.g. "newage", "electronic", "jazz").
        /// Group name matching is case-insensitive and falls back to Master if not found.
        /// </summary>
        public void SetMixerGroup(string genreId)
        {
            if (_mixer == null) return;

            // Unity mixer group names set in Step 2: "NewAge", "Electronic", "Jazz".
            // Map genre IDs (lower-case) to the Pascal-case group names used in the mixer asset.
            string groupName = genreId switch
            {
                "newage"     => "NewAge",
                "electronic" => "Electronic",
                "jazz"       => "Jazz",
                _            => "Master"
            };

            var groups = _mixer.FindMatchingGroups(groupName);
            _activeGroup = groups.Length > 0 ? groups[0] : null;

            EnsurePool();
            foreach (var src in _pool)
            {
                if (src != null)
                    src.outputAudioMixerGroup = _activeGroup;
            }
        }

        private void Awake()
        {
            EnsurePool();
        }

        private void Update()
        {
            // Drain completed renders — promote into cache, play pending audio.
            while (_completedRenders.TryDequeue(out var completed))
            {
                if (_activeRenderCount > 0) _activeRenderCount--;
                if (_inFlightKeys.TryGetValue(completed.cacheKey, out var inFlightGeneration)
                    && inFlightGeneration == completed.generation)
                {
                    _inFlightKeys.Remove(completed.cacheKey);
                }

                if (completed.failed || completed.generation != _renderGeneration)
                    continue;

                if (!_voiceCache.ContainsKey(completed.cacheKey))
                {
                    var clip = SynthUtilities.BuildClip(completed.clipName, completed.left, completed.right);
                    _voiceCache[completed.cacheKey] = new CachedClip { clip = clip, lastUsed = ++_cacheAge };
                    TrimCacheIfNeeded();
                }

                if (!completed.warmOnly && _voiceCache.TryGetValue(completed.cacheKey, out var c))
                    IssuePlay(c.clip, completed.volume, completed.pan, completed.startDelay);
            }

            // Drain the queued renders up to the concurrency cap.
            while (_renderQueue.Count > 0 && _activeRenderCount < MaxConcurrentRenders)
            {
                var entry = _renderQueue.Dequeue();
                if (entry.generation != _renderGeneration)
                    continue;

                string key = entry.spec.GetCacheKey();
                if (!_voiceCache.ContainsKey(key) && !IsInFlight(key, entry.generation))
                {
                    _inFlightKeys[key] = entry.generation;
                    LaunchRenderTask(entry.spec, entry.volume, entry.pan, entry.startDelay, entry.warmOnly, entry.generation);
                }
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

            for (int i = _pendingClipRequests.Count - 1; i >= 0; i--)
            {
                var request = _pendingClipRequests[i];
                if (_voiceCache.TryGetValue(request.cacheKey, out var cached))
                {
                    cached.lastUsed = ++_cacheAge;
                    request.onReady?.Invoke(cached.clip);
                    _pendingClipRequests.RemoveAt(i);
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
            PlayDrum(preset, lane, velocity, pan, brightness, gain, fxSend, fxSend, profile);
        }

        public void PlayDrum(InstrumentPreset preset, string lane, float velocity, float pan,
            float brightness, float gain, float reverbSend, float delaySend, SoundProfile profile)
        {
            var spec = VoiceSpecResolver.ResolveDrum(lane, preset, profile, brightness, reverbSend, delaySend);
            PlayClip(spec, Mathf.Clamp01(velocity * gain), pan, 0f);
        }

        public void PlayNote(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float gain, float fxSend, SoundProfile profile,
            PatternType type, float glide = 0f, float startDelay = 0f)
        {
            PlayNote(preset, midi, velocity, duration, pan, brightness, gain, fxSend, fxSend, profile, type, glide, startDelay);
        }

        public void PlayNote(InstrumentPreset preset, int midi, float velocity, float duration,
            float pan, float brightness, float gain, float reverbSend, float delaySend, SoundProfile profile,
            PatternType type, float glide = 0f, float startDelay = 0f)
        {
            ResolvedVoiceSpec spec = type == PatternType.HarmonyPad
                ? VoiceSpecResolver.ResolveHarmony(preset, profile, midi, duration, brightness, reverbSend, delaySend)
                : VoiceSpecResolver.ResolveMelody(preset, profile, midi, duration, brightness, reverbSend, delaySend, glide);

            float voiceGain = type == PatternType.HarmonyPad ? 0.66f : 0.72f;
            PlayClip(spec, Mathf.Clamp01(velocity * gain * voiceGain), pan, startDelay);
        }

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity, float duration,
            float pan, float brightness, float gain, float fxSend, SoundProfile profile)
        {
            PlayChord(preset, chord, velocity, duration, pan, brightness, gain, fxSend, fxSend, profile);
        }

        public void PlayChord(InstrumentPreset preset, List<int> chord, float velocity, float duration,
            float pan, float brightness, float gain, float reverbSend, float delaySend, SoundProfile profile)
        {
            if (chord == null) return;

            for (int i = 0; i < chord.Count; i++)
            {
                float chordPan = Mathf.Clamp(pan + (i - 1.5f) * 0.12f, -1f, 1f);
                float chordVelocity = velocity * (i == 0 ? 0.9f : 0.72f);
                float delay = i * 0.01f;
                PlayNote(preset, chord[i], chordVelocity, duration, chordPan, brightness, gain,
                    reverbSend, delaySend, profile, PatternType.HarmonyPad, 0f, delay);
            }
        }

        public void PlayResolved(ResolvedVoiceSpec spec, float volume, float pan = 0f, float startDelay = 0f)
        {
            PlayClip(spec, volume, pan, startDelay);
        }

        public void RequestClip(ResolvedVoiceSpec spec, System.Action<AudioClip> onReady)
        {
            if (onReady == null)
                return;

            string key = spec.GetCacheKey();
            if (_voiceCache.TryGetValue(key, out var cached))
            {
                cached.lastUsed = ++_cacheAge;
                onReady(cached.clip);
                return;
            }

            _pendingClipRequests.Add(new PendingClipRequest
            {
                cacheKey = key,
                onReady = onReady
            });

            if (!IsInFlight(key, _renderGeneration))
                DispatchBackgroundRender(spec, 0f, 0f, 0f, warmOnly: true);
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
                if (_voiceCache.ContainsKey(key) || IsInFlight(key, _renderGeneration))
                    continue;
                DispatchBackgroundRender(spec, 0f, 0f, 0f, warmOnly: true);
            }
        }

        public void InvalidateAll()
        {
            _renderGeneration++;

            foreach (var entry in _voiceCache.Values)
            {
                if (entry?.clip != null)
                    ReleaseClip(entry.clip);
            }

            _voiceCache.Clear();
            _pendingPlays.Clear();
            _pendingClipRequests.Clear();
            _inFlightKeys.Clear();
            _renderQueue.Clear();
            _cacheAge = 0;
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

            if (!IsInFlight(key, _renderGeneration))
                DispatchBackgroundRender(spec, volume, pan, startDelay, warmOnly: false);
        }

        private void DispatchBackgroundRender(ResolvedVoiceSpec spec, float volume, float pan, float startDelay, bool warmOnly)
        {
            int generation = _renderGeneration;

            if (_activeRenderCount >= MaxConcurrentRenders)
            {
                _renderQueue.Enqueue(new QueuedRender
                {
                    spec = spec,
                    volume = volume,
                    pan = pan,
                    startDelay = startDelay,
                    warmOnly = warmOnly,
                    generation = generation
                });
                return;
            }

            string key = spec.GetCacheKey();
            if (IsInFlight(key, generation)) return;
            _inFlightKeys[key] = generation;
            LaunchRenderTask(spec, volume, pan, startDelay, warmOnly, generation);
        }

        private void LaunchRenderTask(ResolvedVoiceSpec spec, float volume, float pan, float startDelay, bool warmOnly, int generation)
        {
            _activeRenderCount++;
            string key = spec.GetCacheKey();
            var queue = _completedRenders;
            Task.Run(() =>
            {
                try
                {
                    RawSamples raw = VoiceRendererRegistry.RenderRaw(spec);
                    queue.Enqueue(new CompletedRender
                    {
                        cacheKey   = key,
                        clipName   = raw.name,
                        left       = raw.left,
                        right      = raw.right,
                        warmOnly   = warmOnly,
                        volume     = volume,
                        pan        = pan,
                        startDelay = startDelay,
                        generation = generation
                    });
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[SamplePlayer] Background render failed for {key}: {ex.Message}");
                    queue.Enqueue(new CompletedRender
                    {
                        cacheKey = key,
                        generation = generation,
                        failed = true
                    });
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

        private bool IsInFlight(string key, int generation)
        {
            return _inFlightKeys.TryGetValue(key, out var activeGeneration) && activeGeneration == generation;
        }

        private AudioClip GetOrCreateClip(ResolvedVoiceSpec spec)
        {
            string key = spec.GetCacheKey();
            if (_voiceCache.TryGetValue(key, out var cached))
            {
                cached.lastUsed = ++_cacheAge;
                return cached.clip;
            }

            var raw = VoiceRendererRegistry.RenderRaw(spec);
            var clip = SynthUtilities.BuildClip(raw.name, raw.left, raw.right);
            _voiceCache[key] = new CachedClip { clip = clip, lastUsed = ++_cacheAge };
            TrimCacheIfNeeded();
            return clip;
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
                if (_activeGroup != null)
                    src.outputAudioMixerGroup = _activeGroup;
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
            InstanceVoiceRegistry.Shared?.CollectActiveClips(_inUseScratch);

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
