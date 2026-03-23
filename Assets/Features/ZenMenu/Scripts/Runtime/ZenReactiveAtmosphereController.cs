using UnityEngine;

[DisallowMultipleComponent]
public class ZenReactiveAtmosphereController : MonoBehaviour
{
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private Light _directionalLight;
    [SerializeField] private ParticleSystem _motes;
    [SerializeField] private float _baseLightIntensity = 1.15f;
    [SerializeField] private float _peakLightIntensity = 1.35f;
    [SerializeField] private float _baseFogDensity = 0.0032f;
    [SerializeField] private float _peakFogDensity = 0.0041f;
    [SerializeField] private float _responseSpeed = 1.5f;

    private float _smoothedLoudness;
    private ParticleSystem.EmissionModule _emissionModule;

    private void Awake()
    {
        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }

        if (_directionalLight == null)
        {
            _directionalLight = RenderSettings.sun;
        }

        if (_motes != null)
        {
            _emissionModule = _motes.emission;
        }
    }

    private void Update()
    {
        float target = _signalSource != null ? _signalSource.Loudness01 : 0f;
        _smoothedLoudness = Mathf.MoveTowards(_smoothedLoudness, target, _responseSpeed * Time.deltaTime);

        if (_directionalLight != null)
        {
            _directionalLight.intensity = Mathf.Lerp(_baseLightIntensity, _peakLightIntensity, _smoothedLoudness);
        }

        RenderSettings.fogDensity = Mathf.Lerp(_baseFogDensity, _peakFogDensity, _smoothedLoudness * 0.6f);

        if (_motes != null)
        {
            _emissionModule.rateOverTime = Mathf.Lerp(8f, 20f, _smoothedLoudness);
            var main = _motes.main;
            main.simulationSpeed = Mathf.Lerp(0.65f, 1.4f, _smoothedLoudness);
        }
    }
}
