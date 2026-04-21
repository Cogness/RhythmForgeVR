using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Bootstrap;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.Core.PatternBehavior;
using RhythmForge.Audio;
using RhythmForge.Interaction;
using RhythmForge.UI;
using RhythmForge.UI.Panels;

namespace RhythmForge
{
    /// <summary>
    /// Subsystem references passed to RhythmForgeManager.Configure.
    /// </summary>
    public struct ManagerSubsystems
    {
        public AudioEngine audioEngine;
        public SamplePlayer samplePlayer;
        public Sequencer.Sequencer sequencer;
        public StrokeCapture strokeCapture;
        public DrawModeController drawMode;
        public PhaseController phaseController;
        public InputMapper inputMapper;
        public InstanceGrabber instanceGrabber;
    }

    /// <summary>
    /// Panel references passed to RhythmForgeManager.Configure.
    /// </summary>
    public struct ManagerPanels
    {
        public CommitCardPanel   commitCard;
        public InspectorPanel    inspector;
        public DockPanel         dock;
        public TransportPanel    transport;
        public PhasePanel        phase;
        public SceneStripPanel   sceneStrip;
        public ArrangementPanel  arrangement;
        public ToastMessage      toast;
        public GenreSelectorPanel genreSelector;
    }

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
        [SerializeField] private PhaseController _phaseController;
        [SerializeField] private InputMapper _inputMapper;
        [SerializeField] private InstanceGrabber _instanceGrabber;

        [Header("UI Panels")]
        [SerializeField] private CommitCardPanel  _commitCard;
        [SerializeField] private InspectorPanel   _inspectorPanel;
        [SerializeField] private DockPanel        _dockPanel;
        [SerializeField] private TransportPanel   _transportPanel;
        [SerializeField] private PhasePanel       _phasePanel;
        [SerializeField] private SceneStripPanel  _sceneStripPanel;
        [SerializeField] private ArrangementPanel _arrangementPanel;
        [SerializeField] private ToastMessage     _toast;
        [SerializeField] private GenreSelectorPanel _genreSelectorPanel;

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
        private readonly Dictionary<PatternType, Material> _materialCache = new Dictionary<PatternType, Material>();
        private SamplePlayer _samplePlayer;

        /// <summary>Called by RhythmForgeBootstrapper to inject all subsystem and UI references.</summary>
        public void Configure(
            ManagerSubsystems subsystems,
            ManagerPanels panels,
            Transform instanceContainer,
            Transform userHead)
        {
            _audioEngine        = subsystems.audioEngine;
            _samplePlayer       = subsystems.samplePlayer;
            _sequencer          = subsystems.sequencer;
            _strokeCapture      = subsystems.strokeCapture;
            _drawModeController = subsystems.drawMode;
            _phaseController    = subsystems.phaseController;
            _inputMapper        = subsystems.inputMapper;
            _inputProvider      = subsystems.inputMapper;
            _instanceGrabber    = subsystems.instanceGrabber;
            _commitCard         = panels.commitCard;
            _inspectorPanel     = panels.inspector;
            _dockPanel          = panels.dock;
            _transportPanel     = panels.transport;
            _phasePanel         = panels.phase;
            _sceneStripPanel    = panels.sceneStrip;
            _arrangementPanel     = panels.arrangement;
            _toast                = panels.toast;
            _genreSelectorPanel   = panels.genreSelector;
            _instanceContainer  = instanceContainer;
            _userHead           = userHead;
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
            if (_phaseController)
                _phaseController.Initialize(_store, _drawModeController);
            SyncInteractionModeFromStore();

            if (_sequencer)
                _sequencer.Initialize(_store);
            if (_store != null)
                _store.OnGenreRederived += HandleGenreRederived;
            if (_strokeCapture)
                _strokeCapture.Initialize(_store);
            if (_instanceGrabber)
                _instanceGrabber.Initialize(_store);

            if (_commitCard)
                _commitCard.Initialize(_strokeCapture, _store, _phaseController);
            if (_inspectorPanel)
                _inspectorPanel.Initialize(_store);
            if (_dockPanel)
                _dockPanel.Initialize(_store, _drawModeController);
            if (_transportPanel)
            {
                _transportPanel.Initialize(_store, _sequencer, _drawModeController);
                _showParamLabels = _transportPanel.ShowParams;
            }
            if (_phasePanel)
                _phasePanel.Initialize(_store, _phaseController, _sequencer);
            if (_sceneStripPanel)
                _sceneStripPanel.Initialize(_store, _sequencer);
            if (_arrangementPanel)
                _arrangementPanel.Initialize(_store, _sequencer);
            if (_genreSelectorPanel)
                _genreSelectorPanel.Initialize(_store);

            _visualizerManager = new VisualizerManager(
                _store,
                _sequencer,
                _instanceContainer,
                _userHead,
                GetMaterialForType);

            SubscribeToEventBus();
            ApplyGuidedModeUiState();

            _visualizerManager.RebuildInstanceVisuals(_showParamLabels);
        }

        public void SetSamplePlayer(SamplePlayer samplePlayer)
        {
            _samplePlayer = samplePlayer;
        }

