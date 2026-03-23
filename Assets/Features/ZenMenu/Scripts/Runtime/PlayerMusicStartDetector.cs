using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMusicStartDetector : MonoBehaviour
{
    [SerializeField] private MusicReactiveSignalSource _signalSource;

    private bool _hasStarted;

    public bool HasStarted => _hasStarted;
    public event Action PlayerMusicStarted;

    private void Awake()
    {
        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }
    }

    private void OnEnable()
    {
        LineDrawing.AnyLineStarted += HandleLineStarted;
        if (_signalSource != null)
        {
            _signalSource.PlayerMusicStarted += HandleMusicSignalStarted;
        }
    }

    private void OnDisable()
    {
        LineDrawing.AnyLineStarted -= HandleLineStarted;
        if (_signalSource != null)
        {
            _signalSource.PlayerMusicStarted -= HandleMusicSignalStarted;
        }
    }

    private void HandleLineStarted(Vector3 _)
    {
        RaiseStarted();
    }

    private void HandleMusicSignalStarted()
    {
        RaiseStarted();
    }

    private void RaiseStarted()
    {
        if (_hasStarted)
        {
            return;
        }

        _hasStarted = true;
        PlayerMusicStarted?.Invoke();
    }
}
