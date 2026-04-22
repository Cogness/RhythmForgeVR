using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
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

        [Header("Audio Mixer")]
        [Tooltip("Assign Assets/RhythmForge/Audio/RhythmForgeMixer.mixer here")]
        [SerializeField] private AudioMixer _rhythmForgeMixer;

        [Header("Startup")]
        [Tooltip("When enabled, Play Mode starts in a fresh guided session so the Phase C UI is visible immediately.")]
        [SerializeField] private bool _startFreshGuidedSessionOnStart = true;

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
        [SerializeField] private ImmersionController _immersionController;

        private VRRigLocator _rig;
        private GameObject _immersiveEnvironment;
        private TransportPanel _transportPanelRef;
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

            // Initialize subsystems and panels before loading startup session content.
            if (_manager != null)
            {
                _manager.InitializeSubsystems();
                if (_startFreshGuidedSessionOnStart)
                    _manager.LoadFreshGuidedSession();
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

            // The merged Transport panel shifts its world position based on guided/free mode so
            // the Transport strip's top edge stays anchored when the panel grows/shrinks. We
            // just overwrote that shift with the registered (guided-mode) baseline, so ask the
            // panel to re-apply the current mode's offset now.
            _transportPanelRef?.ResetAfterReposition();
        }

        private void BuildAll()
        {
            // ── 0. EventSystem (required for UI interaction) ──
            EnsureEventSystem();

            // ── 1. Audio ──
            BuildAudio();

            // ── 1b. Immersive environment + PassThrough controller ──
            BuildImmersion();

            // ── 2. Default stroke material (StrokeCapture only; manager creates per-type materials itself) ──
            var defaultStrokeMat = MaterialFactory.CreateStrokeMaterial(PatternType.Percussion);

            // ── 3. Core subsystems ──
            var subsystems = BuildSubsystems(defaultStrokeMat);

            // ── 4. UI panels ──
            var panels = BuildUIPanels(subsystems.strokeCapture, subsystems.drawMode);

            // ── 4b. Create drag coordinator (replaces individual panel draggers) ──
            CreateDragCoordinator(subsystems.inputMapper);

            // ── 4c. Wire immersion toggle into the Transport panel. ──
            if (_immersionController != null && panels.transport != null)
                panels.transport.BindImmersion(_immersionController);

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
                    phaseController = subsystems.phaseController,
                    inputMapper     = subsystems.inputMapper,
                    instanceGrabber = subsystems.instanceGrabber
                },
                new ManagerPanels
                {
                    commitCard    = panels.commitCard,
                    inspector     = panels.inspector,
                    dock          = panels.dock,
                    transport     = panels.transport,
                    phase         = panels.phase,
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
            _audioEngine.Configure(_samplePlayer, _rhythmForgeMixer);
            // Route pool sources to the genre group active at startup
            _audioEngine.SetGenre(GenreRegistry.GetActive().Id);
        }

        // ──────────────────────────────────────────────────────────
        //  IMMERSION (PassThrough ↔ Immersed)
        // ──────────────────────────────────────────────────────────

        private void BuildImmersion()
        {
            // Parent the environment under this bootstrapper so it gets cleaned up with it.
            _immersiveEnvironment = ImmersiveEnvironmentFactory.Build(transform);

            _immersionController = GetOrAdd<ImmersionController>(gameObject);
            _immersionController.Configure(_immersiveEnvironment, _rig != null ? _rig.CenterEye : null);
        }

        // ──────────────────────────────────────────────────────────
        //  SUBSYSTEMS
        // ──────────────────────────────────────────────────────────

        private struct SubsystemRefs
        {
            public InputMapper inputMapper;
            public DrawModeController drawMode;
            public PhaseController phaseController;
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

            var phaseGo = new GameObject("PhaseController");
            phaseGo.transform.SetParent(transform);
            refs.phaseController = phaseGo.AddComponent<PhaseController>();

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
            public PhasePanel         phase;
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

            // Transport + Phase + SceneStrip + Arrangement + Dock + Genre all live inside one
            // mega-canvas that drags as a single unit.
            var tp = BuildMergedTransportPanel(head);
            refs.transport     = tp.transport;
            refs.phase         = tp.phase;
            refs.sceneStrip    = tp.sceneStrip;
            refs.arrangement   = tp.arrangement;
            refs.dock          = tp.dock;
            refs.genreSelector = tp.genreSelector;
            _transportPanelRef = tp.transport;

            refs.commitCard    = BuildCommitCardPanel(head, strokeCapture);
            refs.inspector     = BuildInspectorPanel(head);

            return refs;
        }

        private struct MergedTransportBuild
        {
            public TransportPanel      transport;
            public PhasePanel          phase;
            public SceneStripPanel     sceneStrip;
            public ArrangementPanel    arrangement;
            public DockPanel           dock;
            public GenreSelectorPanel  genreSelector;
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

        // ──────── Merged Transport Mega-Panel ────────
        // One canvas contains:
        //   • Dock (left, full height)
        //   • Genre (top of right column)
        //   • Transport (middle of right column)
        //   • Phase (bottom, guided) OR Scene + Arrangement (bottom, free)
        //
        // Canvas size: 1088 × 450 (guided) or 1088 × 470 (free).
        // Registered world bottom-left is chosen so the Transport section's bottom edge sits at
        // exactly eye-0.22 (matching its legacy stand-alone location). Dock extends down with
        // the canvas bottom; Genre sits above Transport.

        private MergedTransportBuild BuildMergedTransportPanel(Transform head)
        {
            const float width       = TransportPanel.MergedWidth;     // 1088
            const float initialH    = TransportPanel.GuidedTotalH;    // 450 (guided default)
            const float dockW       = TransportPanel.DockSectionW;    // 340
            const float gutter      = TransportPanel.DockGutter;      // 8
            const float rightX      = dockW + gutter;                 // 348 — left edge of right column
            const float rightW      = TransportPanel.RightColumnW;    // 740
            const float genreH      = TransportPanel.GenreSectionH;   // 200
            const float transportH  = TransportPanel.TransportSectionH; // 100
            const float phaseH      = TransportPanel.PhaseSectionH;   // 150
            const float sceneH      = TransportPanel.SceneSectionH;   // 70
            const float arrangeH    = TransportPanel.ArrangementSectionH; // 100

            // Canvas pivot is bottom-left. We want the Transport section (at canvas-local
            // y = initialH - genreH - transportH = 150, x = rightX = 348) to land at world
            // PositionInFront(0, -0.37, 1.1) — identical to its legacy position. So the canvas
            // bottom-left sits at PositionInFront(0 - 0.348, -0.37 - 0.150, 1.1).
            var canvas = UIFactory.CreateWorldCanvas("TransportPanel",
                transform, new Vector2(width, initialH),
                PositionInFront(-0.348f, -0.52f, 1.1f), 0.001f);
            RegisterPanel(canvas, -0.348f, -0.52f, 1.1f, PanelDragCoordinator.DragMembership.MainGroup);

            UIFactory.CreateBackground(canvas.transform,
                new Vector2(width, initialH), MaterialFactory.PanelBg);

            // ── Section containers (pivot bottom-left, positioned inside the canvas) ──
            // Dock: full canvas height on the LEFT.
            var dockRt = CreateSectionContainer("DockSection", canvas.transform,
                new Rect(0f, 0f, dockW, initialH));

            // Genre: top of right column.
            var genreRt = CreateSectionContainer("GenreSection", canvas.transform,
                new Rect(rightX, initialH - genreH, rightW, genreH));

            // Transport: directly below Genre, full right-column width.
            var transportRt = CreateSectionContainer("TransportSection", canvas.transform,
                new Rect(rightX, initialH - genreH - transportH, rightW, transportH));

            // Phase: 640-wide content centered in the 740-wide right column.
            float phaseContentW = 640f;
            float phaseOffsetX = rightX + (rightW - phaseContentW) * 0.5f;
            var phaseRt = CreateSectionContainer("PhaseSection", canvas.transform,
                new Rect(phaseOffsetX, 0f, phaseContentW, phaseH));

            // Scene strip: 540-wide centered in 740-wide right column.
            float sceneContentW = 540f;
            float sceneOffsetX = rightX + (rightW - sceneContentW) * 0.5f;
            var sceneRt = CreateSectionContainer("SceneSection", canvas.transform,
                new Rect(sceneOffsetX, arrangeH, sceneContentW, sceneH));
            sceneRt.gameObject.SetActive(false);

            // Arrangement: 560-wide centered in 740-wide right column.
            float arrangeContentW = 560f;
            float arrangeOffsetX = rightX + (rightW - arrangeContentW) * 0.5f;
            var arrangeRt = CreateSectionContainer("ArrangementSection", canvas.transform,
                new Rect(arrangeOffsetX, 0f, arrangeContentW, arrangeH));
            arrangeRt.gameObject.SetActive(false);

            // ── Decorative dividers ──
            // Vertical divider between Dock and right column — child of the canvas, spans full
            // canvas height via stretched anchors so it follows the canvas when it resizes.
            CreateFullHeightVerticalDivider(canvas.transform, "DockDivider",
                xOffset: dockW + gutter * 0.5f - 0.75f);

            // Horizontal divider at the top of the Transport section (bottom edge of Genre).
            // Parented to the Transport section so it moves with it when Transport shifts y.
            UIFactory.CreateImage(transportRt.transform, "GenreTransportDivider",
                new Color(1f, 1f, 1f, 0.08f),
                new Rect(0f, transportH - 1.5f, rightW, 1.5f));

            // Horizontal divider at the bottom of the Transport section (top edge of Phase / Scene).
            UIFactory.CreateImage(transportRt.transform, "TransportBottomDivider",
                new Color(1f, 1f, 1f, 0.08f),
                new Rect(0f, 0f, rightW, 1.5f));

            // ── Content ──
            var dockPanel     = BuildDockSectionContent(dockRt, head);
            var genrePanel    = BuildGenreSectionContent(genreRt);
            var transportRefs = BuildTransportSectionContent(transportRt.transform);
            var phasePanel    = BuildPhaseSectionContent(phaseRt);
            var scenePanel    = BuildSceneStripSectionContent(sceneRt);
            var arrangePanel  = BuildArrangementSectionContent(arrangeRt);

            // ── TransportPanel component wires everything together ──
            var panel = canvas.gameObject.AddComponent<TransportPanel>();
            panel.SetUIRefs(
                transportRefs.playBtn, transportRefs.playLabel,
                transportRefs.modeBtn, transportRefs.modeLabel,
                transportRefs.bpmText, transportRefs.keyText, transportRefs.statusText,
                transportRefs.paramsBtn, transportRefs.paramsLabel,
                transportRefs.viewBtn, transportRefs.viewLabel);
            panel.SetSectionRefs(
                canvas.GetComponent<RectTransform>(),
                dockRt, genreRt, transportRt, phaseRt, sceneRt, arrangeRt);

            return new MergedTransportBuild
            {
                transport     = panel,
                phase         = phasePanel,
                sceneStrip    = scenePanel,
                arrangement   = arrangePanel,
                dock          = dockPanel,
                genreSelector = genrePanel,
            };
        }

        /// <summary>
        /// Creates a thin vertical line image that spans the full height of its parent via
        /// stretched anchors, positioned at the given X pixel offset from the parent's left.
        /// </summary>
        private static void CreateFullHeightVerticalDivider(Transform parent, string name, float xOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.08f);
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            // Stretch vertically across the full parent height.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, 0f);
            rt.sizeDelta = new Vector2(1.5f, 0f);
        }

        private static RectTransform CreateSectionContainer(string name, Transform parent, Rect rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            UIFactory.SetAnchoredRect(rt, rect);
            return rt;
        }

        private struct TransportSectionRefs
        {
            public Button playBtn;      public Text playLabel;
            public Button modeBtn;      public Text modeLabel;
            public Button paramsBtn;    public Text paramsLabel;
            public Button viewBtn;      public Text viewLabel;
            public Text   bpmText;
            public Text   keyText;
            public Text   statusText;
        }

        private static TransportSectionRefs BuildTransportSectionContent(Transform section)
        {
            // Coordinates are relative to the section (pivot bottom-left, size 740×100) —
            // identical to the legacy stand-alone TransportPanel's 740×100 canvas, so no
            // button positions change when a panel-level move converts to a section-level move.
            var playBtn = UIFactory.CreateButton(section, "PlayStopButton", "Play",
                new Rect(8, 8, 100, 84), MaterialFactory.ButtonActive, Color.white, 20, null);
            var paramsBtn = UIFactory.CreateButton(section, "ToggleParamsButton", "Params\nOFF",
                new Rect(420, 8, 100, 84), new Color(0.28f, 0.32f, 0.42f), Color.white, 16, null);
            var modeBtn = UIFactory.CreateButton(section, "ModeButton", "Mode\nPercussion",
                new Rect(532, 8, 100, 84), TypeColors.Percussion, Color.white, 18, null);
            var viewBtn = UIFactory.CreateButton(section, "ViewModeButton", "View\nImmersed",
                new Rect(644, 8, 88, 84), new Color(0.18f, 0.22f, 0.32f, 1f), Color.white, 14, null);

            var bpmText = UIFactory.CreateRectText(section, "BpmText",
                "85 BPM", 18, Color.white, TextAnchor.MiddleLeft,
                new Rect(120, 58, 280, 34));
            var keyText = UIFactory.CreateRectText(section, "KeyText",
                "A minor", 16, new Color(0.7f, 0.9f, 1f), TextAnchor.MiddleLeft,
                new Rect(120, 28, 280, 28));
            var statusText = UIFactory.CreateRectText(section, "StatusText",
                "Idle", 14, new Color(0.55f, 0.55f, 0.65f), TextAnchor.MiddleLeft,
                new Rect(120, 6, 400, 22));

            return new TransportSectionRefs
            {
                playBtn    = playBtn, playLabel   = playBtn.GetComponentInChildren<Text>(),
                modeBtn    = modeBtn, modeLabel   = modeBtn.GetComponentInChildren<Text>(),
                paramsBtn  = paramsBtn, paramsLabel = paramsBtn.GetComponentInChildren<Text>(),
                viewBtn    = viewBtn, viewLabel   = viewBtn.GetComponentInChildren<Text>(),
                bpmText    = bpmText,
                keyText    = keyText,
                statusText = statusText,
            };
        }

        private static PhasePanel BuildPhaseSectionContent(RectTransform section)
        {
            // Section is 640×150, pivot bottom-left — identical to legacy PhasePanel canvas.
            var banner = UIFactory.CreateRectText(section.transform, "PhaseBanner",
                "Current phase: Harmony", 16, new Color(1f, 0.92f, 0.58f), TextAnchor.MiddleLeft,
                new Rect(12, 116, 320, 24));

            var playPieceButton = UIFactory.CreateButton(section.transform, "PlayPieceButton", "Play Piece",
                new Rect(494, 112, 134, 28),
                MaterialFactory.ButtonActive, Color.white, 14, null);

            var buttons = new List<Button>();
            var labels = new List<Text>();
            var clearButtons = new List<Button>();
            var phases = CompositionPhaseExtensions.All;
            float buttonWidth = 118f;
            float gap = 8f;
            float startX = 10f;

            for (int i = 0; i < phases.Length; i++)
            {
                float x = startX + i * (buttonWidth + gap);
                var button = UIFactory.CreateButton(section.transform, $"PhaseBtn_{phases[i]}",
                    $"{phases[i]}\nEmpty",
                    new Rect(x, 42, buttonWidth, 60),
                    new Color(0.25f, 0.25f, 0.3f, 1f), Color.white, 14, null);
                var clearButton = UIFactory.CreateButton(section.transform, $"PhaseClear_{phases[i]}",
                    "Clear",
                    new Rect(x, 10, buttonWidth, 24),
                    MaterialFactory.ButtonDanger, Color.white, 11, null);
                buttons.Add(button);
                labels.Add(button.GetComponentInChildren<Text>());
                clearButtons.Add(clearButton);
            }

            var panel = section.gameObject.AddComponent<PhasePanel>();
            panel.SetUIRefs(banner, buttons, labels, clearButtons,
                playPieceButton, playPieceButton.GetComponentInChildren<Text>());
            return panel;
        }

        private static SceneStripPanel BuildSceneStripSectionContent(RectTransform section)
        {
            // Section is 540×70, pivot bottom-left — identical to legacy SceneStripPanel canvas.
            var sceneNames = new[] { "Scene A", "Scene B", "Scene C", "Scene D" };
            var buttons    = new List<Button>();
            var labels     = new List<Text>();
            float bw = 128f, bh = 52f, gap = 6f, startX = 8f, cy = 9f;

            for (int i = 0; i < 4; i++)
            {
                float bx = startX + i * (bw + gap);
                var btn = UIFactory.CreateButton(section.transform, $"SceneBtn_{i}",
                    sceneNames[i], new Rect(bx, cy, bw, bh),
                    MaterialFactory.ButtonDefault, Color.white, 16, null);
                buttons.Add(btn);
                labels.Add(btn.GetComponentInChildren<Text>());
            }

            var panel = section.gameObject.AddComponent<SceneStripPanel>();
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
                "Percussion", 14, new Color(0.7f, 1f, 1f), TextAnchor.MiddleLeft,
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
                "Percussion", 14, new Color(0.7f, 1f, 1f), TextAnchor.MiddleLeft, new Rect(10, 348, 200, 20));
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

        // ──────── Dock section (left column of the merged Transport panel) ────────

        private static DockPanel BuildDockSectionContent(RectTransform section, Transform head)
        {
            // The section's size is dynamic (canvas height changes with guided/free mode). Tabs
            // top-anchor to the section so they stay at the top. The three tab sub-panels
            // stretch-fill the area below the tabs so they auto-resize with the section.

            const float tabRowHeight   = 24f;
            const float tabRowTopPad   = 4f;       // gap from section top to tab row top
            const float subPanelTopIns = 32f;      // top inset = tabRowTopPad + tabRowHeight + 4 gap
            const float subPanelBotIns = 4f;       // bottom inset for the tab sub-panels

            // Tab buttons (top row) — top-anchored so they stay at the section top on resize.
            var instrTab = UIFactory.CreateButton(section.transform, "InstrumentsTab", "Instruments",
                new Rect(4, 0, 108, tabRowHeight), MaterialFactory.ButtonActive, Color.white, 13, null);
            var patternsTab = UIFactory.CreateButton(section.transform, "PatternsTab", "Patterns",
                new Rect(116, 0, 100, tabRowHeight), MaterialFactory.ButtonDefault, Color.white, 13, null);
            var scenesTab = UIFactory.CreateButton(section.transform, "ScenesTab", "Scenes",
                new Rect(220, 0, 116, tabRowHeight), MaterialFactory.ButtonDefault, Color.white, 13, null);
            TopAnchorRect(instrTab.GetComponent<RectTransform>(),    xFromLeft: 4f,   yFromTop: tabRowTopPad, width: 108f, height: tabRowHeight);
            TopAnchorRect(patternsTab.GetComponent<RectTransform>(), xFromLeft: 116f, yFromTop: tabRowTopPad, width: 100f, height: tabRowHeight);
            TopAnchorRect(scenesTab.GetComponent<RectTransform>(),   xFromLeft: 220f, yFromTop: tabRowTopPad, width: 116f, height: tabRowHeight);

            // ── Sub-panels: each stretches to fill the section below the tab row. ─────
            var instrPanelGo   = CreateStretchedSubPanel(section.transform, "InstrumentsPanel", subPanelTopIns, subPanelBotIns, activeInitially: true);
            var patternPanelGo = CreateStretchedSubPanel(section.transform, "PatternsPanel",    subPanelTopIns, subPanelBotIns, activeInitially: false);
            var scenePanelGo   = CreateStretchedSubPanel(section.transform, "ScenesPanel",      subPanelTopIns, subPanelBotIns, activeInitially: false);

            // ── Instruments sub-panel: 5 phase cards stacked directly (no scroll view).
            // 5 cards × 72 px + 4 × 4 gap + 8 padding ≈ 384 px → fills the ~416 px sub-panel.
            var phaseCards = BuildPhaseCardList(instrPanelGo.transform);

            // ── Patterns sub-panel: reserved, shows placeholder text ─────────────────────
            UIFactory.CreateRectText(patternPanelGo.transform, "PatternsPlaceholder",
                "Patterns — coming soon", 13, new Color(0.45f, 0.48f, 0.55f), TextAnchor.MiddleCenter,
                new Rect(0, 0, 320, 40));

            var panel = section.gameObject.AddComponent<DockPanel>();
            panel.SetUIRefs(
                instrTab, patternsTab, scenesTab,
                instrPanelGo, patternPanelGo, scenePanelGo,
                phaseCards, head);
            return panel;
        }

        // ── Phase card list builder ──────────────────────────────────────────────────────

        /// <summary>
        /// Creates 5 phase cards inside <paramref name="container"/> (one per CompositionPhase).
        /// Each card: background Image + 4-px left accent bar + phase label + details line + summary line.
        /// Cards are stacked top-to-bottom with a fixed height of 72 px and 4 px gap.
        /// Returns a list of PhaseCardUI structs for DockPanel to update at runtime.
        /// </summary>
        private static List<DockPanel.PhaseCardUI> BuildPhaseCardList(Transform container)
        {
            const float cardH   = 72f;
            const float cardGap = 4f;
            const float cardW   = 320f;
            const float accentW = 4f;
            const float padL    = 10f;  // text left padding (after accent bar)

            var cards = new List<DockPanel.PhaseCardUI>();
            var phases = CompositionPhaseExtensions.All;

            // Vertical stack on the container so cards auto-lay out top-down.
            var vlg = container.gameObject.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg == null) vlg = container.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.spacing                = cardGap;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.padding                = new RectOffset(4, 4, 4, 4);

            for (int i = 0; i < phases.Length; i++)
            {
                // Card root
                var cardGo = new GameObject($"PhaseCard_{phases[i]}");
                cardGo.transform.SetParent(container, false);
                var cardRt = cardGo.AddComponent<RectTransform>();
                cardRt.sizeDelta = new Vector2(cardW, cardH);

                // Tell the VerticalLayoutGroup the preferred height for this card.
                var le = cardGo.AddComponent<UnityEngine.UI.LayoutElement>();
                le.preferredHeight = cardH;
                le.minHeight       = cardH;

                // Background — slightly lighter than the dock panel bg so cards stand out.
                var bg = cardGo.AddComponent<Image>();
                bg.color = new Color(0.17f, 0.19f, 0.25f, 1f);

                // Left accent bar (4 px wide, full card height, left-anchored)
                var accentGo = new GameObject("AccentBar");
                accentGo.transform.SetParent(cardGo.transform, false);
                var accentRt  = accentGo.AddComponent<RectTransform>();
                accentRt.anchorMin = new Vector2(0f, 0f);
                accentRt.anchorMax = new Vector2(0f, 1f);
                accentRt.pivot     = new Vector2(0f, 0.5f);
                accentRt.offsetMin = Vector2.zero;
                accentRt.offsetMax = new Vector2(accentW, 0f);
                var accentImg = accentGo.AddComponent<Image>();
                accentImg.color = Color.gray;

                // Phase label — top-left
                var phaseLbl = UIFactory.CreateRectText(cardGo.transform, "PhaseLabel",
                    "● PHASE", 13, new Color(0.90f, 0.93f, 1f), TextAnchor.UpperLeft,
                    new Rect(accentW + padL, 0, cardW - accentW - padL - 4f, 20f));
                TopAnchorRect(phaseLbl.GetComponent<RectTransform>(),
                    xFromLeft: accentW + padL, yFromTop: 5f,
                    width: cardW - accentW - padL - 4f, height: 18f);

                // Details line (preset · key · bars · tempo)
                var detailsLbl = UIFactory.CreateRectText(cardGo.transform, "DetailsText",
                    "Not yet composed", 11, new Color(0.65f, 0.68f, 0.75f), TextAnchor.UpperLeft,
                    new Rect(accentW + padL, 0, cardW - accentW - padL - 4f, 18f));
                TopAnchorRect(detailsLbl.GetComponent<RectTransform>(),
                    xFromLeft: accentW + padL, yFromTop: 25f,
                    width: cardW - accentW - padL - 4f, height: 18f);

                // Summary line
                var summaryLbl = UIFactory.CreateRectText(cardGo.transform, "SummaryText",
                    string.Empty, 10, new Color(0.55f, 0.58f, 0.65f), TextAnchor.UpperLeft,
                    new Rect(accentW + padL, 0, cardW - accentW - padL - 4f, 22f));
                TopAnchorRect(summaryLbl.GetComponent<RectTransform>(),
                    xFromLeft: accentW + padL, yFromTop: 45f,
                    width: cardW - accentW - padL - 4f, height: 23f);
                summaryLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                summaryLbl.verticalOverflow   = VerticalWrapMode.Truncate;

                cards.Add(new DockPanel.PhaseCardUI
                {
                    background  = bg,
                    accentBar   = accentImg,
                    phaseLabel  = phaseLbl,
                    detailsText = detailsLbl,
                    summaryText = summaryLbl,
                });
            }

            return cards;
        }

        /// <summary>
        /// Positions a RectTransform so that (xFromLeft, yFromTop) is its top-left corner and
        /// (width, height) is its size, with top-left anchor so it follows the parent's top
        /// edge when the parent resizes.
        /// </summary>
        private static void TopAnchorRect(RectTransform rt, float xFromLeft, float yFromTop, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(xFromLeft, -yFromTop);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Stretches a RectTransform to fill its parent completely.
        /// </summary>
        private static void StretchToParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Creates a GameObject that stretch-fills the parent horizontally and fills the vertical
        /// space between y=bottomInset and y=(parentTop - topInset). Used for Dock tab sub-panels.
        /// </summary>
        private static GameObject CreateStretchedSubPanel(Transform parent, string name, float topInset, float bottomInset, bool activeInitially)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, bottomInset);
            rt.offsetMax = new Vector2(0f, -topInset);
            go.SetActive(activeInitially);
            return go;
        }

        // ──────── Arrangement section (free-mode child of the merged Transport panel) ────────

        private static ArrangementPanel BuildArrangementSectionContent(RectTransform section)
        {
            // Section is 560×100, pivot bottom-left — identical to legacy ArrangementPanel canvas.
            var slotUIs = new List<ArrangementPanel.SlotUI>();
            float sw = 62f, gap = 4f;

            for (int i = 0; i < 8; i++)
            {
                float sx = 6f + i * (sw + gap);

                var slotLabel = UIFactory.CreateRectText(section.transform, $"Slot{i + 1}Label",
                    $"Slot {i + 1}", 11, new Color(0.5f, 0.6f, 0.7f), TextAnchor.MiddleCenter,
                    new Rect(sx, 78, sw, 18));

                var sceneBtn = UIFactory.CreateButton(section.transform, $"SceneBtn{i}", "--",
                    new Rect(sx, 46, sw, 28), MaterialFactory.ButtonDefault, Color.white, 11, null);
                var sceneLabel = sceneBtn.GetComponentInChildren<Text>();

                var barsBtn = UIFactory.CreateButton(section.transform, $"BarsBtn{i}", "4",
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

            var panel = section.gameObject.AddComponent<ArrangementPanel>();
            SetPrivateField(panel, "_slots", slotUIs);
            return panel;
        }

        // ──────── Genre section (top of the right column inside the merged Transport panel) ────────

        private static GenreSelectorPanel BuildGenreSectionContent(RectTransform section)
        {
            // Section is 740 × 200 (pivot bottom-left). Layout:
            //   • "GENRE" title across the top
            //   • 3 genre buttons + sub-labels, horizontally centered in the 740-wide area
            //   • Description label spanning the remaining vertical space
            //   • Info line at the bottom

            const float sectionW = TransportPanel.RightColumnW; // 740

            UIFactory.CreateRectText(section.transform, "GenreTitle",
                "GENRE", 14, new Color(0.5f, 0.6f, 0.8f), TextAnchor.MiddleCenter,
                new Rect(0, 172, sectionW, 22));

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

            // 3 buttons of width 114 with 8 px gaps → total content width = 350 px.
            // Centered in a 740-wide section → startX = (740 - 350) / 2 = 195.
            float bw = 114f, bh = 56f, gap = 8f, by = 96f;
            float contentW = 3f * bw + 2f * gap;
            float startX = (sectionW - contentW) * 0.5f;

            for (int i = 0; i < genres.Count; i++)
            {
                float bx = startX + i * (bw + gap);
                var btn = UIFactory.CreateButton(section.transform, $"GenreBtn_{genres[i]}",
                    labels[i],
                    new Rect(bx, by, bw, bh),
                    new Color(0.18f, 0.18f, 0.24f, 1f), Color.white, 15, null);
                buttons.Add(btn);
                btnTexts.Add(btn.GetComponentInChildren<Text>());
            }

            // Description label — stretches nearly the full section width.
            var descLabel = UIFactory.CreateRectText(section.transform, "GenreDescription",
                "Lo-Fi, Trap & Dream synthesis", 12,
                new Color(0.6f, 0.7f, 0.85f), TextAnchor.MiddleCenter,
                new Rect(8, 30, sectionW - 16f, 34));

            // Sub-labels for each genre (just below the buttons).
            float lby = 74f;
            for (int i = 0; i < genres.Count; i++)
            {
                float lbx = startX + i * (bw + gap);
                UIFactory.CreateRectText(section.transform, $"GenreDesc_{genres[i]}",
                    descLines[i], 10, new Color(0.45f, 0.5f, 0.6f), TextAnchor.UpperCenter,
                    new Rect(lbx, lby, bw, 20));
            }

            // Info line at bottom.
            UIFactory.CreateRectText(section.transform, "GenreInfo",
                "Switching genre re-derives all patterns", 11,
                new Color(0.38f, 0.4f, 0.5f), TextAnchor.MiddleCenter,
                new Rect(8, 8, sectionW - 16f, 18));

            var panel = section.gameObject.AddComponent<GenreSelectorPanel>();
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
