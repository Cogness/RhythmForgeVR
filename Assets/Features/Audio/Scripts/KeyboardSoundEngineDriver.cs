using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
public class KeyboardSoundEngineDriver : MonoBehaviour
{
    [SerializeField] private RhythmSoundEngine _engine;

    private void Awake()
    {
        if (_engine == null)
        {
            _engine = GetComponent<RhythmSoundEngine>();
        }
    }

    private void Update()
    {
        if (_engine == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        HandleNumberKey(keyboard.digit1Key, shiftHeld, 0);
        HandleNumberKey(keyboard.digit2Key, shiftHeld, 1);
        HandleNumberKey(keyboard.digit3Key, shiftHeld, 2);
        HandleNumberKey(keyboard.digit4Key, shiftHeld, 3);
        HandleNumberKey(keyboard.digit5Key, shiftHeld, 4);
        HandleNumberKey(keyboard.digit6Key, shiftHeld, 5);
        HandleNumberKey(keyboard.digit7Key, shiftHeld, 6);
        HandleNumberKey(keyboard.digit8Key, shiftHeld, 7);
        HandleNumberKey(keyboard.digit9Key, shiftHeld, 8);
        HandleNumberKey(keyboard.numpad1Key, shiftHeld, 0);
        HandleNumberKey(keyboard.numpad2Key, shiftHeld, 1);
        HandleNumberKey(keyboard.numpad3Key, shiftHeld, 2);
        HandleNumberKey(keyboard.numpad4Key, shiftHeld, 3);
        HandleNumberKey(keyboard.numpad5Key, shiftHeld, 4);
        HandleNumberKey(keyboard.numpad6Key, shiftHeld, 5);
        HandleNumberKey(keyboard.numpad7Key, shiftHeld, 6);
        HandleNumberKey(keyboard.numpad8Key, shiftHeld, 7);
        HandleNumberKey(keyboard.numpad9Key, shiftHeld, 8);

        if (keyboard.commaKey.wasPressedThisFrame)
        {
            _engine.PreviousPreset();
        }

        if (keyboard.periodKey.wasPressedThisFrame)
        {
            _engine.NextPreset();
        }

        if (keyboard.minusKey.wasPressedThisFrame)
        {
            _engine.AdjustBpm(-_engine.GetBpmStep());
        }

        if (keyboard.equalsKey.wasPressedThisFrame)
        {
            _engine.AdjustBpm(_engine.GetBpmStep());
        }

        if (keyboard.leftBracketKey.wasPressedThisFrame)
        {
            _engine.AdjustPitchSemitones(-_engine.GetPitchStep());
        }

        if (keyboard.rightBracketKey.wasPressedThisFrame)
        {
            _engine.AdjustPitchSemitones(_engine.GetPitchStep());
        }

        if (keyboard.semicolonKey.wasPressedThisFrame)
        {
            _engine.AdjustReverb(-_engine.GetReverbStep());
        }

        if (keyboard.quoteKey.wasPressedThisFrame)
        {
            _engine.AdjustReverb(_engine.GetReverbStep());
        }

        if (keyboard.backspaceKey.wasPressedThisFrame)
        {
            _engine.StopAllLoops();
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            _engine.ResetEngineSettings();
        }
    }

    private void HandleNumberKey(KeyControl key, bool shiftHeld, int soundIndex)
    {
        if (!key.wasPressedThisFrame)
        {
            return;
        }

        if (shiftHeld)
        {
            _engine.ToggleLoop(soundIndex);
        }
        else
        {
            _engine.TriggerSound(soundIndex);
        }
    }
}
