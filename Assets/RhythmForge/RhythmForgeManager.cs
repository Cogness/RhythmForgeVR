using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.Audio;
using RhythmForge.Interaction;
using RhythmForge.UI;
using RhythmForge.UI.Panels;

namespace RhythmForge
{
    /// <summary>
    /// Top-level manager that owns all subsystems and wires lifecycle events between them.
    /// </summary>
    public class RhythmForgeManager : MonoBehaviour
    {
        [Header("Subsystem References (assign in Inspector)")]
        [SerializeField] private AudioEngine _audioEngine;
        [SerializeField] private Sequencer.Sequencer _sequencer;
        [SerializeField] private StrokeCapture _strokeCapture;
        [SerializeField] private DrawModeController _drawModeController;
        [SerializeField] private InputMapper _inputMapper;
        [SerializeField] private InstanceGrabber _instanceGrabber;

        [Header("UI Panels")]
        [SerializeField] private CommitCardPanel _commitCard;
        [SerializeField] private InspectorPanel _inspectorPanel;
        [SerializeField] private DockPanel _dockPanel;
        [SerializeField] private TransportPanel _transportPanel;
        [SerializeField] private SceneStripPanel _sceneStripPanel;
        [SerializeField] private ArrangementPanel _arrangementPanel;
        [SerializeField] private ToastMessage _toast;

        [Header("Visual Settings")]
        [SerializeField] private Material _rhythmMaterial;
        [SerializeField] private Material _melodyMaterial;
        [SerializeField] private Material _harmonyMaterial;

        [Header("Scene")]
        [SerializeField] private Transform _instanceContainer;
        [SerializeField] private Transform _userHead;

        private SessionStore _store;
        private RhythmForgeEventBus _eventBus;
        private VisualizerManager _visualizerManager;
        private AutosaveController _autosaveController;
        private IInputProvider _inputProvider;
        private bool _showParamLabels = true;
        private bool _initialized;
        private float _sceneSwapCooldown;

        /// <summary>Called by RhythmForgeBootstrapper to inject all subsystem and UI references.</summary>
        public void Configure(
            AudioEngine audioEngine,
            Sequencer.Sequencer sequencer,
            StrokeCapture strokeCapture,
            DrawModeController drawModeController,
            InputMapper inputMapper,
            InstanceGrabber instanceGrabber,
            CommitCardPanel commitCard,
            InspectorPanel inspectorPanel,
            DockPanel dockPanel,
            TransportPanel transportPanel,
            SceneStripPanel sceneStripPanel,
            ArrangementPanel arrangementPanel,
            ToastMessage toast,
            Material rhythmMaterial,
            Material melodyMaterial,
            Material harmonyMaterial,
            Transform instanceContainer,
            Transform userHead)
        {
            _audioEngine = audioEngine;
            _sequencer = sequencer;
            _strokeCapture = strokeCapture;
            _drawModeController = drawModeController;
            _inputMapper = inputMapper;
            _inputProvider = inputMapper;
            _instanceGrabber = instanceGrabber;
            _commitCard = commitCard;
            _inspectorPanel = inspectorPanel;
            _dockPanel = dockPanel;
            _transportPanel = transportPanel;
            _sceneStripPanel = sceneStripPanel;
            _arrangementPanel = arrangementPanel;
            _toast = toast;
            _rhythmMaterial = rhythmMaterial;
            _melodyMaterial = melodyMaterial;
            _harmonyMaterial = harmonyMaterial;
            _instanceContainer = instanceContainer;
            _userHead = userHead;
        }

        private void Awake()
        {
            _store = new SessionStore();
            _autosaveController = new AutosaveController();

            var saved = SessionPersistence.Load();
            if (saved != null)
                _store.LoadState(saved);
        }

        private void Start()
        {
            if (!_initialized)
                InitializeSubsystems();
        }

        /// <summary>Called by RhythmForgeBootstrapper to initialize before LoadDemoSession.</summary>
        public void InitializeSubsystems()
        {
            if (_initialized)
                return;

            _initialized = true;
            _eventBus = _store != null ? _store.EventBus : null;
            if (_drawModeController != null)
                _drawModeController.SetEventBus(_eventBus);
            SyncDrawModeFromStore();

            if (_sequencer)
                _sequencer.Initialize(_store);
            if (_strokeCapture)
                _strokeCapture.Initialize(_store);
            if (_instanceGrabber)
                _instanceGrabber.Initialize(_store);

            if (_commitCard)
                _commitCard.Initialize(_strokeCapture);
            if (_inspectorPanel)
                _inspectorPanel.Initialize(_store);
            if (_dockPanel)
                _dockPanel.Initialize(_store, _drawModeController);
            if (_transportPanel)
            {
                _transportPanel.Initialize(_store, _sequencer, _drawModeController);
                _showParamLabels = _transportPanel.ShowParams;
            }
            if (_sceneStripPanel)
                _sceneStripPanel.Initialize(_store, _sequencer);
            if (_arrangementPanel)
                _arrangementPanel.Initialize(_store, _sequencer);

            _visualizerManager = new VisualizerManager(
                _store,
                _sequencer,
                _instanceContainer,
                _userHead,
                GetMaterialForType);

            SubscribeToEventBus();

            _visualizerManager.RebuildInstanceVisuals(_showParamLabels);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEventBus();

            _visualizerManager?.Dispose();
        }

