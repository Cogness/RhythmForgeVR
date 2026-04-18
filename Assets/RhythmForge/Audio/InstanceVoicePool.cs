using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RhythmForge.Audio
{
    /// <summary>
    /// Small per-instance pool of spatialized AudioSources parented to a visualizer.
    /// </summary>
    public sealed class InstanceVoicePool
    {
        private readonly Transform _parent;
        private readonly string _label;
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private Transform _root;
        private AudioMixerGroup _mixerGroup;
        private int _nextSourceIndex;

        public int VoiceCount => _sources.Count;

        public InstanceVoicePool(Transform parent, string label)
        {
            _parent = parent;
            _label = string.IsNullOrEmpty(label) ? "InstanceVoicePool" : label;
            EnsureRoot();
        }

        public void SetVoiceCount(int voiceCount)
        {
            voiceCount = Mathf.Max(1, voiceCount);
            EnsureRoot();

            while (_sources.Count < voiceCount)
                _sources.Add(CreateSource(_sources.Count));

            while (_sources.Count > voiceCount)
            {
                int lastIndex = _sources.Count - 1;
                var source = _sources[lastIndex];
                _sources.RemoveAt(lastIndex);
                if (source != null)
                {
                    if (Application.isPlaying)
                        Object.Destroy(source.gameObject);
                    else
                        Object.DestroyImmediate(source.gameObject);
                }
            }

            if (_nextSourceIndex >= _sources.Count)
                _nextSourceIndex = 0;
        }

        public void SetMixerGroup(AudioMixerGroup mixerGroup)
        {
            _mixerGroup = mixerGroup;
            for (int i = 0; i < _sources.Count; i++)
            {
                if (_sources[i] != null)
                    _sources[i].outputAudioMixerGroup = _mixerGroup;
            }
        }

        public void Play(AudioClip clip, float volume, float startDelay)
        {
            if (clip == null)
                return;

            if (_sources.Count == 0)
                SetVoiceCount(1);

            var source = _sources[_nextSourceIndex];
            _nextSourceIndex = (_nextSourceIndex + 1) % _sources.Count;
            if (source == null)
                return;

            if (source.isPlaying)
                source.Stop();

            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = 1f;
            source.time = 0f;

            if (startDelay > 0f)
                source.PlayDelayed(startDelay);
            else
                source.Play();
        }

        public void CollectActiveClips(HashSet<AudioClip> results)
        {
            if (results == null)
                return;

            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source != null && source.isPlaying && source.clip != null)
                    results.Add(source.clip);
            }
        }

        public void Release()
        {
            _sources.Clear();
            _nextSourceIndex = 0;

            if (_root == null)
                return;

            var rootObject = _root.gameObject;
            _root = null;

            if (Application.isPlaying)
                Object.Destroy(rootObject);
            else
                Object.DestroyImmediate(rootObject);
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;

            var rootObject = new GameObject("InstanceVoicePool");
            _root = rootObject.transform;
            _root.SetParent(_parent, false);
            _root.localPosition = Vector3.zero;
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one;
        }

        private AudioSource CreateSource(int index)
        {
            var sourceObject = new GameObject($"{_label}_{index}");
            sourceObject.transform.SetParent(_root, false);

            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialize = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.4f;
            source.maxDistance = 8f;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.outputAudioMixerGroup = _mixerGroup;
            return source;
        }
    }
}
