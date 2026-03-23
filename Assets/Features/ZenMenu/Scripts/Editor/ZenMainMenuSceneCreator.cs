using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ZenMainMenuSceneCreator
{
    private const string SourceScenePath = "Assets/Scenes/MXInkSample.unity";
    private const string TargetScenePath = "Assets/Scenes/ZenMainMenu.unity";
    private const string FeatureRoot = "Assets/Features/ZenMenu";
    private const string MaterialFolder = FeatureRoot + "/Materials";

    [MenuItem("RhythmForge/Create Zen Main Menu Scene")]
    public static void CreateScene()
    {
        EnsureFolders();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            Debug.LogError($"ZenMainMenuSceneCreator: source scene not found at {SourceScenePath}.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) == null)
        {
            AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath);
            AssetDatabase.Refresh();
        }

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        CleanupScene(scene);
        BuildEnvironment(scene);
        WireSystems();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"ZenMainMenuSceneCreator: scene created at {TargetScenePath}.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(FeatureRoot))
        {
            AssetDatabase.CreateFolder("Assets/Features", "ZenMenu");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder(FeatureRoot, "Materials");
        }
    }

    private static void CleanupScene(Scene scene)
    {
        Camera mainCamera = Object.FindFirstObjectByType<Camera>();
        LineDrawing drawing = Object.FindFirstObjectByType<LineDrawing>();
        StylusHandler stylus = Object.FindFirstObjectByType<StylusHandler>();
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);

        HashSet<GameObject> keepRoots = new();
        if (mainCamera != null)
        {
            keepRoots.Add(mainCamera.transform.root.gameObject);
        }

        if (drawing != null)
        {
            keepRoots.Add(drawing.transform.root.gameObject);
        }

        if (stylus != null)
        {
            keepRoots.Add(stylus.transform.root.gameObject);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                keepRoots.Add(lights[i].transform.root.gameObject);
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!keepRoots.Contains(root))
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void BuildEnvironment(Scene scene)
    {
        Material sand = GetOrCreateLitMaterial("ZenSand", new Color(0.82f, 0.76f, 0.61f), 0.25f);
        Material water = GetOrCreateTransparentMaterial("ZenWater", new Color(0.34f, 0.64f, 0.73f, 0.78f));
        Material mountain = GetOrCreateLitMaterial("ZenMountain", new Color(0.52f, 0.58f, 0.56f), 0.6f);
        Material foliage = GetOrCreateLitMaterial("ZenFoliage", new Color(0.32f, 0.47f, 0.31f), 0.35f);
        Material bark = GetOrCreateLitMaterial("ZenBark", new Color(0.31f, 0.22f, 0.16f), 0.2f);
        Material ripple = GetOrCreateTransparentMaterial("ZenRipple", new Color(0.83f, 0.95f, 1f, 0.85f));
        Material stone = GetOrCreateLitMaterial("ZenStone", new Color(0.6f, 0.62f, 0.6f), 0.55f);

        GameObject environmentRoot = new("ZenEnvironment");
        SceneManager.MoveGameObjectToScene(environmentRoot, scene);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(environmentRoot.transform);
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = sand;

        GameObject waterRoot = new("WaterRoot");
        waterRoot.transform.SetParent(environmentRoot.transform);
        waterRoot.transform.position = new Vector3(0f, 0.02f, 1.2f);

        GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        waterPlane.name = "WaterPlane";
        waterPlane.transform.SetParent(waterRoot.transform, false);
        waterPlane.transform.localScale = new Vector3(8f, 0.02f, 8f);
        waterPlane.GetComponent<MeshRenderer>().sharedMaterial = water;
        Object.DestroyImmediate(waterPlane.GetComponent<CapsuleCollider>());

        GameObject mountainRoot = new("Mountains");
        mountainRoot.transform.SetParent(environmentRoot.transform);
        for (int i = 0; i < 12; i++)
        {
            float angle = i / 12f * Mathf.PI * 2f;
            float distance = 28f + (i % 3);
            Vector3 position = new(Mathf.Cos(angle) * distance, 2.2f + (i % 2), Mathf.Sin(angle) * distance);
            GameObject peak = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            peak.name = $"Mountain_{i + 1}";
            peak.transform.SetParent(mountainRoot.transform);
            peak.transform.position = position;
            peak.transform.localScale = new Vector3(8f + (i % 4), 8f + ((i + 1) % 5), 8f + (i % 3));
            peak.GetComponent<MeshRenderer>().sharedMaterial = mountain;
        }

        GameObject treeRoot = new("Trees");
        treeRoot.transform.SetParent(environmentRoot.transform);
        Random.InitState(1427);
        for (int i = 0; i < 24; i++)
        {
            float angle = i / 24f * Mathf.PI * 2f;
            float radius = Random.Range(11f, 19f);
            Vector3 treePosition = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            GameObject tree = new($"Tree_{i + 1}");
            tree.transform.SetParent(treeRoot.transform);
            tree.transform.position = treePosition;
            tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform);
            trunk.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            trunk.transform.localScale = new Vector3(0.24f, 1.1f, 0.24f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = bark;

            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(tree.transform);
            canopy.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            canopy.transform.localScale = new Vector3(1.8f, 2.2f, 1.8f);
            canopy.GetComponent<MeshRenderer>().sharedMaterial = foliage;
        }

        GameObject stonePathRoot = new("StonePath");
        stonePathRoot.transform.SetParent(environmentRoot.transform);
        for (int i = 0; i < 7; i++)
        {
            GameObject stoneStep = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stoneStep.name = $"Stone_{i + 1}";
            stoneStep.transform.SetParent(stonePathRoot.transform);
            stoneStep.transform.position = new Vector3(0f, 0.035f, -1.8f + (i * 0.7f));
            stoneStep.transform.localScale = new Vector3(0.85f, 0.05f, 0.7f);
            stoneStep.transform.rotation = Quaternion.Euler(0f, i * 12f, 0f);
            stoneStep.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }

        GameObject penDock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        penDock.name = "PenDock";
        penDock.transform.SetParent(environmentRoot.transform);
        penDock.transform.position = new Vector3(0.65f, 0.42f, 0.45f);
        penDock.transform.localScale = new Vector3(0.22f, 0.42f, 0.22f);
        penDock.GetComponent<MeshRenderer>().sharedMaterial = stone;

        GameObject spawnPoint = new("PlayerSpawnPoint");
        spawnPoint.transform.SetParent(environmentRoot.transform);
        spawnPoint.transform.position = new Vector3(0f, 0f, -1.1f);
        spawnPoint.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        if (RenderSettings.sun == null)
        {
            GameObject lightObject = new("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.transform.rotation = Quaternion.Euler(24f, -32f, 0f);
            RenderSettings.sun = light;
            SceneManager.MoveGameObjectToScene(lightObject, scene);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.61f, 0.72f, 0.81f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.62f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.26f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.68f, 0.79f, 0.84f);
        RenderSettings.fogDensity = 0.0032f;

        GameObject systemsRoot = new("ZenMenuSystems");
        SceneManager.MoveGameObjectToScene(systemsRoot, scene);

        GameObject signalObject = new("MusicReactiveSignal");
        signalObject.transform.SetParent(systemsRoot.transform);
        signalObject.AddComponent<MusicReactiveSignalSource>();
        signalObject.AddComponent<ManualMusicSignalTester>();
        signalObject.AddComponent<RhythmSoundEngineSignalAdapter>();
        signalObject.AddComponent<PlayerMusicStartDetector>();

        GameObject ambientObject = new("AmbientMusic");
        ambientObject.transform.SetParent(systemsRoot.transform);
        ambientObject.AddComponent<AudioSource>();
        ambientObject.AddComponent<ZenAmbientMusicController>();

        GameObject windObject = new("ReactiveWind");
        windObject.transform.SetParent(systemsRoot.transform);
        WindZone windZone = windObject.AddComponent<WindZone>();
        windZone.mode = WindZoneMode.Directional;
        windZone.windMain = 0.18f;
        windZone.windTurbulence = 0.08f;
        windZone.windPulseMagnitude = 0.05f;
        windZone.windPulseFrequency = 0.2f;
        windObject.AddComponent<ZenWindReactiveController>();

        GameObject rippleObject = new("WaterRipples");
        rippleObject.transform.SetParent(systemsRoot.transform);
        ZenWaterRippleController rippleController = rippleObject.AddComponent<ZenWaterRippleController>();
        SetSerializedVector2(rippleController, "_waterExtents", new Vector2(5.8f, 5.8f));
        SetSerializedObject(rippleController, "_waterSurface", waterRoot.transform);
        SetSerializedObject(rippleController, "_rippleMaterial", ripple);

        GameObject atmosphereObject = new("ReactiveAtmosphere");
        atmosphereObject.transform.SetParent(systemsRoot.transform);
        ParticleSystem motes = atmosphereObject.AddComponent<ParticleSystem>();
        ConfigureMotes(motes);
        atmosphereObject.AddComponent<ZenReactiveAtmosphereController>();

        GameObject menuControllerObject = new("ZenMainMenuController");
        menuControllerObject.transform.SetParent(systemsRoot.transform);
        menuControllerObject.AddComponent<ZenMainMenuController>();
    }

    private static void WireSystems()
    {
        MusicReactiveSignalSource signalSource = Object.FindFirstObjectByType<MusicReactiveSignalSource>();
        PlayerMusicStartDetector startDetector = Object.FindFirstObjectByType<PlayerMusicStartDetector>();
        ZenAmbientMusicController ambientController = Object.FindFirstObjectByType<ZenAmbientMusicController>();
        ZenWindReactiveController windController = Object.FindFirstObjectByType<ZenWindReactiveController>();
        ZenReactiveAtmosphereController atmosphereController = Object.FindFirstObjectByType<ZenReactiveAtmosphereController>();
        ZenMainMenuController menuController = Object.FindFirstObjectByType<ZenMainMenuController>();
        ZenWaterRippleController rippleController = Object.FindFirstObjectByType<ZenWaterRippleController>();
        RhythmSoundEngineSignalAdapter engineAdapter = Object.FindFirstObjectByType<RhythmSoundEngineSignalAdapter>();
        ManualMusicSignalTester tester = Object.FindFirstObjectByType<ManualMusicSignalTester>();
        LineDrawing drawing = Object.FindFirstObjectByType<LineDrawing>();
        StylusHandler stylus = Object.FindFirstObjectByType<StylusHandler>();
        Camera mainCamera = Object.FindFirstObjectByType<Camera>();
        Transform waterRoot = GameObject.Find("WaterRoot")?.transform;
        Transform penDock = GameObject.Find("PenDock")?.transform;
        Transform spawnPoint = GameObject.Find("PlayerSpawnPoint")?.transform;

        if (drawing != null && stylus != null)
        {
            SetSerializedObject(drawing, "_stylusHandler", stylus);
        }

        if (ambientController != null)
        {
            SetSerializedObject(ambientController, "_startDetector", startDetector);
        }

        if (windController != null)
        {
            SetSerializedObject(windController, "_signalSource", signalSource);
        }

        if (atmosphereController != null)
        {
            SetSerializedObject(atmosphereController, "_signalSource", signalSource);
            SetSerializedObject(atmosphereController, "_directionalLight", RenderSettings.sun);
            SetSerializedObject(atmosphereController, "_motes", atmosphereController.GetComponent<ParticleSystem>());
        }

        if (rippleController != null)
        {
            SetSerializedObject(rippleController, "_signalSource", signalSource);
            SetSerializedObject(rippleController, "_waterSurface", waterRoot);
        }

        if (engineAdapter != null)
        {
            SetSerializedObject(engineAdapter, "_signalSource", signalSource);
            SetSerializedObject(engineAdapter, "_engine", Object.FindFirstObjectByType<RhythmSoundEngine>());
        }

        if (tester != null)
        {
            SetSerializedObject(tester, "_signalSource", signalSource);
        }

        if (startDetector != null)
        {
            SetSerializedObject(startDetector, "_signalSource", signalSource);
        }

        if (menuController != null)
        {
            SetSerializedObject(menuController, "_headCamera", mainCamera);
            SetSerializedObject(menuController, "_playerRigRoot", mainCamera != null ? mainCamera.transform.root : null);
            SetSerializedObject(menuController, "_spawnPoint", spawnPoint);
            SetSerializedObject(menuController, "_penDockAnchor", penDock);
            SetSerializedObject(menuController, "_mxInkRoot", stylus != null ? stylus.transform : null);
        }
    }

    private static void ConfigureMotes(ParticleSystem particleSystem)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 10f;
        main.startSpeed = 0.08f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 8f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 5f, 18f);
        particleSystem.transform.position = new Vector3(0f, 1.8f, 1.2f);
    }

    private static Material GetOrCreateLitMaterial(string name, Color color, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material GetOrCreateTransparentMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetSerializedObject(Object target, string fieldName, Object value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedVector2(Object target, string fieldName, Vector2 value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            return;
        }

        property.vector2Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
