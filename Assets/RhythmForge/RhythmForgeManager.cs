using System.Collections.Generic;
using UnityEngine;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;
using RhythmForge.Audio;
using RhythmForge.Interaction;
using RhythmForge.UI;
using RhythmForge.UI.Panels;

namespace RhythmForge
{
    /// <summary>
    /// Top-level manager that owns all subsystems, wires events,
    /// and manages the lifecycle of pattern instance visuals.
    /// Attach to a single root GameObject in the scene.
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
            _audioEngine        = audioEngine;
            _sequencer          = sequencer;
            _strokeCapture      = strokeCapture;
            _drawModeController = drawModeController;
            _inputMapper        = inputMapper;
            _instanceGrabber    = instanceGrabber;
            _commitCard         = commitCard;
            _inspectorPanel     = inspectorPanel;
            _dockPanel          = dockPanel;
            _transportPanel     = transportPanel;
            _sceneStripPanel    = sceneStripPanel;
            _arrangementPanel   = arrangementPanel;
            _toast              = toast;
            _rhythmMaterial     = rhythmMaterial;
            _melodyMaterial     = melodyMaterial;
            _harmonyMaterial    = harmonyMaterial;
            _instanceContainer  = instanceContainer;
            _userHead           = userHead;
        }

        // Core state
        private SessionStore _store;
        private Dictionary<string, PatternVisualizer> _visualizers = new Dictionary<string, PatternVisualizer>();

        private void Awake()
        {
            _store = new SessionStore();

            // Try loading saved session
            var saved = SessionPersistence.Load();
            if (saved != null)
                _store.LoadState(saved);
        }

        private bool _initialized;

        private void Start()
        {
            // Skip if already called by bootstrapper
            if (!_initialized) InitializeSubsystems();
        }

        /// <summary>Called by RhythmForgeBootstrapper to initialize before LoadDemoSession.</summary>
        public void InitializeSubsystems()
        {
            if (_initialized) return;
            _initialized = true;

            SyncDrawModeFromStore();

            // Initialize subsystems
            if (_sequencer) _sequencer.Initialize(_store);
            if (_strokeCapture) _strokeCapture.Initialize(_store);
            if (_instanceGrabber) _instanceGrabber.Initialize(_store);

            // Initialize UI panels
            if (_commitCard) _commitCard.Initialize(_strokeCapture);
            if (_inspectorPanel) _inspectorPanel.Initialize(_store);
            if (_dockPanel) _dockPanel.Initialize(_store, _drawModeController);
            if (_transportPanel) _transportPanel.Initialize(_store, _sequencer, _drawModeController);
            if (_sceneStripPanel) _sceneStripPanel.Initialize(_store, _sequencer);
            if (_arrangementPanel) _arrangementPanel.Initialize(_store, _sequencer);

            // Wire events
            _store.OnStateChanged += OnStateChanged;

            if (_strokeCapture)
            {
                _strokeCapture.OnDraftCreated += OnDraftCreated;
                _strokeCapture.OnDraftDiscarded += OnDraftDiscarded;
            }

            if (_sequencer)
                _sequencer.OnTransportChanged += OnTransportChanged;
            if (_drawModeController != null)
                _drawModeController.OnModeChanged += OnDrawModeChanged;

            // Initial visual build
            RebuildInstanceVisuals();
        }

        private void OnDestroy()
        {
            if (_store != null) _store.OnStateChanged -= OnStateChanged;
            if (_strokeCapture != null)
            {
                _strokeCapture.OnDraftCreated -= OnDraftCreated;
                _strokeCapture.OnDraftDiscarded -= OnDraftDiscarded;
            }
            if (_sequencer != null)
                _sequencer.OnTransportChanged -= OnTransportChanged;
            if (_drawModeController != null)
                _drawModeController.OnModeChanged -= OnDrawModeChanged;
        }

        private void Update()
        {
            // Update pulse on all visualizers during playback
            if (_sequencer != null && _sequencer.IsPlaying)
            {
                foreach (var kvp in _visualizers)
                {
                    float pulse = _sequencer.GetPulse(kvp.Key);
                    kvp.Value.SetPulse(pulse);
                }
            }

            // Left controller thumbstick L/R to switch scenes
            if (_inputMapper != null)
            {
                Vector2 stick = _inputMapper.LeftThumbstick;
                if (stick.x < -0.7f)
                    SwitchSceneRelative(-1);
                else if (stick.x > 0.7f)
                    SwitchSceneRelative(1);

                // Y button to toggle play
                if (_inputMapper.ButtonTwo && _sequencer != null)
                    _sequencer.TogglePlayback();
            }

            // Auto-save periodically (every 30s)
            _saveTimer += Time.deltaTime;
            if (_saveTimer > 30f)
            {
                _saveTimer = 0f;
                SessionPersistence.Save(_store.State);
            }
        }

        private float _saveTimer;
        private float _sceneSwapCooldown;

        // --- Event handlers ---

        private void OnStateChanged()
        {
            RebuildInstanceVisuals();
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
            // Could trigger visual updates, haptics, etc.
        }

        private void OnDrawModeChanged(PatternType mode)
        {
            if (_store != null)
                _store.SetDrawMode(mode);
        }

        // --- Instance visual management ---

        private void RebuildInstanceVisuals()
        {
            if (_store == null) return;

            string activeSceneId = _store.State.activeSceneId;
            var sceneInstances = _store.GetSceneInstances(activeSceneId);
            var activeIds = new HashSet<string>();

            foreach (var instance in sceneInstances)
            {
                activeIds.Add(instance.id);
                var pattern = _store.GetPattern(instance.patternId);
                if (pattern == null) continue;

                if (_visualizers.TryGetValue(instance.id, out var existing))
                {
                    existing.RefreshGeometry(pattern, instance, GetMaterialForType(pattern.type), _userHead);
                    existing.SetMuted(instance.muted);
                    existing.SetSelected(instance.id == _store.State.selectedInstanceId);
                }
                else
                {
                    // Create new visualizer
                    var go = new GameObject($"Instance_{instance.id}");
                    if (_instanceContainer) go.transform.SetParent(_instanceContainer);

                    var vis = go.AddComponent<PatternVisualizer>();
                    Material mat = GetMaterialForType(pattern.type);
                    vis.Initialize(pattern, instance, mat, _userHead);
                    vis.SetMuted(instance.muted);
                    vis.SetSelected(instance.id == _store.State.selectedInstanceId);

                    // Add a collider for raycasting
                    var col = go.AddComponent<SphereCollider>();
                    col.radius = 0.08f;
                    col.isTrigger = true;

                    _visualizers[instance.id] = vis;
                }
            }

            // Remove visualizers no longer in the active scene
            var toRemove = new List<string>();
            foreach (var kvp in _visualizers)
            {
                if (!activeIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
            {
                if (_visualizers.TryGetValue(id, out var vis))
                {
                    Destroy(vis.gameObject);
                    _visualizers.Remove(id);
                }
            }
        }

        private Material GetMaterialForType(PatternType type)
        {
            switch (type)
            {
                case PatternType.RhythmLoop: return _rhythmMaterial;
                case PatternType.MelodyLine: return _melodyMaterial;
                case PatternType.HarmonyPad: return _harmonyMaterial;
                default: return _rhythmMaterial;
            }
        }

        private void SwitchSceneRelative(int direction)
        {
            _sceneSwapCooldown -= Time.deltaTime;
            if (_sceneSwapCooldown > 0f) return;
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

        // --- Public API ---

        public void LoadDemoSession()
        {
            var demo = DemoSession.CreateDemoState(_store);
            _store.LoadState(demo);
            SyncDrawModeFromStore();
            if (_toast) _toast.Show("Demo session loaded.");
        }

        public void ResetSession()
        {
            _store.Reset();
            SyncDrawModeFromStore();
            SessionPersistence.Delete();
            if (_toast) _toast.Show("Session reset.");
        }

        public void SaveSession()
        {
            SessionPersistence.Save(_store.State);
            if (_toast) _toast.Show("Session saved.");
        }

        private void SyncDrawModeFromStore()
        {
            if (_store != null && _drawModeController != null)
                _drawModeController.SetMode(_store.GetDrawMode());
        }
    }
}
