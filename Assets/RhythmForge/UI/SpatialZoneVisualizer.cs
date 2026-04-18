using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.UI
{
    /// <summary>
    /// Renders a faint translucent wireframe sphere for each spatial zone.
    /// Parented to a container transform set up by the bootstrapper.
    /// </summary>
    public class SpatialZoneVisualizer : MonoBehaviour
    {
        private readonly List<GameObject> _spheres = new List<GameObject>();

        public void Initialize(IReadOnlyList<SpatialZone> zones)
        {
            foreach (var zone in zones)
                CreateSphere(zone);
        }

        private void CreateSphere(SpatialZone zone)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Zone_{zone.id}";
            go.transform.SetParent(transform, false);
            go.transform.position = zone.center;
            go.transform.localScale = Vector3.one * zone.radius * 2f;

            // Remove collider — purely visual
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Transparent unlit material
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(zone.color.r, zone.color.g, zone.color.b, 0.06f);
            go.GetComponent<Renderer>().material = mat;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.GetComponent<Renderer>().receiveShadows = false;

            _spheres.Add(go);
        }
    }
}
