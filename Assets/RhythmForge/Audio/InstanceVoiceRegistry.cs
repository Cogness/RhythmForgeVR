using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Global registry for per-instance spatial voice pools.
    /// </summary>
    public sealed class InstanceVoiceRegistry
    {
        public const int MaxSpatialVoices = 32;

        private readonly Dictionary<string, InstanceVoicePool> _pools = new Dictionary<string, InstanceVoicePool>();
        private AudioMixerGroup _mixerGroup;
        private int _activeVoiceCount;

        public static InstanceVoiceRegistry Shared { get; private set; }

        public int ActiveVoiceCount => _activeVoiceCount;

        public static InstanceVoiceRegistry GetShared()
        {
            if (Shared == null)
                Shared = new InstanceVoiceRegistry();

            return Shared;
        }

        public static void SetShared(InstanceVoiceRegistry registry)
        {
            Shared = registry;
        }

        public void SetMixerGroup(AudioMixerGroup mixerGroup)
        {
            _mixerGroup = mixerGroup;
            foreach (var pool in _pools.Values)
                pool?.SetMixerGroup(_mixerGroup);
        }

        public void Register(string instanceId, InstanceVoicePool pool, int requestedVoiceCount)
        {
            if (string.IsNullOrEmpty(instanceId) || pool == null)
                return;

            Debug.Assert(!_pools.ContainsKey(instanceId), $"[SpatialAudio] Duplicate InstanceVoicePool registration for {instanceId}.");
            if (_pools.ContainsKey(instanceId))
                return;

            int voiceCount = Mathf.Max(1, requestedVoiceCount);
            if (_activeVoiceCount + voiceCount > MaxSpatialVoices)
            {
                voiceCount = 1;
                Debug.LogWarning($"[SpatialAudio] Spatial voice cap reached; routing {instanceId} with a single voice.");
            }

            pool.SetVoiceCount(voiceCount);
            pool.SetMixerGroup(_mixerGroup);
            _pools.Add(instanceId, pool);
            _activeVoiceCount += voiceCount;
        }

        public bool TryGetPool(string instanceId, out InstanceVoicePool pool)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                pool = null;
                return false;
            }

            return _pools.TryGetValue(instanceId, out pool) && pool != null;
        }

        public void Unregister(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return;

            if (!_pools.TryGetValue(instanceId, out var pool))
                return;

            _pools.Remove(instanceId);
            _activeVoiceCount = Mathf.Max(0, _activeVoiceCount - pool.VoiceCount);
            pool.Release();
        }

        public void CollectActiveClips(HashSet<AudioClip> results)
        {
            if (results == null)
                return;

            foreach (var pool in _pools.Values)
                pool?.CollectActiveClips(results);
        }
    }
}
