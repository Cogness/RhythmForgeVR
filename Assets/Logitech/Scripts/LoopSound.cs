using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct LoopSoundSettings
{
    public float secondsPerMeter;
    public Vector2 loopDurationRange;
    public Vector2 lengthRange;
    public Vector2 frequencyRange;
    public float minVolume;
    public float maxVolume;
    public float beatAmplitude;
    public float beatDuration;
    public float beatFrequency;
    public float brightnessFromTilt;
    public float spatialBlend;
    public float minDistance;
    public float maxDistance;

    public static LoopSoundSettings CreateDefault()
    {
        return new LoopSoundSettings
        {
            secondsPerMeter = 0.6f,
            loopDurationRange = new Vector2(0.4f, 8f),
            lengthRange = new Vector2(0.2f, 2.0f),
            frequencyRange = new Vector2(110f, 660f),
            minVolume = 0.05f,
            maxVolume = 0.4f,
            beatAmplitude = 0.35f,
            beatDuration = 0.06f,
            beatFrequency = 800f,
            brightnessFromTilt = 0.85f,
            spatialBlend = 1f,
            minDistance = 0.2f,
            maxDistance = 3f,
        };
    }

    public static LoopSoundSettings Sanitize(LoopSoundSettings settings)
    {
        if (settings.secondsPerMeter <= 0f)
        {
            settings.secondsPerMeter = 0.6f;
        }
        if (settings.loopDurationRange.x <= 0f)
        {
            settings.loopDurationRange.x = 0.4f;
        }
        if (settings.loopDurationRange.y < settings.loopDurationRange.x)
        {
            settings.loopDurationRange.y = settings.loopDurationRange.x + 0.1f;
        }
        if (settings.lengthRange.x <= 0f)
        {
            settings.lengthRange.x = 0.2f;
        }
        if (settings.lengthRange.y < settings.lengthRange.x)
        {
            settings.lengthRange.y = settings.lengthRange.x + 0.2f;
        }
        if (settings.frequencyRange.x <= 0f)
        {
            settings.frequencyRange.x = 110f;
        }
        if (settings.frequencyRange.y < settings.frequencyRange.x)
        {
            settings.frequencyRange.y = settings.frequencyRange.x + 110f;
        }
        if (settings.maxVolume <= 0f)
        {
            settings.maxVolume = 0.4f;
        }
        if (settings.minVolume < 0f)
        {
            settings.minVolume = 0f;
        }
        if (settings.minVolume > settings.maxVolume)
        {
            settings.minVolume = settings.maxVolume * 0.5f;
        }
        if (settings.beatAmplitude < 0f)
        {
            settings.beatAmplitude = 0.35f;
        }
        if (settings.beatDuration <= 0.001f)
        {
            settings.beatDuration = 0.06f;
        }
        if (settings.beatFrequency <= 0f)
        {
            settings.beatFrequency = 800f;
        }
        if (settings.brightnessFromTilt < 0f)
        {
            settings.brightnessFromTilt = 0f;
        }
        if (settings.spatialBlend < 0f)
        {
            settings.spatialBlend = 0f;
        }
        if (settings.spatialBlend > 1f)
        {
            settings.spatialBlend = 1f;
        }
        if (settings.minDistance <= 0f)
        {
            settings.minDistance = 0.2f;
        }
        if (settings.maxDistance < settings.minDistance)
        {
            settings.maxDistance = settings.minDistance + 0.5f;
        }
        return settings;
    }
}

[RequireComponent(typeof(AudioSource))]
public class LoopSound : MonoBehaviour
{
    private const float SpikeMergeEpsilon = 0.01f;
    private const float SpikeAngleDegrees = 65f;
    private const float SpikeMinSegmentLength = 0.003f;
    private const int SpikeMinIndexGap = 3;

    private AudioSource _audioSource;
    private LineStrokeData _baseStroke;
    private LoopSoundSettings _settings;
    private readonly List<float> _spikeTimesNormalized = new List<float>();

    private Vector3 _center;
    private Vector3 _normal;
    private Vector3 _axisX;
    private Vector3 _axisY;
    private float _averageRadius;

    public Vector3 Center => _center;
    public float AverageRadius => _averageRadius;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void Initialize(LineStrokeData stroke, LoopSoundSettings settings)
    {
        _baseStroke = stroke;
        _settings = LoopSoundSettings.Sanitize(settings);

        ComputeLoopPlane();
        _spikeTimesNormalized.Clear();
        _spikeTimesNormalized.AddRange(LoopSoundUtils.DetectSpikeTimesByPath(stroke, SpikeAngleDegrees, SpikeMinSegmentLength, SpikeMinIndexGap));
        NormalizeSpikeTimes();
        BuildClip();
    }

