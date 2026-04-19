using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.UI
{
    public class SpatialZoneVisualizer : MonoBehaviour
    {
        private static readonly Color BaseSphereColor = new Color(0.48f, 0.82f, 1f, 0.04f);
        private static readonly Color ActiveSphereColor = new Color(0.48f, 0.92f, 1f, 0.10f);
        private static readonly Color BaseRingColor = new Color(0.60f, 0.90f, 1f, 0.12f);
        private static readonly Color ActiveRingColor = new Color(0.76f, 0.96f, 1f, 0.25f);

        private SpatialZone _zone;
        private MeshRenderer _sphereRenderer;
        private LineRenderer _ringRenderer;
        private float _pulse;

        public void Initialize(SpatialZone zone)
        {
            _zone = zone;
            name = zone != null ? zone.id : "SpatialZone";

            CreateSphere();
            CreateRing();
            RefreshPose();
            UpdateVisuals();
        }

        public void RefreshPose()
        {
            if (_zone == null)
                return;

            transform.localPosition = _zone.center;
            transform.localRotation = Quaternion.identity;
        }

        public void Pulse()
        {
            _pulse = 1f;
            UpdateVisuals();
        }

        private void Update()
        {
            if (_pulse <= 0f)
                return;

            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime / 0.25f);
            UpdateVisuals();
        }

        private void CreateSphere()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SoftSphere";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * Mathf.Max(0.01f, _zone.radius * 2f);

            var collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }

            _sphereRenderer = sphere.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            _sphereRenderer.sharedMaterial = new Material(shader);
            _sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sphereRenderer.receiveShadows = false;
        }

        private void CreateRing()
        {
            var ringObject = new GameObject("EquatorRing");
            ringObject.transform.SetParent(transform, false);
            _ringRenderer = ringObject.AddComponent<LineRenderer>();
            _ringRenderer.loop = true;
            _ringRenderer.useWorldSpace = false;
            _ringRenderer.alignment = LineAlignment.View;
            _ringRenderer.positionCount = 48;
            _ringRenderer.startWidth = 0.01f;
            _ringRenderer.endWidth = 0.01f;
            _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringRenderer.receiveShadows = false;
            _ringRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            float radius = Mathf.Max(0.01f, _zone.radius);
            for (int i = 0; i < _ringRenderer.positionCount; i++)
            {
                float t = i / (float)_ringRenderer.positionCount;
                float angle = t * Mathf.PI * 2f;
                _ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private void UpdateVisuals()
        {
            if (_sphereRenderer != null && _sphereRenderer.sharedMaterial != null)
                _sphereRenderer.sharedMaterial.color = Color.Lerp(BaseSphereColor, ActiveSphereColor, _pulse);

            if (_ringRenderer != null)
            {
                Color color = Color.Lerp(BaseRingColor, ActiveRingColor, _pulse);
                _ringRenderer.startColor = color;
                _ringRenderer.endColor = color;
            }
        }
    }
}
