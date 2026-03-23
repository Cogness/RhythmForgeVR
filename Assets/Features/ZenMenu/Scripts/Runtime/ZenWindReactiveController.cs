using UnityEngine;

[DisallowMultipleComponent]
public class ZenWindReactiveController : MonoBehaviour
{
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private WindZone[] _windZones = System.Array.Empty<WindZone>();
    [SerializeField] private float _baseWindMain = 0.18f;
    [SerializeField] private float _peakWindMain = 0.82f;
    [SerializeField] private float _baseTurbulence = 0.08f;
    [SerializeField] private float _peakTurbulence = 0.44f;
    [SerializeField] private float _basePulseMagnitude = 0.05f;
    [SerializeField] private float _peakPulseMagnitude = 0.18f;
    [SerializeField] private float _responseSpeed = 2.5f;

    private float _smoothedLoudness;

    private void Awake()
    {
        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }

        if (_windZones == null || _windZones.Length == 0)
        {
            _windZones = FindObjectsByType<WindZone>(FindObjectsSortMode.None);
        }
    }

    private void Update()
    {
        float target = _signalSource != null ? _signalSource.Loudness01 : 0f;
        _smoothedLoudness = Mathf.MoveTowards(_smoothedLoudness, target, _responseSpeed * Time.deltaTime);
        for (int i = 0; i < _windZones.Length; i++)
        {
            WindZone windZone = _windZones[i];
            if (windZone == null)
            {
                continue;
            }

            windZone.windMain = Mathf.Lerp(_baseWindMain, _peakWindMain, _smoothedLoudness);
            windZone.windTurbulence = Mathf.Lerp(_baseTurbulence, _peakTurbulence, _smoothedLoudness);
            windZone.windPulseMagnitude = Mathf.Lerp(_basePulseMagnitude, _peakPulseMagnitude, _smoothedLoudness);
        }
    }
}
