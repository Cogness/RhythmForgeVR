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
    [SerializeField] private float _bpmStep = 5f;

    [Header("Global Sound")]
    [SerializeField] private float _pitchStep = 1f;
    [SerializeField] private float _reverbStep = 0.05f;

    [Header("Presets")]
    [SerializeField] private bool _autoCreateBuiltInPresets = true;
    [SerializeField] private RhythmSoundPreset[] _presets = Array.Empty<RhythmSoundPreset>();
    [SerializeField] private int _defaultPresetIndex;

    [Header("Playback")]
    [SerializeField] private int _sourcePoolSize = 18;

    private AudioSource[] _voiceSources = Array.Empty<AudioSource>();
    private AudioReverbFilter[] _reverbFilters = Array.Empty<AudioReverbFilter>();
    private AudioClip[] _proceduralClips = Array.Empty<AudioClip>();
    private RhythmSoundSlot[] _slots = Array.Empty<RhythmSoundSlot>();
    private bool[] _activeLoops = new bool[VoiceCount];
    private int _sourceCursor;
    private int _transportStep = -1;
    private float _stepTimer;
    private float _currentBpm;
    private float _currentPitchSemitones;
    private float _currentReverbAmount;
    private float _masterVolume = 0.8f;
    private int _currentPresetIndex = -1;

    public float Bpm => _currentBpm;
    public float PitchSemitones => _currentPitchSemitones;
    public float ReverbAmount => _currentReverbAmount;
    public int RegisteredSoundCount => _slots != null ? _slots.Length : 0;
    public int PresetCount => _presets != null ? _presets.Length : 0;
    public int CurrentPresetIndex => _currentPresetIndex;
    public string CurrentPresetName => IsValidPresetIndex(_currentPresetIndex) ? _presets[_currentPresetIndex].presetName : string.Empty;

    public event Action<int, float> SoundTriggered;

    private void Awake()
    {
        EnsurePresetDefinitions();
        InitialiseSourcePool();
        SetPresetInternal(Mathf.Clamp(_defaultPresetIndex, 0, Mathf.Max(0, PresetCount - 1)), true);
        Debug.Log($"RhythmSoundEngine: ready with preset '{CurrentPresetName}'.");
    }

    private void Update()
    {
        if (_slots == null || _slots.Length == 0)
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

        PlaySlot(soundIndex);
    }

    public void ToggleLoop(int soundIndex)
    {
        if (!IsValidSoundIndex(soundIndex))
        {
            Debug.LogWarning($"RhythmSoundEngine: invalid loop index {soundIndex}.");
            return;
        }

        _activeLoops[soundIndex] = !_activeLoops[soundIndex];
        Debug.Log($"RhythmSoundEngine: loop {(_activeLoops[soundIndex] ? "enabled" : "disabled")} for {_slots[soundIndex].name}.");
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
        ApplyCurrentPresetDefaults();
        StopAllLoops();
        ResetTransport();
        ApplyReverbToPool();
    }

    public void SetPreset(int presetIndex)
    {
        if (!IsValidPresetIndex(presetIndex))
        {
            Debug.LogWarning($"RhythmSoundEngine: invalid preset index {presetIndex}.");
            return;
        }

        SetPresetInternal(presetIndex, false);
    }

    public void SetPreset(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName) || _presets == null)
        {
            return;
        }

        for (int i = 0; i < _presets.Length; i++)
        {
            if (string.Equals(_presets[i].presetName, presetName, StringComparison.OrdinalIgnoreCase))
            {
                SetPresetInternal(i, false);
                return;
            }
        }

        Debug.LogWarning($"RhythmSoundEngine: preset '{presetName}' was not found.");
    }

    public void NextPreset()
    {
        if (PresetCount == 0)
        {
            return;
        }

        SetPresetInternal((_currentPresetIndex + 1) % PresetCount, false);
    }

    public void PreviousPreset()
    {
        if (PresetCount == 0)
        {
            return;
        }

        int nextIndex = _currentPresetIndex - 1;
        if (nextIndex < 0)
        {
            nextIndex = PresetCount - 1;
        }

        SetPresetInternal(nextIndex, false);
    }

    public string[] GetPresetNames()
    {
        if (_presets == null)
        {
            return Array.Empty<string>();
        }

        string[] names = new string[_presets.Length];
        for (int i = 0; i < _presets.Length; i++)
        {
            names[i] = _presets[i].presetName;
        }

        return names;
    }

    public RhythmEngineState GetEngineState()
    {
        var loopCopy = new bool[_activeLoops.Length];
        Array.Copy(_activeLoops, loopCopy, _activeLoops.Length);

        return new RhythmEngineState
        {
            presetIndex = _currentPresetIndex,
            presetName = CurrentPresetName,
            bpm = _currentBpm,
            pitchSemitones = _currentPitchSemitones,
            reverbAmount = _currentReverbAmount,
            activeLoops = loopCopy
        };
    }

    public float GetBpmStep() => _bpmStep;
    public float GetPitchStep() => _pitchStep;
    public float GetReverbStep() => _reverbStep;

    private void EnsurePresetDefinitions()
    {
        bool needsBuiltIns = _presets == null || _presets.Length == 0;
        if (!needsBuiltIns)
        {
            for (int i = 0; i < _presets.Length; i++)
            {
                if (_presets[i] == null || _presets[i].slots == null || _presets[i].slots.Length != VoiceCount)
                {
                    needsBuiltIns = true;
                    break;
                }

                for (int slotIndex = 0; slotIndex < _presets[i].slots.Length; slotIndex++)
                {
                    bool[] steps = _presets[i].slots[slotIndex].defaultLoopSteps;
                    if (steps == null || steps.Length != StepsPerBar)
                    {
                        needsBuiltIns = true;
                        break;
                    }
                }

                if (needsBuiltIns)
                {
                    break;
                }
            }
        }

        if (_autoCreateBuiltInPresets && needsBuiltIns)
        {
            _presets = RhythmBuiltInPresets.CreateBuiltInPresets();
        }
    }

    private void SetPresetInternal(int presetIndex, bool isInitial)
    {
        if (!IsValidPresetIndex(presetIndex))
        {
            return;
        }

        _currentPresetIndex = presetIndex;
        RhythmSoundPreset preset = _presets[presetIndex];
        _slots = CloneSlots(preset.slots);
        LoadSamplesFromResourcesForCurrentPreset();
        _masterVolume = Mathf.Clamp(preset.masterVolume, 0.1f, 1f);

        ApplyCurrentPresetDefaults();
        StopAllLoops();
        StopAllSources();
        ResetTransport();
        RebuildProceduralClips();
        UpdateSourceVolumes();
        ApplyReverbToPool();

        if (!isInitial)
        {
            Debug.Log($"RhythmSoundEngine: switched to preset '{preset.presetName}'.");
        }
    }

    private void ApplyCurrentPresetDefaults()
    {
        if (!IsValidPresetIndex(_currentPresetIndex))
        {
            return;
        }

        RhythmSoundPreset preset = _presets[_currentPresetIndex];
        _currentBpm = Mathf.Clamp(preset.defaultBpm, MinimumBpm, MaximumBpm);
        _currentPitchSemitones = Mathf.Clamp(preset.defaultPitchSemitones, MinimumPitchSemitones, MaximumPitchSemitones);
        _currentReverbAmount = Mathf.Clamp01(preset.defaultReverbAmount);
    }

    private void LoadSamplesFromResourcesForCurrentPreset()
    {
        if (!IsValidPresetIndex(_currentPresetIndex) || _slots == null || _slots.Length == 0)
        {
            return;
        }

        string presetName = _presets[_currentPresetIndex].presetName;
        string[] resourcePaths =
        {
            $"AudioPresets/{presetName}",
            $"AudioPresets/{presetName.Replace(" ", string.Empty)}"
        };

        AudioClip[] loadedClips = Array.Empty<AudioClip>();
        for (int i = 0; i < resourcePaths.Length; i++)
        {
            loadedClips = Resources.LoadAll<AudioClip>(resourcePaths[i]);
            if (loadedClips != null && loadedClips.Length > 0)
            {
                break;
            }
        }

        if (loadedClips == null || loadedClips.Length == 0)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
        {
            var matching = new System.Collections.Generic.List<AudioClip>();
            for (int clipIndex = 0; clipIndex < loadedClips.Length; clipIndex++)
            {
                AudioClip clip = loadedClips[clipIndex];
                if (clip == null)
                {
                    continue;
                }

                if (TryGetSlotIndexFromClipName(clip.name, out int parsedIndex) && parsedIndex == slotIndex)
                {
                    matching.Add(clip);
                }
            }

            if (matching.Count > 0)
            {
                _slots[slotIndex].sampleClips = matching.ToArray();
            }
        }
    }

    private void RebuildProceduralClips()
    {
        int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        _proceduralClips = new AudioClip[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            _proceduralClips[i] = CreateProceduralClip(_slots[i].proceduralVoice, i, sampleRate);
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
            source.priority = 64;

            var reverb = child.AddComponent<AudioReverbFilter>();
            reverb.enabled = true;
            reverb.reverbPreset = AudioReverbPreset.User;

            _voiceSources[i] = source;
            _reverbFilters[i] = reverb;
        }
    }

    private void UpdateSourceVolumes()
    {
        if (_voiceSources == null)
        {
            return;
        }

        for (int i = 0; i < _voiceSources.Length; i++)
        {
            if (_voiceSources[i] != null)
            {
                _voiceSources[i].volume = _masterVolume;
            }
        }
    }

    private void ApplyReverbToPool()
    {
        if (_reverbFilters == null)
        {
            return;
        }

        float room = Mathf.Lerp(-9000f, -1500f, _currentReverbAmount);
        float decay = Mathf.Lerp(0.12f, 3.2f, _currentReverbAmount);
        float reflections = Mathf.Lerp(-10000f, -1000f, _currentReverbAmount);
        float reverb = Mathf.Lerp(-10000f, -700f, _currentReverbAmount);
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
            filter.roomHF = Mathf.Lerp(-100f, -2600f, _currentReverbAmount);
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

        for (int i = 0; i < _activeLoops.Length && i < _slots.Length; i++)
        {
            if (!_activeLoops[i])
            {
                continue;
            }

            bool[] pattern = _slots[i].defaultLoopSteps;
            if (pattern != null && pattern.Length == StepsPerBar && pattern[_transportStep])
            {
                PlaySlot(i);
            }
        }
    }

    private void PlaySlot(int soundIndex)
    {
        if (_voiceSources == null || _voiceSources.Length == 0 || soundIndex >= _slots.Length)
        {
            return;
        }

        RhythmSoundSlot slot = _slots[soundIndex];
        AudioClip clipToPlay = ChooseSampleClip(slot);
        if (clipToPlay == null && _proceduralClips != null && soundIndex < _proceduralClips.Length)
        {
            clipToPlay = _proceduralClips[soundIndex];
        }

        if (clipToPlay == null)
        {
            return;
        }

        AudioSource source = _voiceSources[_sourceCursor];
        _sourceCursor = (_sourceCursor + 1) % _voiceSources.Length;

        source.Stop();
        source.clip = clipToPlay;
        source.pitch = Mathf.Pow(2f, (_currentPitchSemitones + slot.pitchOffsetSemitones) / 12f);
        source.volume = _masterVolume * Mathf.Clamp(slot.volumeMultiplier, 0.1f, 2f);
        source.Play();
        SoundTriggered?.Invoke(soundIndex, Mathf.Clamp01(source.volume));
    }

    private AudioClip ChooseSampleClip(RhythmSoundSlot slot)
    {
        if (slot.sampleClips == null || slot.sampleClips.Length == 0)
        {
            return null;
        }

        int nonNullCount = 0;
        for (int i = 0; i < slot.sampleClips.Length; i++)
        {
            if (slot.sampleClips[i] != null)
            {
                nonNullCount++;
            }
        }

        if (nonNullCount == 0)
        {
            return null;
        }

        int selection = UnityEngine.Random.Range(0, nonNullCount);
        for (int i = 0; i < slot.sampleClips.Length; i++)
        {
            if (slot.sampleClips[i] == null)
            {
                continue;
            }

            if (selection == 0)
            {
                return slot.sampleClips[i];
            }

            selection--;
        }

        return null;
    }

    private void StopAllSources()
    {
        if (_voiceSources == null)
        {
            return;
        }

        for (int i = 0; i < _voiceSources.Length; i++)
        {
            if (_voiceSources[i] != null)
            {
                _voiceSources[i].Stop();
            }
        }
    }

    private void ResetTransport()
    {
        _stepTimer = 0f;
        _transportStep = -1;
    }

    private bool IsValidSoundIndex(int soundIndex)
    {
        return _slots != null && soundIndex >= 0 && soundIndex < _slots.Length;
    }

    private bool IsValidPresetIndex(int presetIndex)
    {
        return _presets != null && presetIndex >= 0 && presetIndex < _presets.Length;
    }

    private static bool TryGetSlotIndexFromClipName(string clipName, out int slotIndex)
    {
        slotIndex = -1;
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return false;
        }

        int value = 0;
        int digits = 0;
        for (int i = 0; i < clipName.Length; i++)
        {
            char c = clipName[i];
            if (!char.IsDigit(c))
            {
                break;
            }

            value = (value * 10) + (c - '0');
            digits++;
        }

        if (digits == 0 || value < 1 || value > VoiceCount)
        {
            return false;
        }

        slotIndex = value - 1;
        return true;
    }

    private AudioClip CreateProceduralClip(RhythmVoiceDefinition definition, int clipIndex, int sampleRate)
    {
        float clipLengthSeconds = Mathf.Max(0.12f, definition.attackSeconds + definition.decaySeconds + definition.holdSeconds + definition.releaseSeconds + 0.05f);
        int sampleCount = Mathf.Max(256, Mathf.CeilToInt(clipLengthSeconds * sampleRate));
        float[] samples = new float[sampleCount];
        uint noiseState = (uint)(definition.baseFrequency.GetHashCode() ^ ((clipIndex + 1) * 486187739));
        if (noiseState == 0)
        {
            noiseState = 1u;
        }

        double primaryPhase = 0d;
        double subPhase = 0d;
        double overtonePhase = 0d;
        float filterState = 0f;
        float detuneMultiplier = Mathf.Pow(2f, definition.detuneCents / 1200f);
        float transientDecay = Mathf.Max(0.001f, definition.transientDecaySeconds);
        float peak = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float progress = i / (float)sampleCount;
            float amplitude = EvaluateEnvelope(definition, time);
            float pitchHold = Mathf.Max(0.01f, definition.holdSeconds);
            float pitchDecayProgress = Mathf.Clamp01(time / pitchHold);
            float pitchModifier = 1f + (definition.pitchDecayAmount * (1f - pitchDecayProgress));
            float vibrato = definition.vibratoDepth <= 0f
                ? 0f
                : Mathf.Sin(time * definition.vibratoRate * Mathf.PI * 2f) * definition.vibratoDepth;

            float frequency = Mathf.Max(10f, definition.baseFrequency * detuneMultiplier * (pitchModifier + vibrato));
            primaryPhase = AdvancePhase(primaryPhase, frequency, sampleRate);
            subPhase = AdvancePhase(subPhase, frequency * 0.5f, sampleRate);
            overtonePhase = AdvancePhase(overtonePhase, frequency * Mathf.Max(0.5f, definition.overtoneRatio), sampleRate);

            float primary = SampleWaveform(definition.waveform, (float)primaryPhase);
            float sub = Mathf.Sin((float)(subPhase * Mathf.PI * 2d));
            float overtone = SampleWaveform(definition.waveform, (float)overtonePhase);
            float tonal = MixOscillators(primary, sub, overtone, definition.subOscillatorMix, definition.overtoneMix);
            float transient = NextNoise(ref noiseState) * definition.transientMix * Mathf.Exp(-time / transientDecay);
            float noisy = Mathf.Lerp(tonal, NextNoise(ref noiseState), definition.noiseMix) + transient;

            float cutoffNorm = Mathf.Clamp01(definition.brightness + definition.brightnessDecay * (1f - progress));
            float cutoffHz = Mathf.Lerp(180f, 14000f, cutoffNorm);
            float filtered = ApplyLowPass(noisy, cutoffHz, sampleRate, ref filterState);
            float saturated = ApplySaturation(filtered, definition.saturation);
            float finalSample = saturated * amplitude * definition.gain;
            samples[i] = finalSample;
            peak = Mathf.Max(peak, Mathf.Abs(finalSample));
        }

        if (peak > 0.98f)
        {
            float normalise = 0.98f / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= normalise;
            }
        }

        AudioClip clip = AudioClip.Create($"RhythmVoice_{clipIndex + 1}_{_slots[clipIndex].name}", sampleCount, 1, sampleRate, false);
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

    private static double AdvancePhase(double currentPhase, float frequency, int sampleRate)
    {
        currentPhase += frequency / sampleRate;
        currentPhase -= Math.Floor(currentPhase);
        return currentPhase;
    }

    private static float MixOscillators(float primary, float sub, float overtone, float subMix, float overtoneMix)
    {
        float baseMix = Mathf.Clamp01(1f - (subMix * 0.65f) - (overtoneMix * 0.65f));
        float subWeight = subMix * 0.8f;
        float overtoneWeight = overtoneMix * 0.75f;
        float totalWeight = Mathf.Max(0.0001f, baseMix + subWeight + overtoneWeight);
        return ((primary * baseMix) + (sub * subWeight) + (overtone * overtoneWeight)) / totalWeight;
    }

    private static float ApplyLowPass(float input, float cutoffHz, int sampleRate, ref float filterState)
    {
        float omega = 2f * Mathf.PI * cutoffHz / sampleRate;
        float alpha = 1f - Mathf.Exp(-omega);
        filterState += alpha * (input - filterState);
        return filterState;
    }

    private static float ApplySaturation(float sample, float amount)
    {
        if (amount <= 0.0001f)
        {
            return sample;
        }

        float drive = 1f + (amount * 8f);
        float shaped = (float)Math.Tanh(sample * drive);
        float normalised = (float)Math.Tanh(drive);
        return normalised > 0.0001f ? shaped / normalised : shaped;
    }

    private static float SampleWaveform(WaveformType waveform, float phase)
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
                return 0f;
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

    private static RhythmSoundSlot[] CloneSlots(RhythmSoundSlot[] source)
    {
        if (source == null)
        {
            return Array.Empty<RhythmSoundSlot>();
        }

        var clone = new RhythmSoundSlot[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            RhythmSoundSlot sourceSlot = source[i] ?? new RhythmSoundSlot();
            clone[i] = new RhythmSoundSlot
            {
                name = sourceSlot.name,
                volumeMultiplier = sourceSlot.volumeMultiplier,
                pitchOffsetSemitones = sourceSlot.pitchOffsetSemitones,
                proceduralVoice = sourceSlot.proceduralVoice,
                sampleClips = sourceSlot.sampleClips != null ? (AudioClip[])sourceSlot.sampleClips.Clone() : Array.Empty<AudioClip>(),
                defaultLoopSteps = sourceSlot.defaultLoopSteps != null ? (bool[])sourceSlot.defaultLoopSteps.Clone() : new bool[StepsPerBar]
            };
        }

        return clone;
    }
}



