using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MusicReactiveSignalSource : MonoBehaviour, IMusicReactiveSignalSource
{
    [SerializeField] private float _riseSpeed = 8f;
    [SerializeField] private float _fallSpeed = 1.4f;
    [SerializeField] private float _beatCooldown = 0.18f;
    [SerializeField] private float _minimumActivationLoudness = 0.08f;

    private float _targetLoudness;
    private float _loudness;
    private float _lastBeatTime = -100f;
    private bool _hasReceivedPlayerMusic;

    public float Loudness01 => _loudness;
    public bool HasReceivedPlayerMusic => _hasReceivedPlayerMusic;

    public event Action<float> BeatDetected;
    public event Action PlayerMusicStarted;

    public void InjectPulse(float intensity01, bool isBeat)
    {
        intensity01 = Mathf.Clamp01(intensity01);
        if (intensity01 <= 0f)
        {
            return;
        }

        _targetLoudness = Mathf.Max(_targetLoudness, intensity01);
        if (!_hasReceivedPlayerMusic && intensity01 >= _minimumActivationLoudness)
        {
            _hasReceivedPlayerMusic = true;
            PlayerMusicStarted?.Invoke();
        }

        if (isBeat && Time.time >= _lastBeatTime + _beatCooldown)
        {
            _lastBeatTime = Time.time;
            BeatDetected?.Invoke(intensity01);
        }
    }

    public void ResetSignal()
    {
        _targetLoudness = 0f;
        _loudness = 0f;
        _lastBeatTime = -100f;
        _hasReceivedPlayerMusic = false;
    }

    private void Update()
    {
        _targetLoudness = Mathf.MoveTowards(_targetLoudness, 0f, _fallSpeed * Time.deltaTime);
        float blendSpeed = _targetLoudness > _loudness ? _riseSpeed : _fallSpeed;
        _loudness = Mathf.MoveTowards(_loudness, _targetLoudness, blendSpeed * Time.deltaTime);
    }
}
