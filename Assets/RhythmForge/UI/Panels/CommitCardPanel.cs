using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;
using RhythmForge.Interaction;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel shown after a stroke is finished.
    /// Displays draft info and Save/Save+Dup/Discard buttons.
    /// </summary>
    public class CommitCardPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _summaryText;
        [SerializeField] private Text _detailsText;
        [SerializeField] private Text _typeLabel;
        [SerializeField] private Image _typeColorBar;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _saveDupButton;
        [SerializeField] private Button _discardButton;

        [Header("References")]
        [SerializeField] private Transform _lookAtTarget; // camera

        private StrokeCapture _strokeCapture;
        private SessionStore _store;
        private PhaseController _phaseController;
        private RhythmForgeEventBus _eventBus;
        private DraftResult _currentDraft;
        private bool _guidedAutoAdvance = true;

        /// <summary>Called by RhythmForgeBootstrapper to inject all UI element references.</summary>
        public void SetUIRefs(Text nameText, Text summaryText, Text detailsText,
            Text typeLabel, Image typeColorBar, Button saveBtn, Button saveDupBtn,
            Button discardBtn, Transform lookAt)
        {
            _nameText    = nameText;
            _summaryText = summaryText;
            _detailsText = detailsText;
            _typeLabel   = typeLabel;
            _typeColorBar = typeColorBar;
            _saveButton   = saveBtn;
            _saveDupButton = saveDupBtn;
            _discardButton = discardBtn;
            _lookAtTarget  = lookAt;
        }

        public void Initialize(StrokeCapture strokeCapture, SessionStore store, PhaseController phaseController)
        {
            _strokeCapture = strokeCapture;
            _store = store;
            _phaseController = phaseController;
            _eventBus = strokeCapture != null ? strokeCapture.EventBus : null;

            if (_eventBus != null)
            {
                _eventBus.Subscribe<DraftCreatedEvent>(HandleDraftCreated);
                _eventBus.Subscribe<DraftDiscardedEvent>(HandleDraftDiscarded);
            }
            else if (_strokeCapture != null)
            {
                _strokeCapture.OnDraftCreated += ShowDraft;
                _strokeCapture.OnDraftDiscarded += Hide;
            }

            if (_saveButton) _saveButton.onClick.AddListener(() => Confirm(false));
            if (_saveDupButton) _saveDupButton.onClick.AddListener(HandleSecondaryAction);
            if (_discardButton) _discardButton.onClick.AddListener(Discard);

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<DraftCreatedEvent>(HandleDraftCreated);
                _eventBus.Unsubscribe<DraftDiscardedEvent>(HandleDraftDiscarded);
            }
            else if (_strokeCapture != null)
            {
                _strokeCapture.OnDraftCreated -= ShowDraft;
                _strokeCapture.OnDraftDiscarded -= Hide;
            }
        }

        private void HandleDraftCreated(DraftCreatedEvent evt)
        {
            ShowDraft(evt.Draft);
        }

        private void HandleDraftDiscarded(DraftDiscardedEvent evt)
        {
            Hide();
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            // Face the user
            if (_lookAtTarget != null)
            {
                Vector3 dir = _lookAtTarget.position - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
            }
        }

        private void ShowDraft(DraftResult draft)
        {
            _currentDraft = draft;
            gameObject.SetActive(true);

            // Position near the stroke center
            transform.position = draft.spawnPosition + Vector3.up * 0.15f;

            if (_nameText) _nameText.text = draft.name;
            if (_summaryText) _summaryText.text = draft.summary;
            if (_detailsText) _detailsText.text = draft.details;
            if (_typeLabel) _typeLabel.text = draft.type.ToString();
            if (_typeColorBar) _typeColorBar.color = draft.color;
            RefreshActionButtons();
        }

        private void Confirm(bool duplicate)
        {
            bool guidedMode = IsGuidedMode();
            if (_strokeCapture != null && _currentDraft != null)
                _strokeCapture.ConfirmDraft(guidedMode ? false : duplicate);

            if (guidedMode && _guidedAutoAdvance && _currentDraft != null)
                _phaseController?.Next();

            _currentDraft = null;
            Hide();
        }

        private void HandleSecondaryAction()
        {
            if (IsGuidedMode())
            {
                _guidedAutoAdvance = !_guidedAutoAdvance;
                RefreshActionButtons();
                return;
            }

            Confirm(true);
        }

        private void Discard()
        {
            if (_strokeCapture != null)
                _strokeCapture.DiscardPending();

            _currentDraft = null;
            Hide();
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }

        private bool IsGuidedMode()
        {
            return _store != null && _store.State.guidedMode;
        }

        private void RefreshActionButtons()
        {
            if (_saveDupButton == null)
                return;

            var label = _saveDupButton.GetComponentInChildren<Text>();
            if (label == null)
                return;

            if (IsGuidedMode())
            {
                label.text = _guidedAutoAdvance ? "Auto Next\nON" : "Auto Next\nOFF";
                var colors = _saveDupButton.colors;
                Color baseColor = _guidedAutoAdvance
                    ? new Color(0.24f, 0.58f, 0.92f, 1f)
                    : new Color(0.32f, 0.32f, 0.38f, 1f);
                colors.normalColor = baseColor;
                colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
                colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
                _saveDupButton.colors = colors;
                var image = _saveDupButton.GetComponent<Image>();
                if (image != null)
                    image.color = baseColor;
            }
            else
            {
                label.text = "Save+Dup";
                var colors = _saveDupButton.colors;
                Color baseColor = new Color(0.24f, 0.32f, 0.44f, 1f);
                colors.normalColor = baseColor;
                colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
                colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
                _saveDupButton.colors = colors;
                var image = _saveDupButton.GetComponent<Image>();
                if (image != null)
                    image.color = baseColor;
            }
        }
    }
}
