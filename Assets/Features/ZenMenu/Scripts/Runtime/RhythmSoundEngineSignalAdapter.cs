using UnityEngine;

[DisallowMultipleComponent]
public class RhythmSoundEngineSignalAdapter : MonoBehaviour
{
    [SerializeField] private RhythmSoundEngine _engine;
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private int[] _beatVoiceIndices = { 0, 1, 2, 8 };
    [SerializeField] private float _defaultPulseStrength = 0.42f;
    [SerializeField] private float _beatPulseBonus = 0.28f;

    private void Awake()
    {
        if (_engine == null)
        {
            _engine = FindFirstObjectByType<RhythmSoundEngine>();
        }

        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }
    }

    private void OnEnable()
    {
        if (_engine != null)
        {
            _engine.SoundTriggered += HandleSoundTriggered;
        }
    }

    private void OnDisable()
    {
        if (_engine != null)
        {
            _engine.SoundTriggered -= HandleSoundTriggered;
        }
    }

    private void HandleSoundTriggered(int soundIndex, float intensity)
    {
        if (_signalSource == null)
        {
            return;
        }

        bool isBeat = IsBeatVoice(soundIndex);
        float pulseStrength = Mathf.Clamp01(_defaultPulseStrength + intensity + (isBeat ? _beatPulseBonus : 0f));
        _signalSource.InjectPulse(pulseStrength, isBeat);
    }

    private bool IsBeatVoice(int soundIndex)
    {
        if (_beatVoiceIndices == null)
        {
            return false;
        }

        for (int i = 0; i < _beatVoiceIndices.Length; i++)
        {
            if (_beatVoiceIndices[i] == soundIndex)
            {
                return true;
            }
        }

        return false;
    }
}
