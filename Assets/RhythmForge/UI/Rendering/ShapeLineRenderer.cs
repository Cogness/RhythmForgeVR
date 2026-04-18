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

        /// <summary>
        /// Phase G: render from a 3D, center-relative point list. Unlike the
        /// 2D overload above this preserves the out-of-plane dimension, so a
        /// helix draws as a helix instead of being flattened. Points are
        /// already center-relative (stored that way on
        /// <see cref="RhythmForge.Core.Data.PatternDefinition.worldPoints"/>),
        /// so we skip the centroid subtraction that the 2D path performs.
        /// </summary>
        public ShapeRenderData Render(List<Vector3> centerRelativePoints, float renderScale)
        {
            if (centerRelativePoints == null || centerRelativePoints.Count == 0)
            {
                _lineRenderer.positionCount = 0;
                return new ShapeRenderData
                {
                    points = new Vector3[0],
                    width = 0f,
                    height = 0f
                };
            }

            var renderedPoints = new Vector3[centerRelativePoints.Count];
            _lineRenderer.positionCount = centerRelativePoints.Count;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < centerRelativePoints.Count; i++)
            {
                var position = centerRelativePoints[i] * renderScale;
                renderedPoints[i] = position;
                _lineRenderer.SetPosition(i, position);

                if (position.x < minX) minX = position.x;
                if (position.x > maxX) maxX = position.x;
                if (position.y < minY) minY = position.y;
                if (position.y > maxY) maxY = position.y;
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
