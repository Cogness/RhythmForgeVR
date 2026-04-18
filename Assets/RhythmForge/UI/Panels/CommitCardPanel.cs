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
        private RhythmForgeEventBus _eventBus;
        private DraftResult _currentDraft;

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

        public void Initialize(StrokeCapture strokeCapture)
        {
            _strokeCapture = strokeCapture;
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
            if (_saveDupButton) _saveDupButton.onClick.AddListener(() => Confirm(true));
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

            // SW-3: Show kinematic pressure data if available
            if (_detailsText != null && draft.shapeProfile != null && draft.shapeProfile.pressureMean > 0.01f)
            {
                string pressureLine = $"\nPressure: {draft.shapeProfile.pressureMean:F2} avg  " +
                                      $"{draft.shapeProfile.pressurePeak:F2} peak";
                _detailsText.text = draft.details + pressureLine;
            }
        }

        private void Confirm(bool duplicate)
        {
            if (_strokeCapture != null && _currentDraft != null)
                _strokeCapture.ConfirmDraft(duplicate);

            _currentDraft = null;
            Hide();
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
    }
}