        private void Update()
        {
            _visualizerManager?.UpdatePlaybackVisuals();
            HandleSceneAndTransportInput();
            _autosaveController?.Tick(Time.deltaTime, _store?.State);
        }

        private void OnStateChanged()
        {
            _visualizerManager?.RebuildInstanceVisuals(_showParamLabels);
            _visualizerManager?.UpdatePlaybackVisuals();
        }

        private void OnDraftCreated(DraftResult draft)
        {
            if (_toast)
                _toast.Show($"Draft: {draft.name} ({draft.type})");
        }

        private void OnDraftDiscarded()
        {
            if (_toast)
                _toast.Show("Draft discarded.");
        }

        private void OnTransportChanged()
        {
            _visualizerManager?.RebuildInstanceVisuals(_showParamLabels);
            _visualizerManager?.UpdatePlaybackVisuals();
        }

        private void OnDrawModeChanged(PatternType mode)
        {
            if (_store != null)
                _store.SetDrawMode(mode);
        }

        private void OnParamsVisibilityChanged(bool visible)
        {
            _showParamLabels = visible;
            _visualizerManager?.SetParameterLabelVisible(visible);
        }

        private void SubscribeToEventBus()
        {
            if (_eventBus == null)
                return;

            _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Subscribe<DraftCreatedEvent>(HandleDraftCreated);
            _eventBus.Subscribe<DraftDiscardedEvent>(HandleDraftDiscarded);
            _eventBus.Subscribe<TransportChangedEvent>(HandleTransportChanged);
            _eventBus.Subscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
            _eventBus.Subscribe<ParameterLabelsVisibilityChangedEvent>(HandleParameterLabelsVisibilityChanged);
        }

        private void UnsubscribeFromEventBus()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionStateChanged);
            _eventBus.Unsubscribe<DraftCreatedEvent>(HandleDraftCreated);
            _eventBus.Unsubscribe<DraftDiscardedEvent>(HandleDraftDiscarded);
            _eventBus.Unsubscribe<TransportChangedEvent>(HandleTransportChanged);
            _eventBus.Unsubscribe<DrawModeChangedEvent>(HandleDrawModeChanged);
            _eventBus.Unsubscribe<ParameterLabelsVisibilityChangedEvent>(HandleParameterLabelsVisibilityChanged);
        }

        private void HandleSessionStateChanged(SessionStateChangedEvent evt)
        {
            OnStateChanged();
        }

        private void HandleDraftCreated(DraftCreatedEvent evt)
        {
            OnDraftCreated(evt.Draft);
        }

        private void HandleDraftDiscarded(DraftDiscardedEvent evt)
        {
            OnDraftDiscarded();
        }

        private void HandleTransportChanged(TransportChangedEvent evt)
        {
            OnTransportChanged();
        }

        private void HandleDrawModeChanged(DrawModeChangedEvent evt)
        {
            OnDrawModeChanged(evt.Mode);
        }

        private void HandleParameterLabelsVisibilityChanged(ParameterLabelsVisibilityChangedEvent evt)
        {
            OnParamsVisibilityChanged(evt.Visible);
        }

        private void HandleSceneAndTransportInput()
        {
            var input = _inputProvider ?? (IInputProvider)_inputMapper;
            if (input == null)
                return;

            Vector2 stick = input.LeftThumbstick;
            if (stick.x < -0.7f)
                SwitchSceneRelative(-1);
            else if (stick.x > 0.7f)
                SwitchSceneRelative(1);

            if (input.ButtonTwo && _sequencer != null)
                _sequencer.TogglePlayback();
        }

        private Material GetMaterialForType(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop:
                    return _rhythmMaterial;
                case PatternType.MelodyLine:
                    return _melodyMaterial;
                case PatternType.HarmonyPad:
                    return _harmonyMaterial;
                default:
                    return _rhythmMaterial;
            }
        }

        private void SwitchSceneRelative(int direction)
        {
            _sceneSwapCooldown -= Time.deltaTime;
            if (_sceneSwapCooldown > 0f || _store == null)
                return;

            _sceneSwapCooldown = 0.4f;

            string[] ids = { "scene-a", "scene-b", "scene-c", "scene-d" };
            int current = System.Array.IndexOf(ids, _store.State.activeSceneId);
            int next = (current + direction + ids.Length) % ids.Length;

            if (_sequencer != null && _sequencer.IsPlaying && !_sequencer.HasArrangement())
                _store.QueueScene(ids[next]);
            else
                _store.SetActiveScene(ids[next]);

            if (_toast)
                _toast.Show($"Scene: {_store.GetScene(ids[next])?.name ?? ids[next]}");
        }

        public void LoadDemoSession()
        {
            var demo = DemoSession.CreateDemoState(_store);
            _store.LoadState(demo);
            SyncDrawModeFromStore();
            if (_toast)
                _toast.Show("Demo session loaded.");
        }

        public void ResetSession()
        {
            _store.Reset();
            SyncDrawModeFromStore();
            _autosaveController?.ResetTimer();
            SessionPersistence.Delete();
            if (_toast)
                _toast.Show("Session reset.");
        }

        public void SaveSession()
        {
            _autosaveController?.SaveNow(_store?.State);
            if (_toast)
                _toast.Show("Session saved.");
        }

        private void SyncDrawModeFromStore()
        {
            if (_store != null && _drawModeController != null)
                _drawModeController.SetMode(_store.GetDrawMode());
        }
    }
}