    public List<float> AddDetailStroke(LineStrokeData detailStroke)
    {
        List<float> addedSpikes = LoopSoundUtils.MapDetailStrokeToTimes(detailStroke, _center, _axisX, _axisY,
            SpikeAngleDegrees, SpikeMinSegmentLength, SpikeMinIndexGap);

        if (addedSpikes.Count == 0 && detailStroke.Count > 0)
        {
            float fallbackTime = LoopSoundUtils.MapPointToLoopTime(detailStroke.Centroid, _center, _axisX, _axisY);
            addedSpikes.Add(fallbackTime);
        }

        AddSpikes(addedSpikes);
        return addedSpikes;
    }

    public void AddSpikes(List<float> spikeTimesNormalized)
    {
        if (spikeTimesNormalized == null || spikeTimesNormalized.Count == 0)
        {
            return;
        }

        bool anyAdded = false;
        for (int i = 0; i < spikeTimesNormalized.Count; i++)
        {
            float time = Mathf.Repeat(spikeTimesNormalized[i], 1f);
            if (!ContainsSpike(time))
            {
                _spikeTimesNormalized.Add(time);
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            NormalizeSpikeTimes();
            BuildClip();
        }
    }

    public void RemoveSpikes(List<float> spikeTimesNormalized)
    {
        if (spikeTimesNormalized == null || spikeTimesNormalized.Count == 0)
        {
            return;
        }

        bool anyRemoved = false;
        for (int i = _spikeTimesNormalized.Count - 1; i >= 0; i--)
        {
            float existing = _spikeTimesNormalized[i];
            for (int j = 0; j < spikeTimesNormalized.Count; j++)
            {
                float candidate = Mathf.Repeat(spikeTimesNormalized[j], 1f);
                if (Mathf.Abs(existing - candidate) <= SpikeMergeEpsilon)
                {
                    _spikeTimesNormalized.RemoveAt(i);
                    anyRemoved = true;
                    break;
                }
            }
        }

        if (anyRemoved)
        {
            NormalizeSpikeTimes();
            BuildClip();
        }
    }

    private void ComputeLoopPlane()
    {
        _center = _baseStroke.Centroid;
        _normal = LoopSoundUtils.ComputePlaneNormal(_baseStroke.Points);
        if (_normal.sqrMagnitude <= 0.0001f)
        {
            _normal = Vector3.up;
        }
        _normal.Normalize();

        _axisX = Vector3.zero;
        if (_baseStroke.Count > 0)
        {
            _axisX = Vector3.ProjectOnPlane(_baseStroke.Points[0] - _center, _normal);
        }
        if (_axisX.sqrMagnitude <= 0.0001f)
        {
            _axisX = Vector3.Cross(_normal, Vector3.right);
        }
        if (_axisX.sqrMagnitude <= 0.0001f)
        {
            _axisX = Vector3.Cross(_normal, Vector3.forward);
        }
        _axisX.Normalize();
        _axisY = Vector3.Cross(_normal, _axisX).normalized;

        _averageRadius = 0f;
        if (_baseStroke.Count > 0)
        {
            for (int i = 0; i < _baseStroke.Count; i++)
            {
                _averageRadius += Vector3.Distance(_center, _baseStroke.Points[i]);
            }
            _averageRadius /= _baseStroke.Count;
        }

        transform.position = _center;
    }

    private void BuildClip()
    {
        if (_audioSource == null || _baseStroke == null)
        {
            return;
        }

        float loopDuration = Mathf.Clamp(_baseStroke.TotalLength * _settings.secondsPerMeter,
            _settings.loopDurationRange.x, _settings.loopDurationRange.y);
        int sampleRate = AudioSettings.outputSampleRate;
        int totalSamples = Mathf.Max(1, Mathf.CeilToInt(loopDuration * sampleRate));

        float lengthT = Mathf.InverseLerp(_settings.lengthRange.x, _settings.lengthRange.y, _baseStroke.TotalLength);
        float baseFrequency = Mathf.Lerp(_settings.frequencyRange.y, _settings.frequencyRange.x, lengthT);
        float brightness = Mathf.Clamp01(_baseStroke.AverageTilt * _settings.brightnessFromTilt);
        float volume = Mathf.Lerp(_settings.minVolume, _settings.maxVolume, Mathf.Clamp01(_baseStroke.AveragePressure));

        float[] data = new float[totalSamples];
        float invSampleRate = 1f / sampleRate;
        float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i * invSampleRate;
            float phase = twoPi * baseFrequency * t;
            float sine = Mathf.Sin(phase);
            float square = Mathf.Sign(sine);
            float wave = Mathf.Lerp(sine, square, brightness);
            float sample = wave * volume;

            for (int s = 0; s < _spikeTimesNormalized.Count; s++)
            {
                float spikeTime = _spikeTimesNormalized[s] * loopDuration;
                float dt = t - spikeTime;
                if (dt < 0f)
                {
                    dt += loopDuration;
                }
                if (dt < _settings.beatDuration)
                {
                    float env = 1f - (dt / _settings.beatDuration);
                    sample += env * Mathf.Sin(twoPi * _settings.beatFrequency * dt) * _settings.beatAmplitude * volume;
                }
            }

            data[i] = Mathf.Clamp(sample, -1f, 1f);
        }

        if (_audioSource.clip != null)
        {
            Destroy(_audioSource.clip);
        }

        AudioClip clip = AudioClip.Create("LoopSound", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        _audioSource.clip = clip;
        _audioSource.loop = true;
        _audioSource.spatialBlend = _settings.spatialBlend;
        _audioSource.minDistance = _settings.minDistance;
        _audioSource.maxDistance = _settings.maxDistance;
        _audioSource.Play();
    }

    private bool ContainsSpike(float normalizedTime)
    {
        for (int i = 0; i < _spikeTimesNormalized.Count; i++)
        {
            if (Mathf.Abs(_spikeTimesNormalized[i] - normalizedTime) <= SpikeMergeEpsilon)
            {
                return true;
            }
        }
        return false;
    }

    private void NormalizeSpikeTimes()
    {
        _spikeTimesNormalized.Sort();
        for (int i = _spikeTimesNormalized.Count - 1; i > 0; i--)
        {
            if (Mathf.Abs(_spikeTimesNormalized[i] - _spikeTimesNormalized[i - 1]) <= SpikeMergeEpsilon)
            {
                _spikeTimesNormalized.RemoveAt(i);
            }
        }
    }

    private void OnDestroy()
    {
        if (_audioSource != null && _audioSource.clip != null)
        {
            Destroy(_audioSource.clip);
        }
    }
}

internal static class LoopSoundUtils
{
    public static Vector3 ComputePlaneNormal(IReadOnlyList<Vector3> points)
    {
        int count = points.Count;
        if (count < 3)
        {
            return Vector3.up;
        }

        Vector3 normal = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Vector3 current = points[i];
            Vector3 next = points[(i + 1) % count];
            normal.x += (current.y - next.y) * (current.z + next.z);
            normal.y += (current.z - next.z) * (current.x + next.x);
            normal.z += (current.x - next.x) * (current.y + next.y);
        }
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return Vector3.up;
        }
        return normal.normalized;
    }

    public static List<int> DetectSpikeIndices(IReadOnlyList<Vector3> points, float angleThresholdDegrees, float minSegmentLength, int minIndexGap)
    {
        List<int> spikes = new List<int>();
        if (points.Count < 3)
        {
            return spikes;
        }

        int lastSpikeIndex = -minIndexGap;
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 prev = points[i] - points[i - 1];
            Vector3 next = points[i + 1] - points[i];
            float prevLength = prev.magnitude;
            float nextLength = next.magnitude;
            if (prevLength < minSegmentLength || nextLength < minSegmentLength)
            {
                continue;
            }
            float angle = Vector3.Angle(prev, next);
            if (angle <= angleThresholdDegrees && i - lastSpikeIndex >= minIndexGap)
            {
                spikes.Add(i);
                lastSpikeIndex = i;
            }
        }
        return spikes;
    }

    public static List<float> DetectSpikeTimesByPath(LineStrokeData stroke, float angleThresholdDegrees, float minSegmentLength, int minIndexGap)
    {
        List<int> spikeIndices = DetectSpikeIndices(stroke.Points, angleThresholdDegrees, minSegmentLength, minIndexGap);
        List<float> times = new List<float>();
        if (spikeIndices.Count == 0 || stroke.Count < 2 || stroke.TotalLength <= 0.0001f)
        {
            return times;
        }

        spikeIndices.Sort();
        float distance = 0f;
        int spikeCursor = 0;
        for (int i = 1; i < stroke.Count && spikeCursor < spikeIndices.Count; i++)
        {
            distance += Vector3.Distance(stroke.Points[i - 1], stroke.Points[i]);
            if (i == spikeIndices[spikeCursor])
            {
                times.Add(Mathf.Clamp01(distance / stroke.TotalLength));
                spikeCursor++;
            }
        }
        return times;
    }

    public static List<float> MapDetailStrokeToTimes(LineStrokeData detailStroke, Vector3 center, Vector3 axisX, Vector3 axisY,
        float angleThresholdDegrees, float minSegmentLength, int minIndexGap)
    {
        List<int> spikeIndices = DetectSpikeIndices(detailStroke.Points, angleThresholdDegrees, minSegmentLength, minIndexGap);
        List<float> times = new List<float>();
        if (spikeIndices.Count == 0)
        {
            return times;
        }

        for (int i = 0; i < spikeIndices.Count; i++)
        {
            int index = spikeIndices[i];
            if (index >= 0 && index < detailStroke.Count)
            {
                float time = MapPointToLoopTime(detailStroke.Points[index], center, axisX, axisY);
                times.Add(time);
            }
        }
        return times;
    }

    public static float MapPointToLoopTime(Vector3 point, Vector3 center, Vector3 axisX, Vector3 axisY)
    {
        Vector3 fromCenter = point - center;
        float x = Vector3.Dot(fromCenter, axisX);
        float y = Vector3.Dot(fromCenter, axisY);
        float angle = Mathf.Atan2(y, x);
        float time = (angle + Mathf.PI) / (2f * Mathf.PI);
        return Mathf.Repeat(time, 1f);
    }
}
