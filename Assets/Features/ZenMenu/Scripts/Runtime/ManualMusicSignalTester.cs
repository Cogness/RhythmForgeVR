using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ManualMusicSignalTester : MonoBehaviour
{
    [SerializeField] private MusicReactiveSignalSource _signalSource;
    [SerializeField] private float _pulseStrength = 0.5f;
    [SerializeField] private float _beatStrength = 0.85f;

    private void Awake()
    {
        if (_signalSource == null)
        {
            _signalSource = FindFirstObjectByType<MusicReactiveSignalSource>();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || _signalSource == null)
        {
            return;
        }

        if (keyboard.mKey.wasPressedThisFrame)
        {
            _signalSource.InjectPulse(_pulseStrength, false);
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            _signalSource.InjectPulse(_beatStrength, true);
        }
    }
}
