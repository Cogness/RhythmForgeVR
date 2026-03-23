using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class ZenWaterRippleController : MonoBehaviour
{
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private Transform _waterSurface;
    [SerializeField] private Vector2 _waterExtents = new(7.5f, 7.5f);
    [SerializeField] private Material _rippleMaterial;
    [SerializeField] private Color _rippleColor = new(0.72f, 0.9f, 1f, 0.85f);
    [SerializeField] private int _poolSize = 10;
    [SerializeField] private float _waterHeight = 0.02f;

    private ZenWaterRippleRing[] _pool;
    private int _cursor;

    private void Awake()
    {
        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }

        BuildPool();
    }

    private void OnEnable()
    {
        if (_signalSource != null)
        {
            _signalSource.BeatDetected += HandleBeatDetected;
        }
    }

    private void OnDisable()
    {
        if (_signalSource != null)
        {
            _signalSource.BeatDetected -= HandleBeatDetected;
        }
    }

    private void BuildPool()
    {
        if (_waterSurface == null)
        {
            _waterSurface = transform;
        }

        _pool = new ZenWaterRippleRing[_poolSize];
        for (int i = 0; i < _pool.Length; i++)
        {
            GameObject rippleObject = new($"Ripple_{i + 1}");
            rippleObject.transform.SetParent(_waterSurface, false);
            LineRenderer lineRenderer = rippleObject.AddComponent<LineRenderer>();
            lineRenderer.material = _rippleMaterial;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            ZenWaterRippleRing ripple = rippleObject.AddComponent<ZenWaterRippleRing>();
            _pool[i] = ripple;
        }
    }

    private void HandleBeatDetected(float intensity)
    {
        if (_pool == null || _pool.Length == 0 || _waterSurface == null)
        {
            return;
        }

        ZenWaterRippleRing ripple = _pool[_cursor];
        _cursor = (_cursor + 1) % _pool.Length;

        Vector3 localPosition = new(
            Random.Range(-_waterExtents.x, _waterExtents.x),
            _waterHeight,
            Random.Range(-_waterExtents.y, _waterExtents.y));

        ripple.transform.localPosition = localPosition;
        ripple.Play(_rippleColor, Mathf.Lerp(0.65f, 1.2f, intensity));
    }
}
