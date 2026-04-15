using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using RhythmForge;
using RhythmForge.Audio;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;
using RhythmForge.Sequencer;
using RhythmForge.UI;
using RhythmForge.UI.Panels;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Zero-configuration runtime bootstrapper.
    /// Place ONE instance of this on an empty GameObject in any scene
    /// that has OVRCameraRig + MX_Ink. Press Play — everything builds itself.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RhythmForgeBootstrapper : MonoBehaviour
    {
        [Header("Optional overrides (leave empty for auto)")]
        [Tooltip("Leave null to auto-find in scene")]
        [SerializeField] private OVRCameraRig _cameraRigOverride;
        [Tooltip("Leave null to auto-find in scene")]
        [SerializeField] private StylusHandler _stylusOverride;
        [Tooltip("Leave null to load instrument data from Resources or built-in defaults")]
        [SerializeField] private InstrumentRegistryAsset _instrumentRegistryOverride;
        [Tooltip("Leave null to load sound mapping data from Resources or built-in defaults")]
        [SerializeField] private SoundMappingProfileAsset _soundMappingProfileOverride;
        [Tooltip("Leave null to load visual grammar data from Resources or built-in defaults")]
        [SerializeField] private VisualGrammarProfileAsset _visualGrammarProfileOverride;

        [Header("Demo")]
        [SerializeField] private bool _loadDemoOnStart = true;

        // ──────────── built references (inspectable after play) ────────────
        [Header("── Built at Runtime (read-only) ──")]
        [SerializeField] private RhythmForgeManager _manager;
        [SerializeField] private InputMapper _inputMapper;
        [SerializeField] private DrawModeController _drawMode;
        [SerializeField] private StrokeCapture _strokeCapture;
        [SerializeField] private InstanceGrabber _instanceGrabber;
        [SerializeField] private AudioEngine _audioEngine;
        [SerializeField] private SamplePlayer _samplePlayer;
        [SerializeField] private Sequencer.Sequencer _sequencer;

        private VRRigLocator _rig;
        private bool _built;

        private void Awake() => Bootstrap();
        private void OnEnable() => Bootstrap();  // catches the case where component starts disabled then gets enabled

        private void Bootstrap()
        {
            if (_built) return;
            _built = true;

            Debug.Log("[RhythmForge] Bootstrapper starting...");
            SuppressStylusInEditor();
            DisableLogitechSampleDrawing();
            ApplyConfigurationOverrides();
            _rig = VRRigLocator.Find();
            BuildAll();
            Debug.Log("[RhythmForge] Bootstrapper complete. System ready.");
        }

        private void ApplyConfigurationOverrides()
        {
            InstrumentGroups.SetRegistry(_instrumentRegistryOverride);
            SoundMappingProfiles.SetActiveProfile(_soundMappingProfileOverride);
            VisualGrammarProfiles.SetActiveProfile(_visualGrammarProfileOverride);
        }

        /// <summary>
        /// Disable Logitech's sample LineDrawing component — it draws independent
        /// blue lines on tip/middle press, conflicting with RhythmForge's StrokeCapture.
        /// </summary>
        private static void DisableLogitechSampleDrawing()
        {
            var lineDrawing = Object.FindFirstObjectByType<LineDrawing>();
            if (lineDrawing != null)
            {
                lineDrawing.enabled = false;
                Debug.Log("[RhythmForge] Disabled Logitech LineDrawing sample (replaced by StrokeCapture).");
            }
        }

        private static void SuppressStylusInEditor()
        {
#if UNITY_EDITOR
            // Always disable VrStylusHandler in the editor — OVRPlugin.initialized is true
            // even without a headset (OVRManager initializes the plugin), but the action
            // bindings for MX Ink only exist on-device, causing error floods every frame.
            var stylus = Object.FindFirstObjectByType<VrStylusHandler>();
            if (stylus != null)
            {
                stylus.enabled = false;
                Debug.Log("[RhythmForge] Editor: VrStylusHandler disabled " +
                          "(MX Ink actions only available on-device).");
            }
#endif
        }

        private bool _panelsPositioned;
        private float _panelPositionTimer;

        private void Start()
        {
            // Re-locate the VR rig now that the simulator has initialized.
            _rig = VRRigLocator.Find();
            // NOTE: RepositionPanels deferred — OVR tracking needs ~0.5s to report
            // valid head position/rotation after tracking initializes.

            // Initialize subsystems and panels before loading the demo.
            if (_manager != null)
            {
                _manager.InitializeSubsystems();
                if (_loadDemoOnStart)
                    _manager.LoadDemoSession();
            }
        }

        private void Update()
        {
            if (!_panelsPositioned)
            {
                _panelPositionTimer += Time.deltaTime;
                // Wait 0.5s for tracking to stabilize, then validate head forward is valid
                if (_panelPositionTimer >= 0.5f)
                {
                    _rig = VRRigLocator.Find();
                    Transform head = _rig?.CenterEye;
                    if (head != null)
                    {
                        Vector3 fwd = head.forward;
                        fwd.y = 0f;
                        // Only position once we have a valid non-zero forward
                        if (fwd.sqrMagnitude > 0.001f)
                        {
                            _panelsPositioned = true;
                            RepositionPanels();
                        }
                    }
                }
            }
        }

        // Holds canvas transforms so Start() can reposition them after rig is found
        private readonly List<(Canvas canvas, float x, float y, float z, PanelDragCoordinator.DragMembership membership)> _panelPositions
            = new List<(Canvas, float, float, float, PanelDragCoordinator.DragMembership)>();

        private InputMapper _inputMapperRef; // set after BuildSubsystems, used by RegisterPanel

        private PanelDragCoordinator _dragCoordinator;

        private void RegisterPanel(Canvas canvas, float x, float y, float z,
            PanelDragCoordinator.DragMembership membership = PanelDragCoordinator.DragMembership.Independent)
        {
            _panelPositions.Add((canvas, x, y, z, membership));
            // Panels registered with coordinator after it's created
        }

        private void CreateDragCoordinator(InputMapper input)
        {
            // Remove old individual draggers first (clean up from previous versions)
            foreach (var (canvas, _, _, _, _) in _panelPositions)
            {
                if (canvas != null)
                {
                    var oldDragger = canvas.GetComponent<PanelDragger>();
                    if (oldDragger != null) DestroyImmediate(oldDragger);
                }
            }

            var go = new GameObject("PanelDragCoordinator");
            go.transform.SetParent(transform);
            _dragCoordinator = go.AddComponent<PanelDragCoordinator>();
            _dragCoordinator.Configure(input, _rig != null ? _rig.CenterEye : null);

            // Register all panels with the coordinator
            foreach (var (canvas, _, _, _, membership) in _panelPositions)
            {
                if (canvas != null)
                    _dragCoordinator.RegisterPanel(canvas, membership);
            }
        }

        private void RepositionPanels()
        {
            Transform head = _rig?.CenterEye;
            _dragCoordinator?.SetLookAtTarget(head);

            // Use head's actual position but clamp to a sensible standing height.
            Vector3 headPos = head != null ? head.position : new Vector3(0f, 1.6f, 0f);
            // Guarantee panels are never below knee height regardless of tracking origin.
            if (headPos.y < 0.5f) headPos.y = 1.6f;

            // Flatten head forward to horizontal plane — panels sit on the wall in front.
            Vector3 fwd   = Vector3.forward;
            Vector3 right = Vector3.right;
            if (head != null)
            {
                fwd = head.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                right = Vector3.Cross(Vector3.up, fwd).normalized;
            }

            Quaternion mainGroupFacing = Quaternion.LookRotation(fwd, Vector3.up);

            foreach (var (canvas, x, y, z, membership) in _panelPositions)
            {
                if (canvas == null) continue;

                canvas.transform.SetParent(null, false);

                // x = lateral offset, y = vertical offset from eye height, z = forward distance
                Vector3 worldPos = headPos
                    + fwd   * z
                    + right * x
                    + Vector3.up * y;

                canvas.transform.position = worldPos;

                if (membership == PanelDragCoordinator.DragMembership.MainGroup)
                {
                    canvas.transform.rotation = mainGroupFacing;
                }
                else
                {
                    Vector3 toUser = headPos - worldPos;
                    toUser.y = 0f; // flatten to horizontal
                    if (toUser.sqrMagnitude > 0.001f)
                    {
                        Quaternion panelFacing = Quaternion.LookRotation(-toUser.normalized, Vector3.up);
                        canvas.transform.rotation = panelFacing;
                    }
                }
            }

            // Disable canvas component on inactive panels so they are fully invisible.
            foreach (var (canvas, _, _, _, _) in _panelPositions)
            {
                if (canvas == null) continue;
                if (!canvas.gameObject.activeSelf)
                    canvas.enabled = false;
            }
        }

        private void BuildAll()
        {
            // ── 0. EventSystem (required for UI interaction) ──
            EnsureEventSystem();

            // ── 1. Audio ──
            BuildAudio();

            // ── 2. Default stroke material (StrokeCapture only; manager creates per-type materials itself) ──
            var defaultStrokeMat = MaterialFactory.CreateStrokeMaterial(PatternType.RhythmLoop);

            // ── 3. Core subsystems ──
            var subsystems = BuildSubsystems(defaultStrokeMat);

            // ── 4. UI panels ──
            var panels = BuildUIPanels(subsystems.strokeCapture, subsystems.drawMode);

            // ── 4b. Create drag coordinator (replaces individual panel draggers) ──
            CreateDragCoordinator(subsystems.inputMapper);

            // ── 5. Instance container ──
            var instanceContainer = new GameObject("InstanceContainer").transform;
            instanceContainer.SetParent(transform);

            // ── 6. Manager configuration (4 params) ──
            _manager = GetOrAdd<RhythmForgeManager>(gameObject);
            _manager.Configure(
                new ManagerSubsystems
                {
                    audioEngine     = subsystems.audioEngine,
                    samplePlayer    = subsystems.samplePlayer,
                    sequencer       = subsystems.sequencer,
                    strokeCapture   = subsystems.strokeCapture,
                    drawMode        = subsystems.drawMode,
                    inputMapper     = subsystems.inputMapper,
                    instanceGrabber = subsystems.instanceGrabber
                },
                new ManagerPanels
                {
                    commitCard    = panels.commitCard,
                    inspector     = panels.inspector,
                    dock          = panels.dock,
                    transport     = panels.transport,
                    sceneStrip    = panels.sceneStrip,
                    arrangement   = panels.arrangement,
                    toast         = panels.toast,
                    genreSelector = panels.genreSelector
                },
                instanceContainer,
                _rig != null ? _rig.CenterEye : null
            );

            // Cache for inspector readout
            _inputMapper     = subsystems.inputMapper;
            _drawMode        = subsystems.drawMode;
            _strokeCapture   = subsystems.strokeCapture;
            _instanceGrabber = subsystems.instanceGrabber;
            _audioEngine     = subsystems.audioEngine;
            _samplePlayer    = subsystems.samplePlayer;
            _sequencer       = subsystems.sequencer;
        }

        // ──────────────────────────────────────────────────────────
        //  AUDIO
        // ──────────────────────────────────────────────────────────

        private void BuildAudio()
        {
            var audioGo = new GameObject("AudioEngine");
            audioGo.transform.SetParent(transform);

            var playerGo = new GameObject("SamplePlayer");
            playerGo.transform.SetParent(audioGo.transform);

            _samplePlayer = playerGo.AddComponent<SamplePlayer>();
            _audioEngine  = audioGo.AddComponent<AudioEngine>();
            _samplePlayer.Configure();
            _audioEngine.Configure(_samplePlayer);
        }

        // ──────────────────────────────────────────────────────────
        //  SUBSYSTEMS
        // ──────────────────────────────────────────────────────────

        private struct SubsystemRefs
        {
            public InputMapper inputMapper;
            public DrawModeController drawMode;
            public StrokeCapture strokeCapture;
            public InstanceGrabber instanceGrabber;
            public AudioEngine audioEngine;
            public SamplePlayer samplePlayer;
            public Sequencer.Sequencer sequencer;
        }

        private SubsystemRefs BuildSubsystems(Material defaultStrokeMat)
        {
            var refs = new SubsystemRefs();

            // InputMapper
            var inputGo = new GameObject("InputMapper");
            inputGo.transform.SetParent(transform);
            refs.inputMapper = inputGo.AddComponent<InputMapper>();
            var stylus = _stylusOverride != null
                ? _stylusOverride
                : Object.FindFirstObjectByType<StylusHandler>();
            refs.inputMapper.Configure(stylus);

            // DrawModeController
            var dmGo = new GameObject("DrawModeController");
            dmGo.transform.SetParent(transform);
            refs.drawMode = dmGo.AddComponent<DrawModeController>();

            // StylusUIPointer — created first so StrokeCapture can reference it
            var uiPointerGo = new GameObject("StylusUIPointer");
            uiPointerGo.transform.SetParent(transform);
            var uiPointer = uiPointerGo.AddComponent<StylusUIPointer>();

            var uiRayLine = uiPointerGo.AddComponent<LineRenderer>();
            uiRayLine.positionCount = 2;
            uiRayLine.startWidth = 0.003f;
            uiRayLine.endWidth   = 0.001f;
            uiRayLine.useWorldSpace = true;
            uiRayLine.shadowCastingMode = ShadowCastingMode.Off;
            var uiRayMat = new Material(Shader.Find("Sprites/Default"));
            uiRayMat.color = new Color(1f, 1f, 0.4f, 0.6f);
            uiRayLine.material = uiRayMat;
            uiRayLine.enabled = false;
            uiPointer.Configure(refs.inputMapper, uiRayLine, 1 << 5);

            // StrokeCapture
            var scGo = new GameObject("StrokeCapture");
            scGo.transform.SetParent(transform);
            refs.strokeCapture = scGo.AddComponent<StrokeCapture>();
            refs.strokeCapture.Configure(
                refs.inputMapper,
                refs.drawMode,
                _rig.CenterEye,
                defaultStrokeMat,
                uiPointer);

            // InstanceGrabber + ray line renderer
            var igGo = new GameObject("InstanceGrabber");
            igGo.transform.SetParent(transform);
            refs.instanceGrabber = igGo.AddComponent<InstanceGrabber>();

            var rayLine = igGo.AddComponent<LineRenderer>();
            rayLine.positionCount = 2;
            rayLine.startWidth = 0.002f;
            rayLine.endWidth   = 0.0005f;
            rayLine.useWorldSpace = true;
            rayLine.shadowCastingMode = ShadowCastingMode.Off;
            var rayMat = new Material(Shader.Find("Sprites/Default"));
            rayMat.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            rayLine.material = rayMat;
            rayLine.enabled  = false;

            // Layer 6 is empty by default in this project (see TagManager.asset)
            LayerMask instanceLayer = 1 << 6;
            refs.instanceGrabber.Configure(
                refs.inputMapper,
                _rig.LeftController,
                rayLine,
                instanceLayer);

            // Sequencer
            var seqGo = new GameObject("Sequencer");
            seqGo.transform.SetParent(transform);
            refs.sequencer = seqGo.AddComponent<Sequencer.Sequencer>();
            // Inject audio engine via SerializeField backing using the Configure method
            // (added in our edit pass)
            SetPrivateField(refs.sequencer, "_audioEngine", _audioEngine);
            refs.sequencer.SetSamplePlayer(_samplePlayer);

            refs.audioEngine  = _audioEngine;
            refs.samplePlayer = _samplePlayer;
            return refs;
        }

        // ──────────────────────────────────────────────────────────
        //  UI PANELS
        // ──────────────────────────────────────────────────────────

        private struct PanelRefs
        {
            public CommitCardPanel    commitCard;
            public InspectorPanel     inspector;
            public DockPanel          dock;
            public TransportPanel     transport;
            public SceneStripPanel    sceneStrip;
            public ArrangementPanel   arrangement;
            public ToastMessage       toast;
            public GenreSelectorPanel genreSelector;
        }

        private PanelRefs BuildUIPanels(StrokeCapture strokeCapture, DrawModeController drawMode)
        {
            var refs = new PanelRefs();
            // Use CenterEye if available now; RepositionPanels() in Start() corrects positions.
            Transform head = _rig.CenterEye;

            refs.toast         = BuildToastPanel(head);
            refs.transport     = BuildTransportPanel(head);
            refs.sceneStrip    = BuildSceneStripPanel(head);
            refs.commitCard    = BuildCommitCardPanel(head, strokeCapture);
            refs.inspector     = BuildInspectorPanel(head);
            refs.dock          = BuildDockPanel(head, drawMode);
            refs.arrangement   = BuildArrangementPanel(head);
            refs.genreSelector = BuildGenreSelectorPanel(head);

            return refs;
        }

        // ──────── Toast ────────

        private ToastMessage BuildToastPanel(Transform head)
        {
            var canvas = UIFactory.CreateWorldCanvas("Toast",
                transform, new Vector2(480, 60),
                PositionInFront(0f, 0.28f, 1.6f), 0.001f);
            RegisterPanel(canvas, 0f, 0.28f, 1.6f);

            var cg = UIFactory.AddCanvasGroup(canvas.gameObject);
            var text = UIFactory.CreateCenteredText(canvas.transform, "ToastText",
                "", 22, Color.white);

            var toast = canvas.gameObject.AddComponent<ToastMessage>();
            toast.SetUIRefs(text, cg, head);
            return toast;
        }

        // ──────── Transport ────────

        private TransportPanel BuildTransportPanel(Transform head)
        {
            var canvas = UIFactory.CreateWorldCanvas("TransportPanel",
                transform, new Vector2(640, 100),
                PositionInFront(0f, -0.22f, 1.1f), 0.001f);
            RegisterPanel(canvas, 0f, -0.22f, 1.1f, PanelDragCoordinator.DragMembership.MainGroup);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(640, 100), MaterialFactory.PanelBg);

            // Play/Stop button
            var playBtn = UIFactory.CreateButton(canvas.transform, "PlayStopButton", "Play",
                new Rect(8, 8, 100, 84), MaterialFactory.ButtonActive, Color.white, 20, null);
            var playLabel = playBtn.GetComponentInChildren<Text>();

            // Params toggle button (near Mode)
            var paramsBtn = UIFactory.CreateButton(canvas.transform, "ToggleParamsButton", "Params\nON",
                new Rect(420, 8, 100, 84), new Color(0.28f, 0.32f, 0.42f), Color.white, 16, null);
            var paramsLabel = paramsBtn.GetComponentInChildren<Text>();

            // Mode button
            var modeBtn = UIFactory.CreateButton(canvas.transform, "ModeButton", "Mode\nRhythm",
                new Rect(532, 8, 100, 84), TypeColors.RhythmLoop, Color.white, 18, null);
            var modeLabel = modeBtn.GetComponentInChildren<Text>();

            // Info labels
            var bpmText    = UIFactory.CreateRectText(canvas.transform, "BpmText",
                "85 BPM", 18, Color.white, TextAnchor.MiddleLeft,
                new Rect(120, 58, 280, 34));
            var keyText    = UIFactory.CreateRectText(canvas.transform, "KeyText",
                "A minor", 16, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleLeft,
                new Rect(120, 28, 280, 28));
            var statusText = UIFactory.CreateRectText(canvas.transform, "StatusText",
                "Idle", 14, new Color(0.55f, 0.55f, 0.65f), TextAnchor.MiddleLeft,
                new Rect(120, 6, 400, 22));

            var panel = canvas.gameObject.AddComponent<TransportPanel>();
            panel.SetUIRefs(playBtn, playLabel, modeBtn, modeLabel, bpmText, keyText, statusText, paramsBtn, paramsLabel);
            return panel;
        }

        // ──────── Scene Strip ────────

        private SceneStripPanel BuildSceneStripPanel(Transform head)
        {
            var canvas = UIFactory.CreateWorldCanvas("SceneStripPanel",
                transform, new Vector2(540, 70),
                PositionInFront(0f, -0.36f, 1.1f), 0.001f);
            RegisterPanel(canvas, 0f, -0.36f, 1.1f, PanelDragCoordinator.DragMembership.MainGroup);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(540, 70), MaterialFactory.PanelBg);

            var sceneNames = new[] { "Scene A", "Scene B", "Scene C", "Scene D" };
            var buttons    = new List<Button>();
            var labels     = new List<Text>();
            float bw = 128f, bh = 52f, gap = 6f, startX = 8f, cy = 9f;

            for (int i = 0; i < 4; i++)
            {
                float bx = startX + i * (bw + gap);
                var btn = UIFactory.CreateButton(canvas.transform, $"SceneBtn_{i}",
                    sceneNames[i], new Rect(bx, cy, bw, bh),
                    MaterialFactory.ButtonDefault, Color.white, 16, null);
                buttons.Add(btn);
                labels.Add(btn.GetComponentInChildren<Text>());
            }

            var panel = canvas.gameObject.AddComponent<SceneStripPanel>();
            panel.SetUIRefs(buttons, labels);
            return panel;
        }

        // ──────── Commit Card ────────

        private CommitCardPanel BuildCommitCardPanel(Transform head, StrokeCapture strokeCapture)
        {
            var canvas = UIFactory.CreateWorldCanvas("CommitCardPanel",
                transform, new Vector2(480, 260),
                PositionInFront(0f, 0f, 1.2f), 0.001f);
            RegisterPanel(canvas, 0f, 0f, 1.2f);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(480, 260), MaterialFactory.PanelBg);

            // Color bar (top)
            var colorBar = UIFactory.CreateImage(canvas.transform, "TypeColorBar",
                Color.cyan, new Rect(0, 238, 480, 22));

            // Type label
            var typeLabel = UIFactory.CreateRectText(canvas.transform, "TypeLabel",
                "RhythmLoop", 14, new Color(0.7f, 1f, 1f), TextAnchor.MiddleLeft,
                new Rect(12, 212, 300, 24));

            // Name
            var nameText = UIFactory.CreateRectText(canvas.transform, "NameText",
                "Beat-01", 22, Color.white, TextAnchor.MiddleLeft,
                new Rect(12, 180, 456, 30));

            // Summary
            var summaryText = UIFactory.CreateRectText(canvas.transform, "SummaryText",
                "", 15, new Color(0.8f, 0.9f, 1f), TextAnchor.UpperLeft,
                new Rect(12, 98, 456, 80));

            // Details
            var detailsText = UIFactory.CreateRectText(canvas.transform, "DetailsText",
                "", 13, new Color(0.6f, 0.65f, 0.7f), TextAnchor.UpperLeft,
                new Rect(12, 56, 456, 40));

            // Buttons
            var saveBtn = UIFactory.CreateButton(canvas.transform, "SaveButton", "Save",
                new Rect(12, 8, 140, 44), MaterialFactory.ButtonActive, Color.white, 18, null);
            var saveDupBtn = UIFactory.CreateButton(canvas.transform, "SaveDupButton", "Save+Dup",
                new Rect(160, 8, 160, 44), MaterialFactory.ButtonDefault, Color.white, 16, null);
            var discardBtn = UIFactory.CreateButton(canvas.transform, "DiscardButton", "Discard",
                new Rect(328, 8, 140, 44), MaterialFactory.ButtonDanger, Color.white, 16, null);

            canvas.gameObject.SetActive(false);

            var panel = canvas.gameObject.AddComponent<CommitCardPanel>();
            panel.SetUIRefs(nameText, summaryText, detailsText,
                typeLabel, colorBar, saveBtn, saveDupBtn, discardBtn, head);
            return panel;
        }

        // ──────── Inspector ────────

        private InspectorPanel BuildInspectorPanel(Transform head)
        {
            var canvas = UIFactory.CreateWorldCanvas("InspectorPanel",
                transform, new Vector2(340, 420),
                PositionInFront(0.45f, 0.05f, 1.1f), 0.001f);
            RegisterPanel(canvas, 0.45f, 0.05f, 1.1f);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(340, 420), MaterialFactory.PanelBg);

            // Pattern identity
            var typeColorBar = UIFactory.CreateImage(canvas.transform, "TypeColorBar",
                Color.cyan, new Rect(0, 398, 340, 22));
            var pName = UIFactory.CreateRectText(canvas.transform, "PatternName",
                "Pattern", 20, Color.white, TextAnchor.MiddleLeft, new Rect(10, 368, 320, 28));
            var pType = UIFactory.CreateRectText(canvas.transform, "PatternType",
                "RhythmLoop", 14, new Color(0.7f, 1f, 1f), TextAnchor.MiddleLeft, new Rect(10, 348, 200, 20));
            var pBars = UIFactory.CreateRectText(canvas.transform, "PatternBars",
                "2 bars", 14, new Color(0.6f, 0.7f, 0.8f), TextAnchor.MiddleRight, new Rect(200, 348, 130, 20));

            // Shape summary
            var shapeSummary = UIFactory.CreateRectText(canvas.transform, "ShapeSummary",
                "", 13, new Color(0.75f, 0.85f, 0.95f), TextAnchor.UpperLeft, new Rect(10, 295, 320, 52));
            var traitChips = UIFactory.CreateRectText(canvas.transform, "TraitChips",
                "", 12, new Color(0.55f, 0.65f, 0.8f), TextAnchor.UpperLeft, new Rect(10, 270, 320, 24));

            // 6 metric bars (simplified — just sliders with labels)
            var metricBars   = new List<Slider>();
            var metricLabels = new List<Text>();
            string[] mLabels = { "Metric 1", "Metric 2", "Metric 3", "Metric 4", "Metric 5", "Metric 6" };
            for (int i = 0; i < 6; i++)
            {
                float my = 240f - i * 34f;
                var lbl = UIFactory.CreateRectText(canvas.transform, $"MetricLabel_{i}",
                    mLabels[i], 12, new Color(0.6f, 0.7f, 0.8f), TextAnchor.MiddleLeft,
                    new Rect(10, my + 16, 100, 18));
                var bar = UIFactory.CreateSlider(canvas.transform, $"MetricBar_{i}",
                    0f, 1f, 0.5f, new Rect(112, my + 12, 218, 22), null);
                metricLabels.Add(lbl);
                metricBars.Add(bar);
            }

            // Instance controls
            var depthLbl = UIFactory.CreateRectText(canvas.transform, "DepthLabel",
                "Depth", 13, new Color(0.6f, 0.7f, 0.8f), TextAnchor.MiddleLeft, new Rect(10, 42, 70, 20));
            var depthSlider = UIFactory.CreateSlider(canvas.transform, "DepthSlider",
                0f, 1f, 0.3f, new Rect(82, 38, 248, 26), null);

            var muteBtn  = UIFactory.CreateButton(canvas.transform, "MuteButton", "Mute",
                new Rect(10, 8, 90, 28), MaterialFactory.ButtonDefault, Color.white, 14, null);
            var muteLabel = muteBtn.GetComponentInChildren<Text>();
            var removeBtn = UIFactory.CreateButton(canvas.transform, "RemoveButton", "Remove",
                new Rect(108, 8, 100, 28), MaterialFactory.ButtonDanger, Color.white, 14, null);
            var dupBtn   = UIFactory.CreateButton(canvas.transform, "DuplicateButton", "Dup",
                new Rect(216, 8, 64, 28), MaterialFactory.ButtonDefault, Color.white, 14, null);

            // Preset dropdown
            var presetOptions = new List<string> { "(default)" };
            foreach (var p in InstrumentPresets.All) presetOptions.Add(p.label);
            var presetDrop = UIFactory.CreateDropdown(canvas.transform, "PresetDropdown",
                presetOptions, new Rect(284, 8, 46, 28), null);

            // Mix readout
            var panText    = UIFactory.CreateRectText(canvas.transform, "PanText",
                "Pan: 0.00", 12, new Color(0.55f, 0.65f, 0.75f), TextAnchor.MiddleLeft, new Rect(10, -20, 100, 18));
            var gainText   = UIFactory.CreateRectText(canvas.transform, "GainText",
                "Gain: 0.80", 12, new Color(0.55f, 0.65f, 0.75f), TextAnchor.MiddleLeft, new Rect(118, -20, 100, 18));
            var brightText = UIFactory.CreateRectText(canvas.transform, "BrightText",
                "Bright: 0.50", 12, new Color(0.55f, 0.65f, 0.75f), TextAnchor.MiddleLeft, new Rect(226, -20, 104, 18));

            canvas.gameObject.SetActive(false);

            var panel = canvas.gameObject.AddComponent<InspectorPanel>();
            panel.SetUIRefs(pName, pType, pBars, typeColorBar, shapeSummary, traitChips,
                metricBars, metricLabels, depthSlider, muteBtn, muteLabel,
                removeBtn, dupBtn, presetDrop, panText, gainText, brightText, head);
            return panel;
        }

        // ──────── Dock Panel ────────

        private DockPanel BuildDockPanel(Transform head, DrawModeController drawMode)
        {
            var canvas = UIFactory.CreateWorldCanvas("DockPanel",
                transform, new Vector2(340, 380),
                PositionInFront(-0.45f, 0.05f, 1.1f), 0.001f);
            RegisterPanel(canvas, -0.45f, 0.05f, 1.1f, PanelDragCoordinator.DragMembership.MainGroup);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(340, 380), MaterialFactory.PanelBg);

            // Tab buttons (top row)
            var instrTab = UIFactory.CreateButton(canvas.transform, "InstrumentsTab", "Instruments",
                new Rect(4, 352, 108, 24), MaterialFactory.ButtonActive, Color.white, 13, null);
            var patternsTab = UIFactory.CreateButton(canvas.transform, "PatternsTab", "Patterns",
                new Rect(116, 352, 100, 24), MaterialFactory.ButtonDefault, Color.white, 13, null);
            var scenesTab = UIFactory.CreateButton(canvas.transform, "ScenesTab", "Scenes",
                new Rect(220, 352, 116, 24), MaterialFactory.ButtonDefault, Color.white, 13, null);

            // Draw mode label
            var modeLbl = UIFactory.CreateRectText(canvas.transform, "DrawModeLabel",
                "Mode: Rhythm", 15, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleLeft,
                new Rect(10, 324, 320, 26));

            // ── Instruments sub-panel ──
            var instrPanelGo = new GameObject("InstrumentsPanel");
            instrPanelGo.transform.SetParent(canvas.transform, false);
            var instrRt = instrPanelGo.AddComponent<RectTransform>();
            instrRt.anchorMin = Vector2.zero; instrRt.anchorMax = Vector2.zero;
            instrRt.pivot = Vector2.zero;
            instrRt.sizeDelta = new Vector2(340, 316);
            instrRt.anchoredPosition = new Vector2(0, 4);
            var instrScroll = UIFactory.CreateScrollView(instrPanelGo.transform, "GroupList",
                new Rect(0, 0, 340, 316));

            // ── Patterns sub-panel ──
            var patternPanelGo = new GameObject("PatternsPanel");
            patternPanelGo.transform.SetParent(canvas.transform, false);
            var patternRt = patternPanelGo.AddComponent<RectTransform>();
            patternRt.anchorMin = Vector2.zero; patternRt.anchorMax = Vector2.zero;
            patternRt.pivot = Vector2.zero;
            patternRt.sizeDelta = new Vector2(340, 316);
            patternRt.anchoredPosition = new Vector2(0, 4);
            patternPanelGo.SetActive(false);
            var patternScroll = UIFactory.CreateScrollView(patternPanelGo.transform, "PatternList",
                new Rect(0, 0, 340, 316));

            // ── Scenes sub-panel ──
            var scenePanelGo = new GameObject("ScenesPanel");
            scenePanelGo.transform.SetParent(canvas.transform, false);
            var sceneRt = scenePanelGo.AddComponent<RectTransform>();
            sceneRt.anchorMin = Vector2.zero; sceneRt.anchorMax = Vector2.zero;
            sceneRt.pivot = Vector2.zero;
            sceneRt.sizeDelta = new Vector2(340, 316);
            sceneRt.anchoredPosition = new Vector2(0, 4);
            scenePanelGo.SetActive(false);

            var panel = canvas.gameObject.AddComponent<DockPanel>();
            panel.SetUIRefs(
                instrTab, patternsTab, scenesTab,
                instrPanelGo, patternPanelGo, scenePanelGo,
                instrScroll.content, modeLbl,
                patternScroll.content, scenePanelGo.transform, head);
            return panel;
        }

        // ──────── Arrangement ────────

        private ArrangementPanel BuildArrangementPanel(Transform head)
        {
            var canvas = UIFactory.CreateWorldCanvas("ArrangementPanel",
                transform, new Vector2(560, 100),
                PositionInFront(0f, -0.50f, 1.1f), 0.001f);
            RegisterPanel(canvas, 0f, -0.50f, 1.1f, PanelDragCoordinator.DragMembership.MainGroup);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(560, 100), MaterialFactory.PanelBg);

            var slotUIs = new List<ArrangementPanel.SlotUI>();
            float sw = 62f, gap = 4f;

            for (int i = 0; i < 8; i++)
            {
                float sx = 6f + i * (sw + gap);

                var slotLabel = UIFactory.CreateRectText(canvas.transform, $"Slot{i + 1}Label",
                    $"Slot {i + 1}", 11, new Color(0.5f, 0.6f, 0.7f), TextAnchor.MiddleCenter,
                    new Rect(sx, 78, sw, 18));

                var sceneBtn = UIFactory.CreateButton(canvas.transform, $"SceneBtn{i}", "--",
                    new Rect(sx, 46, sw, 28), MaterialFactory.ButtonDefault, Color.white, 11, null);
                var sceneLabel = sceneBtn.GetComponentInChildren<Text>();

                var barsBtn = UIFactory.CreateButton(canvas.transform, $"BarsBtn{i}", "4",
                    new Rect(sx, 14, sw, 28), MaterialFactory.ButtonDefault, Color.white, 11, null);
                var barsLabel = barsBtn.GetComponentInChildren<Text>();

                slotUIs.Add(new ArrangementPanel.SlotUI
                {
                    sceneButton = sceneBtn,
                    sceneLabel  = sceneLabel,
                    barsButton  = barsBtn,
                    barsLabel   = barsLabel,
                    slotLabel   = slotLabel
                });
            }

            var panel = canvas.gameObject.AddComponent<ArrangementPanel>();
            SetPrivateField(panel, "_slots", slotUIs);
            return panel;
        }

        // ──────── Genre Selector ────────

        private GenreSelectorPanel BuildGenreSelectorPanel(Transform head)
        {
            var canvas = UIFactory.CreateWorldCanvas("GenreSelectorPanel",
                transform, new Vector2(400, 200),
                PositionInFront(0.5f, 0.25f, 1.1f), 0.001f);
            RegisterPanel(canvas, 0.5f, 0.25f, 1.1f);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(400, 200), MaterialFactory.PanelBg);

            // Title
            UIFactory.CreateRectText(canvas.transform, "GenreTitle",
                "GENRE", 14, new Color(0.5f, 0.6f, 0.8f), TextAnchor.MiddleCenter,
                new Rect(0, 172, 400, 22));

            var genres   = new System.Collections.Generic.List<string>   { "electronic", "newage", "jazz" };
            var labels   = new System.Collections.Generic.List<string>   { "Electronic", "New Age", "Jazz" };
            var buttons  = new System.Collections.Generic.List<Button>();
            var btnTexts = new System.Collections.Generic.List<Text>();
            var descLines = new string[]
            {
                "Lo-Fi, Trap & Dream synthesis",
                "Meditative bowls, kalimba & drones",
                "Brush kit, Rhodes & jazz voicings"
            };

            float bw = 114f, bh = 56f, gap = 8f, startX = 13f, by = 96f;

            for (int i = 0; i < genres.Count; i++)
            {
                float bx = startX + i * (bw + gap);
                var color = i == 0
                    ? new Color(0.24f, 0.72f, 0.96f, 1f)   // electronic blue
                    : i == 1
                        ? new Color(0.36f, 0.66f, 0.44f, 1f)  // new age green
                        : new Color(0.62f, 0.44f, 0.28f, 1f); // jazz amber

                var btn = UIFactory.CreateButton(canvas.transform, $"GenreBtn_{genres[i]}",
                    labels[i],
                    new Rect(bx, by, bw, bh),
                    new Color(0.18f, 0.18f, 0.24f, 1f), Color.white, 15, null);
                buttons.Add(btn);
                btnTexts.Add(btn.GetComponentInChildren<Text>());
            }

            // Description label
            var descLabel = UIFactory.CreateRectText(canvas.transform, "GenreDescription",
                "Lo-Fi, Trap & Dream synthesis", 12,
                new Color(0.6f, 0.7f, 0.85f), TextAnchor.MiddleCenter,
                new Rect(8, 56, 384, 34));

            // Sub-labels for each genre
            float lby = 74f;
            for (int i = 0; i < genres.Count; i++)
            {
                float lbx = startX + i * (bw + gap);
                UIFactory.CreateRectText(canvas.transform, $"GenreDesc_{genres[i]}",
                    descLines[i], 10, new Color(0.45f, 0.5f, 0.6f), TextAnchor.UpperCenter,
                    new Rect(lbx, lby, bw, 20));
            }

            // Info line at bottom
            UIFactory.CreateRectText(canvas.transform, "GenreInfo",
                "Switching genre re-derives all patterns", 11,
                new Color(0.38f, 0.4f, 0.5f), TextAnchor.MiddleCenter,
                new Rect(8, 8, 384, 18));

            var panel = canvas.gameObject.AddComponent<GenreSelectorPanel>();
            panel.SetUIRefs(buttons, btnTexts, genres, descLabel);
            return panel;
        }

        // ──────────────────────────────────────────────────────────
        //  UTILITY
        // ──────────────────────────────────────────────────────────

        private Vector3 PositionInFront(float xOffset, float yOffset, float zDistance)
        {
            if (_rig.CenterEye != null)
            {
                var fwd = _rig.CenterEye.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();

                var right = Vector3.Cross(Vector3.up, fwd).normalized;
                return _rig.CenterEye.position
                    + fwd    * zDistance
                    + right  * xOffset
                    + Vector3.up * yOffset;
            }

            // Fallback if no head tracking yet
            return new Vector3(xOffset, 1.3f + yOffset, zDistance);
        }

        /// <summary>
        /// Creates an EventSystem if one doesn't already exist.
        /// Uses OVRInputModule on-device, StandaloneInputModule in editor/simulator.
        /// Without this, GraphicRaycaster can't process pointer events.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

            // Project uses New Input System — StandaloneInputModule throws InvalidOperationException.
            // Try InputSystemUIInputModule first, fall back to StandaloneInputModule only if unavailable.
            var inputModuleType =
                System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
                System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI");

            if (inputModuleType != null)
                go.AddComponent(inputModuleType);
            else
                go.AddComponent<StandaloneInputModule>();

            Debug.Log("[RhythmForge] EventSystem created.");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>
        /// Sets a private [SerializeField] field on a component by name using reflection.
        /// Used only where the target class's Configure/SetUIRefs path is unavailable
        /// (e.g. Sequencer._audioEngine and ArrangementPanel._slots).
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
            else
                Debug.LogWarning($"[RhythmForge] SetPrivateField: '{fieldName}' not found on {type.Name}");
        }
    }
}
