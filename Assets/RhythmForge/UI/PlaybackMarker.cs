using UnityEngine;

namespace RhythmForge.UI
{
    /// <summary>
    /// A small sphere that travels along a pattern's line to indicate playback position.
    /// </summary>
    public class PlaybackMarker : MonoBehaviour
    {
        [SerializeField] private float _markerSize = 0.008f;

        private LineRenderer _targetLine;
        private MeshRenderer _renderer;
        private float _phase = -1f;

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
            _targetLine = line;
            if (_renderer != null)
                _renderer.material.color = color;
        }

        public void UpdatePhase(float phase)
        {
            _phase = phase;

            if (_phase < 0f || _targetLine == null || _targetLine.positionCount < 2)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // Interpolate position along the line
            float totalPoints = _targetLine.positionCount - 1;
            float exactIndex = _phase * totalPoints;
            int indexA = Mathf.FloorToInt(exactIndex);
            int indexB = Mathf.Min(indexA + 1, _targetLine.positionCount - 1);
            float local = exactIndex - indexA;

            Vector3 posA = _targetLine.GetPosition(indexA);
            Vector3 posB = _targetLine.GetPosition(indexB);
            transform.localPosition = Vector3.Lerp(posA, posB, local);
        }
    }
}