        private void HandleGenreRederived(string genreId)
        {
            _samplePlayer?.InvalidateAll();
            _sequencer?.ResetWarmBar();
        }

        private void OnDestroy()
        {
            if (_store != null)
                _store.OnGenreRederived -= HandleGenreRederived;
            UnsubscribeFromEventBus();

            _visualizerManager?.Dispose();
        }

        private void Update()
        {
            _store?.Tick();
            _visualizerManager?.UpdatePlaybackVisuals();
            HandleSceneAndTransportInput();
            _autosaveController?.Tick(Time.deltaTime, _store?.State);
        }

        private void OnStateChanged()
        {
            ApplyGuidedModeUiState();
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
            _eventBus.Subscribe<GenreChangedEvent>(HandleGenreChanged);
            _eventBus.Subscribe<MelodyCommittedEvent>(HandleMelodyCommitted);
            _eventBus.Subscribe<GrooveCommittedEvent>(HandleGrooveCommitted);
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
            _eventBus.Unsubscribe<GenreChangedEvent>(HandleGenreChanged);
            _eventBus.Unsubscribe<MelodyCommittedEvent>(HandleMelodyCommitted);
            _eventBus.Unsubscribe<GrooveCommittedEvent>(HandleGrooveCommitted);
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

        private void HandleGenreChanged(GenreChangedEvent evt)
        {
            var genre = GenreRegistry.Get(evt.NewGenreId);
            _audioEngine?.SetGenre(evt.NewGenreId);
            if (_toast && genre != null)
                _toast.Show($"Genre: {genre.DisplayName}");
            // Rebuild visuals — colors have changed
            _visualizerManager?.RebuildInstanceVisuals(_showParamLabels);
        }

        private void HandleMelodyCommitted(MelodyCommittedEvent evt)
        {
            if (_store?.GetComposition()?.groove == null)
                return;

            _samplePlayer?.RefreshPendingWork();
            _sequencer?.ResetWarmBar();
        }

        private void HandleGrooveCommitted(GrooveCommittedEvent evt)
        {
            _samplePlayer?.RefreshPendingWork();
            _sequencer?.ResetWarmBar();

            if (string.IsNullOrEmpty(evt.PatternId))
                return;

            if (_store?.GetComposition()?.GetPatternId(CompositionPhase.Melody) == null && _toast)
                _toast.Show("Groove saved. Add Melody to hear the groove effect.");
        }

        private void HandleSceneAndTransportInput()
        {
            var input = _inputProvider ?? (IInputProvider)_inputMapper;
            if (input == null)
                return;

            bool guidedMode = _store != null && _store.State.guidedMode;
            if (!guidedMode)
            {
                Vector2 stick = input.LeftThumbstick;
                if (stick.x < -0.7f)
                    SwitchSceneRelative(-1);
                else if (stick.x > 0.7f)
                    SwitchSceneRelative(1);
            }

            if (input.ButtonTwo && _sequencer != null)
                _sequencer.TogglePlayback();
        }

        private Material GetMaterialForType(PatternType type)
        {
            if (!_materialCache.TryGetValue(type, out var mat))
            {
                mat = MaterialFactory.CreateStrokeMaterial(type);
                _materialCache[type] = mat;
            }
            return mat;
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
            var demo = GuidedDemoComposition.CreateDemoState(_store);
            _store.LoadState(demo);
            SyncInteractionModeFromStore();
            ApplyGuidedModeUiState();
            if (_toast)
                _toast.Show("Guided composition ready.");
        }

        public void LoadFreshGuidedSession()
        {
            _store.LoadState(GuidedDemoComposition.CreateDemoState(_store));
            SyncInteractionModeFromStore();
            ApplyGuidedModeUiState();
            _autosaveController?.ResetTimer();
            if (_toast)
                _toast.Show("Fresh guided session ready.");
        }

        public void ResetSession()
        {
            _store.Reset();
            SyncInteractionModeFromStore();
            ApplyGuidedModeUiState();
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

        private void SyncInteractionModeFromStore()
        {
            if (_store == null || _drawModeController == null)
                return;

            if (_store.State.guidedMode && _phaseController != null)
            {
                _store.SetDrawMode(_store.GetCurrentPhase().ToPatternType());
                _phaseController.SyncFromStore(true);
            }
            else
            {
                _drawModeController.SetMode(_store.GetDrawMode());
            }
        }

        private void ApplyGuidedModeUiState()
        {
            if (_store == null)
                return;

            bool guidedMode = _store.State.guidedMode;
            SetPanelVisibility(_phasePanel, guidedMode);
            SetPanelVisibility(_sceneStripPanel, !guidedMode);
            SetPanelVisibility(_arrangementPanel, !guidedMode);
            _transportPanel?.SetGuidedMode(guidedMode);
            _dockPanel?.SetGuidedMode(guidedMode);
        }

        private static void SetPanelVisibility(MonoBehaviour panel, bool visible)
        {
            if (panel == null)
                return;

            if (panel.gameObject.activeSelf != visible)
                panel.gameObject.SetActive(visible);

            var canvas = panel.GetComponent<Canvas>();
            if (canvas != null)
                canvas.enabled = visible;
        }
    }
}
