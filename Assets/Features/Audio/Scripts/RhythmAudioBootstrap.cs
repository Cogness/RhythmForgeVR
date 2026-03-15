using UnityEngine;

public static class RhythmAudioBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeAudioRig()
    {
        RhythmSoundEngine existingEngine = Object.FindObjectOfType<RhythmSoundEngine>();
        if (existingEngine != null)
        {
            EnsureKeyboardDriver(existingEngine.gameObject);
            return;
        }

        var runtimeObject = new GameObject("RhythmAudioRuntime");
        Object.DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<AudioSource>();
        runtimeObject.AddComponent<RhythmSoundEngine>();
        EnsureKeyboardDriver(runtimeObject);
        Debug.Log("RhythmAudioBootstrap: created RhythmAudioRuntime.");
    }

    private static void EnsureKeyboardDriver(GameObject target)
    {
        if (target.GetComponent<KeyboardSoundEngineDriver>() == null)
        {
            target.AddComponent<KeyboardSoundEngineDriver>();
        }
    }
}
