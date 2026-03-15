using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class RhythmSoundEngine : MonoBehaviour
{
    private const int VoiceCount = 9;
    private const int StepsPerBar = 16;
    private const int MinimumPoolSize = 12;
    private const float MinimumBpm = 40f;
    private const float MaximumBpm = 220f;
    private const float MinimumPitchSemitones = -24f;
    private const float MaximumPitchSemitones = 24f;

    [Header("Transport")]
    [SerializeField] private float _defaultBpm = 120f;
    [SerializeField] private float _bpmStep = 5f;

    [Header("Global Sound")]
    [SerializeField] private float _masterVolume = 0.9f;
    [SerializeField] private float _defaultPitchSemitones = 0f;
    [SerializeField] private float _pitchStep = 1f;
    [SerializeField] private float _defaultReverbAmount = 0.18f;
    [SerializeField] private float _reverbStep = 0.05f;

    [Header("Voices")]
    [SerializeField] private bool _autoCreateDefaults = true;
    [SerializeField] private RhythmVoiceDefinition[] _voices = Array.Empty<RhythmVoiceDefinition>();
    [SerializeField] private int _sourcePoolSize = 18;

    private AudioSource[] _voiceSources = Array.Empty<AudioSource>();
    private AudioReverbFilter[] _reverbFilters = Array.Empty<AudioReverbFilter>();
    private AudioClip[] _voiceClips = Array.Empty<AudioClip>();
    private bool[] _activeLoops = new bool[VoiceCount];
    private int _sourceCursor;
    private int _transportStep = -1;
    private float _stepTimer;
    private float _currentBpm;
    private float _currentPitchSemitones;
    private float _currentReverbAmount;
    private bool _readyLogged;

    public float Bpm => _currentBpm;
    public float PitchSemitones => _currentPitchSemitones;
    public float ReverbAmount => _currentReverbAmount;
    public int RegisteredSoundCount => _voices != null ? _voices.Length : 0;

    private void Awake()
    {
        EnsureVoiceDefinitions();
        _currentBpm = Mathf.Clamp(_defaultBpm, MinimumBpm, MaximumBpm);
        _currentPitchSemitones = Mathf.Clamp(_defaultPitchSemitones, MinimumPitchSemitones, MaximumPitchSemitones);
        _currentReverbAmount = Mathf.Clamp01(_defaultReverbAmount);

        InitialiseClipCache();
        InitialiseSourcePool();
        ApplyReverbToPool();

        if (!_readyLogged)
        {
            Debug.Log($"RhythmSoundEngine: ready with {RegisteredSoundCount} sounds on '{gameObject.name}'.");
            _readyLogged = true;
        }
    }

    private void Update()
    {
        if (_voices == null || _voices.Length == 0)
        {
            return;
        }

        _stepTimer += Time.unscaledDeltaTime;
        float secondsPerStep = 60f / Mathf.Max(MinimumBpm, _currentBpm) / 4f;
        while (_stepTimer >= secondsPerStep)
        {
            _stepTimer -= secondsPerStep;
            AdvanceSequencer();
        }
    }

    public void TriggerSound(int soundIndex)
    {
        if (!IsValidSoundIndex(soundIndex))
        {
            Debug.LogWarning($"RhythmSoundEngine: invalid sound index {soundIndex}.");
            return;
        }

        PlayVoice(soundIndex);
    }

    public void ToggleLoop(int soundIndex)
    {
        if (!IsValidSoundIndex(soundIndex))
        {
            Debug.LogWarning($"RhythmSoundEngine: invalid loop index {soundIndex}.");
            return;
        }

        _activeLoops[soundIndex] = !_activeLoops[soundIndex];
        Debug.Log($"RhythmSoundEngine: loop {( _activeLoops[soundIndex] ? "enabled" : "disabled" )} for {_voices[soundIndex].name}.");
    }

    public void StopAllLoops()
    {
        for (int i = 0; i < _activeLoops.Length; i++)
        {
            _activeLoops[i] = false;
        }
    }

    public void SetBpm(float bpm)
    {
        _currentBpm = Mathf.Clamp(bpm, MinimumBpm, MaximumBpm);
    }

    public void AdjustBpm(float delta)
    {
        SetBpm(_currentBpm + delta);
    }

    public void SetPitchSemitones(float semitones)
    {
        _currentPitchSemitones = Mathf.Clamp(semitones, MinimumPitchSemitones, MaximumPitchSemitones);
    }

    public void AdjustPitchSemitones(float delta)
    {
        SetPitchSemitones(_currentPitchSemitones + delta);
    }

    public void SetReverb(float normalizedAmount)
    {
        _currentReverbAmount = Mathf.Clamp01(normalizedAmount);
        ApplyReverbToPool();
    }

    public void AdjustReverb(float delta)
    {
        SetReverb(_currentReverbAmount + delta);
    }

    public void ResetEngineSettings()
    {
        SetBpm(_defaultBpm);
        SetPitchSemitones(_defaultPitchSemitones);
        SetReverb(_defaultReverbAmount);
        StopAllLoops();
    }

    public RhythmEngineState GetEngineState()
    {
        var loopCopy = new bool[_activeLoops.Length];
        Array.Copy(_activeLoops, loopCopy, _activeLoops.Length);

        return new RhythmEngineState
        {
            bpm = _currentBpm,
            pitchSemitones = _currentPitchSemitones,
            reverbAmount = _currentReverbAmount,
            activeLoops = loopCopy
        };
    }

    public float GetBpmStep() => _bpmStep;
    public float GetPitchStep() => _pitchStep;
    public float GetReverbStep() => _reverbStep;

    private void EnsureVoiceDefinitions()
    {
        if (!_autoCreateDefaults && _voices != null && _voices.Length == VoiceCount)
        {
            return;
        }

        bool needsDefaults = _voices == null || _voices.Length != VoiceCount;
        if (!needsDefaults)
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i].defaultLoopSteps == null || _voices[i].defaultLoopSteps.Length != StepsPerBar)
                {
                    needsDefaults = true;
                    break;
                }
            }
        }

        if (needsDefaults)
        {
            _voices = CreateDefaultVoices();
        }
    }

    private void InitialiseClipCache()
    {
        int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        _voiceClips = new AudioClip[_voices.Length];
        for (int i = 0; i < _voices.Length; i++)
        {
            _voiceClips[i] = CreateVoiceClip(_voices[i], sampleRate, i);
        }
    }

    private void InitialiseSourcePool()
    {
        int count = Mathf.Max(MinimumPoolSize, _sourcePoolSize);
        _voiceSources = new AudioSource[count];
        _reverbFilters = new AudioReverbFilter[count];

        AudioSource rootSource = GetComponent<AudioSource>();
        rootSource.playOnAwake = false;
        rootSource.loop = false;
        rootSource.spatialBlend = 0f;
        rootSource.volume = 0f;

        for (int i = 0; i < count; i++)
        {
            var child = new GameObject($"RhythmVoiceSource_{i + 1}");
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = _masterVolume;
            source.priority = 64;

            var reverb = child.AddComponent<AudioReverbFilter>();
            reverb.enabled = true;
            reverb.reverbPreset = AudioReverbPreset.User;

            _voiceSources[i] = source;
            _reverbFilters[i] = reverb;
        }
    }

    private void ApplyReverbToPool()
    {
        if (_reverbFilters == null)
        {
            return;
        }

        float room = Mathf.Lerp(-9000f, -1500f, _currentReverbAmount);
        float decay = Mathf.Lerp(0.12f, 2.8f, _currentReverbAmount);
        float reflections = Mathf.Lerp(-10000f, -1000f, _currentReverbAmount);
        float reverb = Mathf.Lerp(-10000f, -800f, _currentReverbAmount);
        float dryLevel = Mathf.Lerp(0f, -1200f, _currentReverbAmount * 0.35f);

        for (int i = 0; i < _reverbFilters.Length; i++)
        {
            AudioReverbFilter filter = _reverbFilters[i];
            if (filter == null)
            {
                continue;
            }

            filter.dryLevel = dryLevel;
            filter.room = room;
            filter.roomHF = Mathf.Lerp(-100f, -2500f, _currentReverbAmount);
            filter.decayTime = decay;
            filter.decayHFRatio = Mathf.Lerp(0.15f, 0.85f, _currentReverbAmount);
            filter.reflectionsLevel = reflections;
            filter.reflectionsDelay = Mathf.Lerp(0.005f, 0.05f, _currentReverbAmount);
            filter.reverbLevel = reverb;
            filter.reverbDelay = Mathf.Lerp(0.01f, 0.08f, _currentReverbAmount);
            filter.hfReference = 5000f;
            filter.roomLF = 0;
            filter.lfReference = 250f;
            filter.diffusion = Mathf.Lerp(20f, 95f, _currentReverbAmount);
            filter.density = Mathf.Lerp(20f, 100f, _currentReverbAmount);
        }
    }

    private void AdvanceSequencer()
    {
        _transportStep = (_transportStep + 1) % StepsPerBar;

        for (int i = 0; i < _activeLoops.Length && i < _voices.Length; i++)
        {
            if (!_activeLoops[i])
            {
                continue;
            }

            bool[] pattern = _voices[i].defaultLoopSteps;
            if (pattern != null && pattern.Length == StepsPerBar && pattern[_transportStep])
            {
                PlayVoice(i);
            }
        }
    }

    private void PlayVoice(int soundIndex)
    {
        if (_voiceSources == null || _voiceSources.Length == 0 || _voiceClips == null || soundIndex >= _voiceClips.Length)
        {
            return;
        }

        AudioSource source = _voiceSources[_sourceCursor];
        _sourceCursor = (_sourceCursor + 1) % _voiceSources.Length;

        source.Stop();
        source.clip = _voiceClips[soundIndex];
        source.pitch = Mathf.Pow(2f, _currentPitchSemitones / 12f);
        source.volume = _masterVolume;
        source.Play();
    }

    private bool IsValidSoundIndex(int soundIndex)
    {
        return _voices != null && soundIndex >= 0 && soundIndex < _voices.Length;
    }

    private AudioClip CreateVoiceClip(RhythmVoiceDefinition definition, int sampleRate, int clipIndex)
    {
        float clipLengthSeconds = Mathf.Max(0.1f, definition.attackSeconds + definition.decaySeconds + definition.holdSeconds + definition.releaseSeconds + 0.05f);
        int sampleCount = Mathf.Max(256, Mathf.CeilToInt(clipLengthSeconds * sampleRate));
        float[] samples = new float[sampleCount];
        uint noiseState = (uint)(definition.baseFrequency.GetHashCode() ^ (clipIndex + 1) * 486187739);
        if (noiseState == 0)
        {
            noiseState = 1u;
        }

        double phase = 0d;
        float detuneMultiplier = Mathf.Pow(2f, definition.detuneCents / 1200f);
        float totalHold = Mathf.Max(0.01f, definition.holdSeconds);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float amplitude = EvaluateEnvelope(definition, time);
            float pitchDecayProgress = Mathf.Clamp01(time / totalHold);
            float pitchModifier = 1f + (definition.pitchDecayAmount * (1f - pitchDecayProgress));
            float vibrato = definition.vibratoDepth <= 0f
                ? 0f
                : Mathf.Sin(time * definition.vibratoRate * Mathf.PI * 2f) * definition.vibratoDepth;

            float frequency = Mathf.Max(10f, definition.baseFrequency * detuneMultiplier * (pitchModifier + vibrato));
            phase += frequency / sampleRate;
            phase -= Math.Floor(phase);

            float oscillator = SampleWaveform(definition.waveform, (float)phase, ref noiseState);
            float noise = NextNoise(ref noiseState);
            float blended = Mathf.Lerp(oscillator, noise, definition.noiseMix);
            samples[i] = blended * amplitude * definition.gain;
        }

        AudioClip clip = AudioClip.Create($"RhythmVoice_{clipIndex + 1}_{definition.name}", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float EvaluateEnvelope(RhythmVoiceDefinition definition, float timeSeconds)
    {
        float attack = Mathf.Max(0.0001f, definition.attackSeconds);
        float decay = Mathf.Max(0.0001f, definition.decaySeconds);
        float sustain = Mathf.Clamp01(definition.sustainLevel);
        float holdEnd = definition.attackSeconds + definition.decaySeconds + Mathf.Max(0f, definition.holdSeconds);

        if (timeSeconds <= definition.attackSeconds)
        {
            return timeSeconds / attack;
        }

        float decayStart = definition.attackSeconds;
        float decayEnd = decayStart + definition.decaySeconds;
        if (timeSeconds <= decayEnd)
        {
            float decayLerp = (timeSeconds - decayStart) / decay;
            return Mathf.Lerp(1f, sustain, decayLerp);
        }

        if (timeSeconds <= holdEnd)
        {
            return sustain;
        }

        float release = Mathf.Max(0.0001f, definition.releaseSeconds);
        float releaseElapsed = timeSeconds - holdEnd;
        float releaseLerp = Mathf.Clamp01(releaseElapsed / release);
        return Mathf.Lerp(sustain, 0f, releaseLerp);
    }

    private static float SampleWaveform(WaveformType waveform, float phase, ref uint noiseState)
    {
        switch (waveform)
        {
            case WaveformType.Square:
                return phase < 0.5f ? 1f : -1f;
            case WaveformType.Saw:
                return (phase * 2f) - 1f;
            case WaveformType.Triangle:
                return 1f - (4f * Mathf.Abs(phase - 0.5f));
            case WaveformType.Noise:
                return NextNoise(ref noiseState);
            default:
                return Mathf.Sin(phase * Mathf.PI * 2f);
        }
    }

    private static float NextNoise(ref uint noiseState)
    {
        noiseState ^= noiseState << 13;
        noiseState ^= noiseState >> 17;
        noiseState ^= noiseState << 5;
        return ((noiseState & 0x7fffffff) / (float)int.MaxValue) * 2f - 1f;
    }

    private RhythmVoiceDefinition[] CreateDefaultVoices()
    {
        return new[]
        {
            RhythmVoiceDefinition.Create("Kick", WaveformType.Sine, 55f, 0.95f, 0.001f, 0.07f, 0.0f, 0.08f, 0.05f, 0.02f, 0f, 0f, 0f, -0.72f, 0, 4, 8, 12),
            RhythmVoiceDefinition.Create("Snare", WaveformType.Noise, 180f, 0.7f, 0.001f, 0.05f, 0.0f, 0.05f, 0.08f, 0.95f, 0f, 0f, 0f, -0.25f, 4, 12),
            RhythmVoiceDefinition.Create("HiHat", WaveformType.Noise, 420f, 0.48f, 0.001f, 0.03f, 0.0f, 0.025f, 0.025f, 1.0f, 0f, 0f, 0f, 0f, 2, 6, 10, 14),
            RhythmVoiceDefinition.Create("Bass Pulse", WaveformType.Saw, 82.41f, 0.4f, 0.005f, 0.08f, 0.45f, 0.18f, 0.12f, 0.05f, -4f, 0f, 0f, -0.08f, 0, 3, 8, 11),
            RhythmVoiceDefinition.Create("Lead Stab", WaveformType.Square, 329.63f, 0.28f, 0.002f, 0.06f, 0.18f, 0.12f, 0.09f, 0.02f, 5f, 5.2f, 0.005f, 0f, 0, 5, 7, 12),
            RhythmVoiceDefinition.Create("Pad Hit", WaveformType.Triangle, 220f, 0.24f, 0.03f, 0.16f, 0.65f, 0.34f, 0.3f, 0.0f, -3f, 2.1f, 0.012f, 0f, 0, 8),
            RhythmVoiceDefinition.Create("Pluck", WaveformType.Saw, 440f, 0.26f, 0.001f, 0.09f, 0.06f, 0.09f, 0.07f, 0.03f, 9f, 6.8f, 0.01f, -0.12f, 1, 5, 9, 13),
            RhythmVoiceDefinition.Create("Arp Tone", WaveformType.Sine, 659.25f, 0.2f, 0.002f, 0.05f, 0.2f, 0.08f, 0.08f, 0.0f, 0f, 7.5f, 0.008f, 0f, 0, 2, 4, 6, 8, 10, 12, 14),
            RhythmVoiceDefinition.Create("FX Sweep", WaveformType.Triangle, 523.25f, 0.24f, 0.01f, 0.12f, 0.32f, 0.22f, 0.25f, 0.28f, 13f, 3.2f, 0.03f, 0.45f, 7, 15)
        };
    }
}
