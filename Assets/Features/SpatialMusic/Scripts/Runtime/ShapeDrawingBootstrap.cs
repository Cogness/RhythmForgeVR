using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShapeDrawingBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "ZenMainMenu" && activeScene.name != "MXInkSample")
        {
            return;
        }

        if (Object.FindFirstObjectByType<ShapeDrawingManager>() != null)
        {
            return;
        }

        if (Object.FindFirstObjectByType<Camera>() == null)
        {
            return;
        }

        DisableLegacyLineDrawing();
        EnsureMusicSignalSource();

        GameObject root = new("SpatialMusicRuntime");
        root.AddComponent<MXInkDrawingInputSource>();
        root.AddComponent<XRControllerDrawingInputSource>();
        root.AddComponent<DesktopDrawingInputSource>();
        root.AddComponent<MusicalShapeFactory>();
        root.AddComponent<ShapeDrawingManager>();
    }

    private static void DisableLegacyLineDrawing()
    {
        LineDrawing lineDrawing = Object.FindFirstObjectByType<LineDrawing>();
        if (lineDrawing != null)
        {
            lineDrawing.enabled = false;
        }
    }

    private static void EnsureMusicSignalSource()
    {
        if (Object.FindFirstObjectByType<MusicReactiveSignalSource>() != null)
        {
            return;
        }

        GameObject signalRoot = new("MusicReactiveSignal");
        signalRoot.AddComponent<MusicReactiveSignalSource>();
    }
}

