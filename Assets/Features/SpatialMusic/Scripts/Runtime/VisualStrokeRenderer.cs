using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VisualStrokeRenderer : MonoBehaviour
{
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private float _baseWidth = 0.012f;

    private void Awake()
    {
        if (_lineRenderer == null)
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        _lineRenderer.loop = false;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.alignment = LineAlignment.View;
        _lineRenderer.positionCount = 0;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
    }

    public void Configure(Material material, Gradient gradient)
    {
        _lineRenderer.material = material;
        _lineRenderer.colorGradient = gradient;
        _lineRenderer.widthMultiplier = _baseWidth;
    }

    public void SetPoints(IReadOnlyList<StrokePoint> points)
    {
        _lineRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            _lineRenderer.SetPosition(i, points[i].Position);
        }
    }

    public void Clear()
    {
        _lineRenderer.positionCount = 0;
    }
}
