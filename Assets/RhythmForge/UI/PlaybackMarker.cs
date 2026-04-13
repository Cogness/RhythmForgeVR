using UnityEngine;

namespace RhythmForge.UI
{
    /// <summary>
    /// A small sphere that travels along a pattern's line to indicate playback position.
    /// Sampling uses arc length so motion stays even on irregular geometry.
    /// </summary>
    public class PlaybackMarker : MonoBehaviour
    {
        [SerializeField] private float _markerSize = 0.008f;

        private MeshRenderer _renderer;
        private Vector3[] _points = new Vector3[0];
        private float[] _cumulativeLengths = new float[0];
        private float _totalLength;
        private bool _loop;
        private Color _baseColor = Color.white;

        private void Awake()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * _markerSize;

            // Remove the collider from the primitive
            var collider = sphere.GetComponent<Collider>();
            if (collider) Destroy(collider);

            _renderer = sphere.GetComponent<MeshRenderer>();
            _renderer.material = new Material(Shader.Find("Sprites/Default"));
            _renderer.material.color = Color.white;
            gameObject.SetActive(false);
        }

        public void SetTarget(LineRenderer line, Color color)
        {
            _baseColor = color;
            _loop = line != null && line.loop;

            if (line == null || line.positionCount < 2)
            {
                _points = new Vector3[0];
                _cumulativeLengths = new float[0];
                _totalLength = 0f;
                gameObject.SetActive(false);
                return;
            }

            _points = new Vector3[line.positionCount];
            for (int i = 0; i < line.positionCount; i++)
                _points[i] = line.GetPosition(i);

            BuildArcLengthTable();
            SetColor(color, 0f);
        }

        public void SetTint(Color color)
        {
            _baseColor = color;
        }

        public void ApplyState(float phase, float intensity, float scale, float normalOffset = 0f)
        {
            if (phase < 0f || _points.Length < 2 || _totalLength <= 0.0001f || intensity <= 0.001f)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            float normalizedPhase = _loop ? Mathf.Repeat(phase, 1f) : Mathf.Clamp01(phase);
            float distance = normalizedPhase * _totalLength;
            SampleArcLength(distance, out var position, out var tangent);

            Vector3 normal = tangent.sqrMagnitude > 0.0001f
                ? new Vector3(-tangent.y, tangent.x, 0f).normalized
                : Vector3.up;

            transform.localPosition = position + normal * normalOffset;
            transform.localScale = Vector3.one * (_markerSize * Mathf.Lerp(0.75f, 2f, Mathf.Clamp01(scale)));
            SetColor(_baseColor, intensity);
        }

        private void BuildArcLengthTable()
        {
            int segmentCount = _loop ? _points.Length : _points.Length - 1;
            _cumulativeLengths = new float[segmentCount + 1];
            _cumulativeLengths[0] = 0f;

            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 a = _points[i];
                Vector3 b = _points[(i + 1) % _points.Length];
                _cumulativeLengths[i + 1] = _cumulativeLengths[i] + Vector3.Distance(a, b);
            }

            _totalLength = _cumulativeLengths[_cumulativeLengths.Length - 1];
        }

        private void SampleArcLength(float distance, out Vector3 position, out Vector3 tangent)
        {
            if (_points.Length == 0)
            {
                position = Vector3.zero;
                tangent = Vector3.right;
                return;
            }

            if (_points.Length == 1 || _totalLength <= 0.0001f)
            {
                position = _points[0];
                tangent = Vector3.right;
                return;
            }

            int segmentCount = _cumulativeLengths.Length - 1;
            float targetDistance = _loop
                ? Mathf.Repeat(distance, _totalLength)
                : Mathf.Clamp(distance, 0f, _totalLength);

            for (int i = 0; i < segmentCount; i++)
            {
                float startDistance = _cumulativeLengths[i];
                float endDistance = _cumulativeLengths[i + 1];

                if (targetDistance > endDistance && i < segmentCount - 1)
                    continue;

                Vector3 a = _points[i];
                Vector3 b = _points[(i + 1) % _points.Length];
                float segmentLength = Mathf.Max(0.0001f, endDistance - startDistance);
                float t = Mathf.Clamp01((targetDistance - startDistance) / segmentLength);
                position = Vector3.Lerp(a, b, t);
                tangent = (b - a).normalized;
                if (tangent.sqrMagnitude <= 0.0001f)
                    tangent = Vector3.right;
                return;
            }

            position = _points[_points.Length - 1];
            tangent = (_points[_points.Length - 1] - _points[_points.Length - 2]).normalized;
            if (tangent.sqrMagnitude <= 0.0001f)
                tangent = Vector3.right;
        }

        private void SetColor(Color color, float intensity)
        {
            if (_renderer == null)
                return;

            float glow = Mathf.Clamp01(intensity);
            _renderer.material.color = new Color(
                Mathf.Clamp01(color.r + glow * 0.32f),
                Mathf.Clamp01(color.g + glow * 0.32f),
                Mathf.Clamp01(color.b + glow * 0.32f),
                Mathf.Lerp(0.1f, 0.98f, glow));
        }
    }
}
