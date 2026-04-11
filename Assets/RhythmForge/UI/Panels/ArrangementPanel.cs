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
            public Dropdown sceneDropdown;
            public Dropdown barsDropdown;
            public Text slotLabel;
        }

        [SerializeField] private List<SlotUI> _slots;

        private SessionStore _store;
        private Sequencer.Sequencer _sequencer;
        private string[] _sceneOptions = { "", "scene-a", "scene-b", "scene-c", "scene-d" };
        private int[] _barsOptions = { 4, 8, 16 };
        private bool _updating;

        public void Initialize(SessionStore store, Sequencer.Sequencer sequencer)
        {
            _store = store;
            _sequencer = sequencer;

            for (int i = 0; i < _slots.Count && i < AppStateFactory.MaxArrangementSlots; i++)
            {
                int index = i;
                var slot = _slots[i];

                // Scene dropdown
                if (slot.sceneDropdown != null)
                {
                    slot.sceneDropdown.ClearOptions();
                    slot.sceneDropdown.AddOptions(new List<string> { "--", "A", "B", "C", "D" });
                    slot.sceneDropdown.onValueChanged.AddListener(v => OnSceneChanged(index, v));
                }

                // Bars dropdown
                if (slot.barsDropdown != null)
                {
                    slot.barsDropdown.ClearOptions();
                    slot.barsDropdown.AddOptions(new List<string> { "4", "8", "16" });
                    slot.barsDropdown.onValueChanged.AddListener(v => OnBarsChanged(index, v));
                }

                if (slot.slotLabel != null)
                    slot.slotLabel.text = $"Slot {i + 1}";
            }

            _store.OnStateChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_store != null) _store.OnStateChanged -= Refresh;
        }

        private void OnSceneChanged(int index, int dropdownValue)
        {
            if (_updating || _store == null) return;
            string sceneId = dropdownValue < _sceneOptions.Length ? _sceneOptions[dropdownValue] : "";
            _store.UpdateArrangement($"slot-{index + 1}", sceneId: sceneId);
        }

        private void OnBarsChanged(int index, int dropdownValue)
        {
            if (_updating || _store == null) return;
            int bars = dropdownValue < _barsOptions.Length ? _barsOptions[dropdownValue] : 4;
            _store.UpdateArrangement($"slot-{index + 1}", bars: bars);
        }

        private void Refresh()
        {
            _updating = true;
            for (int i = 0; i < _slots.Count && i < _store.State.arrangement.Count; i++)
            {
                var data = _store.State.arrangement[i];
                var slot = _slots[i];

                if (slot.sceneDropdown != null)
                {
                    int sceneIdx = System.Array.IndexOf(_sceneOptions, data.sceneId ?? "");
                    slot.sceneDropdown.SetValueWithoutNotify(Mathf.Max(0, sceneIdx));
                }

                if (slot.barsDropdown != null)
                {
                    int barsIdx = System.Array.IndexOf(_barsOptions, data.bars);
                    slot.barsDropdown.SetValueWithoutNotify(Mathf.Max(0, barsIdx));
                }
            }
            _updating = false;
        }
    }
}
