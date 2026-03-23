using UnityEngine;

[DisallowMultipleComponent]
public class ZenWaterRippleRing : MonoBehaviour
{
    [SerializeField] private int _segments = 40;
    [SerializeField] private float _lifetime = 1.4f;
    [SerializeField] private float _minRadius = 0.08f;
    [SerializeField] private float _maxRadius = 1.9f;
    [SerializeField] private float _lineWidth = 0.035f;

    private LineRenderer _lineRenderer;
    private float _age;
    private Color _baseColor;
    private float _strength;
    private bool _isPlaying;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = _segments + 1;
        _lineRenderer.loop = false;
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.widthMultiplier = _lineWidth;
        gameObject.SetActive(false);
    }

    public void Play(Color color, float strength)
    {
        _age = 0f;
        _isPlaying = true;
        _baseColor = color;
        _strength = strength;
        _lineRenderer.widthMultiplier = Mathf.Lerp(_lineWidth * 0.7f, _lineWidth * 1.5f, strength);
        gameObject.SetActive(true);
        UpdateVisual(0f);
    }

    private void Update()
    {
        if (!_isPlaying)
        {
            return;
        }

        _age += Time.deltaTime;
        float normalized = Mathf.Clamp01(_age / _lifetime);
        UpdateVisual(normalized);
        if (normalized >= 1f)
        {
            _isPlaying = false;
            gameObject.SetActive(false);
        }
    }

    private void UpdateVisual(float normalizedAge)
    {
        float radius = Mathf.Lerp(_minRadius, _maxRadius * _strength, normalizedAge);
        float alpha = 1f - normalizedAge;
        Color color = new(_baseColor.r, _baseColor.g, _baseColor.b, alpha);
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;

        for (int i = 0; i <= _segments; i++)
        {
            float angle = (i / (float)_segments) * Mathf.PI * 2f;
            Vector3 point = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            _lineRenderer.SetPosition(i, point);
        }
    }
}
