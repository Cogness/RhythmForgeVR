using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MusicalShapeController : MonoBehaviour
{
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private Renderer _coreRenderer;
    [SerializeField] private ProceduralMusicalShapeVoice _voice;
    [SerializeField] private AudioSource _audioSource;

    private MusicReactiveSignalSource _signalSource;
    private MusicalLoopDefinition _loopDefinition;
    private float _loopTime;
    private int _nextPulseIndex;
    private float _visualPulse;
    private float _baseWidth;
    private Color _baseColor;

    public void Initialize(StrokeData stroke, ShapeDescriptor descriptor, MusicalLoopDefinition loopDefinition, Material lineMaterial, Material coreMaterial, MusicReactiveSignalSource signalSource)
    {
        _signalSource = signalSource;
        _loopDefinition = loopDefinition;
        gameObject.name = $"MusicalShape_{descriptor.Classification}";
        transform.position = descriptor.Center;

        CreateVisuals(stroke, lineMaterial, coreMaterial, descriptor);
        CreateVoice(loopDefinition);
    }

    private void CreateVisuals(StrokeData stroke, Material lineMaterial, Material coreMaterial, ShapeDescriptor descriptor)
    {
        if (_lineRenderer == null)
        {
            GameObject lineObject = new("Visual");
            lineObject.transform.SetParent(transform, false);
            _lineRenderer = lineObject.AddComponent<LineRenderer>();
        }

        _lineRenderer.loop = descriptor.Classification == ShapeClassification.CircleLoop;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.alignment = LineAlignment.View;
        _lineRenderer.material = new Material(lineMaterial);
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.positionCount = stroke.PointCount;
        _baseWidth = Mathf.Lerp(0.014f, 0.024f, descriptor.AveragePressure);
        _lineRenderer.widthMultiplier = _baseWidth;
        _baseColor = _loopDefinition.VisualColor;
        _lineRenderer.startColor = _baseColor;
        _lineRenderer.endColor = _baseColor;

        for (int i = 0; i < stroke.PointCount; i++)
        {
            _lineRenderer.SetPosition(i, stroke.Points[i].Position);
        }

        if (_coreRenderer == null)
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = Vector3.one * 0.12f;
            Destroy(core.GetComponent<Collider>());
            _coreRenderer = core.GetComponent<Renderer>();
        }

        _coreRenderer.material = new Material(coreMaterial);
        _coreRenderer.material.color = _baseColor;
    }

    private void CreateVoice(MusicalLoopDefinition loopDefinition)
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (_voice == null)
        {
            _voice = gameObject.AddComponent<ProceduralMusicalShapeVoice>();
        }

        _voice.Initialize(loopDefinition, _audioSource);
    }

    private void Update()
    {
        if (_loopDefinition == null || _loopDefinition.LoopDurationSeconds <= 0f)
        {
            return;
        }

        float previousTime = _loopTime;
        _loopTime += Time.deltaTime;
        if (_loopTime >= _loopDefinition.LoopDurationSeconds)
        {
            _loopTime -= _loopDefinition.LoopDurationSeconds;
            _nextPulseIndex = 0;
            previousTime = -0.0001f;
        }

        List<MusicalPulse> pulses = _loopDefinition.Pulses;
        while (_nextPulseIndex < pulses.Count && pulses[_nextPulseIndex].TimeSeconds <= _loopTime)
        {
            MusicalPulse pulse = pulses[_nextPulseIndex];
            if (pulse.TimeSeconds >= previousTime)
            {
                _visualPulse = Mathf.Max(_visualPulse, pulse.Amplitude);
                _signalSource?.InjectPulse(Mathf.Clamp01(pulse.Amplitude * _loopDefinition.Volume * 1.8f), true);
            }

            _nextPulseIndex++;
        }

        _visualPulse = Mathf.MoveTowards(_visualPulse, 0f, Time.deltaTime * 2.6f);
        float width = Mathf.Lerp(_baseWidth, _baseWidth * 1.5f, _visualPulse);
        _lineRenderer.widthMultiplier = width;
        Color color = Color.Lerp(_baseColor, Color.white, _visualPulse * 0.6f);
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
        if (_coreRenderer != null)
        {
            _coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.2f, _visualPulse);
            _coreRenderer.material.color = color;
        }
    }

    private void OnDestroy()
    {
        _voice?.Stop();
    }
}
