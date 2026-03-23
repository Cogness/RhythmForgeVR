using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MusicalShapeFactory : MonoBehaviour
{
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private int _maxActiveShapes = 8;

    private readonly List<MusicalShapeController> _activeShapes = new();
    private Material _lineMaterial;
    private Material _coreMaterial;
    private Transform _shapeRoot;

    public int ActiveShapeCount => _activeShapes.Count;

    private void Awake()
    {
        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }

        _shapeRoot = new GameObject("ActiveMusicalShapes").transform;
        _shapeRoot.SetParent(transform, false);
        _lineMaterial = CreateLineMaterial();
        _coreMaterial = CreateCoreMaterial();
    }

    public MusicalShapeController CreateShape(StrokeData stroke, ShapeDescriptor descriptor)
    {
        if (_activeShapes.Count >= _maxActiveShapes)
        {
            RemoveOldestShape();
        }

        MusicalLoopDefinition loopDefinition = MusicalLoopDefinition.FromDescriptor(descriptor);
        GameObject shapeObject = new();
        shapeObject.transform.SetParent(_shapeRoot, false);
        MusicalShapeController controller = shapeObject.AddComponent<MusicalShapeController>();
        controller.Initialize(stroke, descriptor, loopDefinition, _lineMaterial, _coreMaterial, _signalSource);
        _activeShapes.Add(controller);
        return controller;
    }

    public void RemoveLastShape()
    {
        if (_activeShapes.Count == 0)
        {
            return;
        }

        MusicalShapeController controller = _activeShapes[_activeShapes.Count - 1];
        _activeShapes.RemoveAt(_activeShapes.Count - 1);
        if (controller != null)
        {
            Destroy(controller.gameObject);
        }
    }

    public void RemoveAllShapes()
    {
        for (int i = 0; i < _activeShapes.Count; i++)
        {
            if (_activeShapes[i] != null)
            {
                Destroy(_activeShapes[i].gameObject);
            }
        }

        _activeShapes.Clear();
    }

    private void RemoveOldestShape()
    {
        MusicalShapeController oldest = _activeShapes[0];
        _activeShapes.RemoveAt(0);
        if (oldest != null)
        {
            Destroy(oldest.gameObject);
        }
    }

    private static Material CreateLineMaterial()
    {
        Material material = new(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", Color.white);
        return material;
    }

    private static Material CreateCoreMaterial()
    {
        Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetFloat("_Smoothness", 0.65f);
        return material;
    }
}
