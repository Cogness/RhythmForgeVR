using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.UI.Rendering
{
    internal struct ShapeRenderData
    {
        public Vector3[] points;
        public float width;
        public float height;
    }

    internal sealed class ShapeLineRenderer
    {
        private readonly LineRenderer _lineRenderer;

        public ShapeLineRenderer(LineRenderer lineRenderer)
        {
            _lineRenderer = lineRenderer;
        }

        public ShapeRenderData Render(List<Vector2> normalizedPoints, float renderScale)
        {
            if (normalizedPoints == null || normalizedPoints.Count == 0)
            {
                _lineRenderer.positionCount = 0;
                return new ShapeRenderData
                {
                    points = new Vector3[0],
                    width = 0f,
                    height = 0f
                };
            }

            var renderedPoints = new Vector3[normalizedPoints.Count];
            _lineRenderer.positionCount = normalizedPoints.Count;

            Vector2 center = Vector2.zero;
            foreach (var point in normalizedPoints)
                center += point;
            center /= normalizedPoints.Count;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 point = normalizedPoints[i] - center;
                var position = new Vector3(point.x * renderScale, point.y * renderScale, 0f);
                renderedPoints[i] = position;
                _lineRenderer.SetPosition(i, position);

                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            return new ShapeRenderData
            {
                points = renderedPoints,
                width = maxX - minX,
                height = maxY - minY
            };
        }
    }
}
