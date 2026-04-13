using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Session;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space Canvas panel showing 8 arrangement slots with scene/bars selectors.
    /// </summary>
    public class ArrangementPanel : MonoBehaviour
    {
        [System.Serializable]
        public class SlotUI
        {
            public Button sceneButton;
            public Text sceneLabel;
            public Button barsButton;
            public Text barsLabel;
            public Text slotLabel;
        }

        [SerializeField] private List<SlotUI> _slots = new List<SlotUI>();
        [SerializeField] private Color _activeSlotColor = new Color(0.24f, 0.72f, 0.96f, 1f);
        [SerializeField] private Color _filledSlotColor = new Color(0.22f, 0.22f, 0.30f, 1f);
        [SerializeField] private Color _emptySlotColor = new Color(0.14f, 0.14f, 0.18f, 1f);
        [SerializeField] private Color _activeSlotLabelColor = Color.white;
        [SerializeField] private Color _inactiveSlotLabelColor = new Color(0.5f, 0.6f, 0.7f, 1f);

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private readonly string[] _sceneOptions = { "", "scene-a", "scene-b", "scene-c", "scene-d" };
        private readonly int[] _barsOptions = { 4, 8, 16 };
        private bool _updating;

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer)
        {
            _store = store;
            _sequencer = sequencer;

            for (int i = 0; i < _slots.Count && i < AppStateFactory.MaxArrangementSlots; i++)
            {
                int index = i;
                var slot = _slots[i];

                if (slot.sceneButton != null)
                    slot.sceneButton.onClick.AddListener(() => CycleScene(index));
                if (slot.barsButton != null)
                    slot.barsButton.onClick.AddListener(() => CycleBars(index));

                if (slot.slotLabel != null)
                    slot.slotLabel.text = $"Slot {i + 1}";
            }

            _store.OnStateChanged += Refresh;
            if (_sequencer != null) _sequencer.OnTransportChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_store != null) _store.OnStateChanged -= Refresh;
            if (_sequencer != null) _sequencer.OnTransportChanged -= Refresh;
        }

        private void CycleScene(int index)
        {
            if (_updating || _store == null) return;

            var slot = GetSlotData(index);
            if (slot == null) return;

            int currentIndex = GetSceneIndex(slot.sceneId);
            int nextIndex = (currentIndex + 1) % _sceneOptions.Length;
            string nextSceneId = string.IsNullOrEmpty(_sceneOptions[nextIndex]) ? null : _sceneOptions[nextIndex];
            _store.UpdateArrangement(slot.id, sceneId: nextSceneId);
        }

        private void CycleBars(int index)
        {
            if (_updating || _store == null) return;

            var slot = GetSlotData(index);
            if (slot == null) return;

            int currentIndex = GetBarsIndex(slot.bars);
            int nextIndex = (currentIndex + 1) % _barsOptions.Length;
            _store.UpdateArrangement(slot.id, bars: _barsOptions[nextIndex]);
        }

        private void Refresh()
        {
            if (_store == null) return;

            _updating = true;
            int activeSlotIndex = GetActiveSlotIndex();

            for (int i = 0; i < _slots.Count && i < _store.State.arrangement.Count; i++)
            {
                var data = _store.State.arrangement[i];
                var slot = _slots[i];
                bool isActiveSlot = i == activeSlotIndex;

                if (slot.slotLabel != null)
                {
                    slot.slotLabel.text = isActiveSlot ? $"Slot {i + 1} \u25B6" : $"Slot {i + 1}";
                    slot.slotLabel.color = isActiveSlot ? _activeSlotLabelColor : _inactiveSlotLabelColor;
                }

                if (slot.sceneLabel != null)
                    slot.sceneLabel.text = GetSceneDisplayLabel(data.sceneId);

                if (slot.barsLabel != null)
                    slot.barsLabel.text = $"{GetNormalizedBars(data.bars)}";

                ApplyButtonStyle(slot.sceneButton, isActiveSlot, data.IsPopulated);
                ApplyButtonStyle(slot.barsButton, isActiveSlot, data.IsPopulated);
            }
            _updating = false;
        }

        private ArrangementSlot GetSlotData(int index)
        {
            if (_store?.State?.arrangement == null) return null;
            if (index < 0 || index >= _store.State.arrangement.Count) return null;
            return _store.State.arrangement[index];
        }

        private int GetActiveSlotIndex()
        {
            if (_sequencer == null || !_sequencer.IsPlaying) return -1;

            var transport = _sequencer.CurrentTransport;
            if (transport == null || transport.mode != "arrangement") return -1;
            return transport.slotIndex;
        }

        private int GetSceneIndex(string sceneId)
        {
            int index = System.Array.IndexOf(_sceneOptions, sceneId ?? "");
            return index >= 0 ? index : 0;
        }

        private int GetBarsIndex(int bars)
        {
            int index = System.Array.IndexOf(_barsOptions, bars);
            return index >= 0 ? index : 0;
        }

        private int GetNormalizedBars(int bars)
        {
            int index = GetBarsIndex(bars);
            return _barsOptions[index];
        }

        private string GetSceneDisplayLabel(string sceneId)
        {
            switch (sceneId)
            {
                case "scene-a": return "A";
                case "scene-b": return "B";
                case "scene-c": return "C";
                case "scene-d": return "D";
                default: return "--";
            }
        }

        private void ApplyButtonStyle(Button button, bool isActiveSlot, bool isFilledSlot)
        {
            if (button == null) return;

            Color baseColor = isActiveSlot
                ? _activeSlotColor
                : isFilledSlot ? _filledSlotColor : _emptySlotColor;

            var colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
            button.colors = colors;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = baseColor;
        }
    }
}
