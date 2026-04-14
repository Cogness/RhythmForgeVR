using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmForge.Core.Data;
using RhythmForge.Core.Events;
using RhythmForge.Core.Session;

namespace RhythmForge.UI.Panels
{
    /// <summary>
    /// World-space panel that lets the user switch between Musical Genres.
    /// Switching a genre re-derives all existing patterns destructively.
    /// </summary>
    public class GenreSelectorPanel : MonoBehaviour
    {
        private SessionStore _store;
        private RhythmForgeEventBus _eventBus;
        private Text _descriptionLabel;
        private Text _toastText;
        private CanvasGroup _toastCG;
        private float _toastTimer;

        private readonly List<Button> _genreButtons = new List<Button>();
        private readonly List<Text>   _genreLabels  = new List<Text>();
        private readonly List<string> _genreIds     = new List<string>();

        public void SetUIRefs(
            List<Button> buttons,
            List<Text> labels,
            List<string> genreIds,
            Text descriptionLabel)
        {
            _genreButtons.Clear();
            _genreLabels.Clear();
            _genreIds.Clear();

            _genreButtons.AddRange(buttons);
            _genreLabels.AddRange(labels);
            _genreIds.AddRange(genreIds);
            _descriptionLabel = descriptionLabel;
        }

        public void Initialize(SessionStore store)
        {
            _store    = store;
            _eventBus = store?.EventBus;

            for (int i = 0; i < _genreButtons.Count; i++)
            {
                int idx = i;
                if (_genreButtons[idx] != null)
                    _genreButtons[idx].onClick.AddListener(() => OnGenreClicked(_genreIds[idx]));
            }

            if (_eventBus != null)
            {
                _eventBus.Subscribe<GenreChangedEvent>(HandleGenreChanged);
                _eventBus.Subscribe<SessionStateChangedEvent>(HandleSessionChanged);
            }

            RefreshHighlight();
        }

        private void OnDestroy()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe<GenreChangedEvent>(HandleGenreChanged);
            _eventBus.Unsubscribe<SessionStateChangedEvent>(HandleSessionChanged);
        }

        private void OnGenreClicked(string genreId)
        {
            if (_store == null) return;
            _store.SetGenre(genreId);
        }

        private void HandleGenreChanged(GenreChangedEvent evt)
        {
            RefreshHighlight();
            ShowDescription(GenreRegistry.Get(evt.NewGenreId)?.Description ?? string.Empty);
        }

        private void HandleSessionChanged(SessionStateChangedEvent evt)
        {
            RefreshHighlight();
        }

        private void RefreshHighlight()
        {
            string activeId = _store?.GetActiveGenreId() ?? "electronic";

            for (int i = 0; i < _genreButtons.Count; i++)
            {
                if (_genreButtons[i] == null) continue;
                var colors = _genreButtons[i].colors;
                bool isActive = _genreIds[i] == activeId;
                var genre = GenreRegistry.Get(_genreIds[i]);

                if (isActive)
                {
                    Color c = genre?.ColorPalette?.rhythmLoop ?? new Color(0.3f, 0.7f, 1f);
                    c.a = 1f;
                    colors.normalColor = c;
                }
                else
                {
                    colors.normalColor = new Color(0.18f, 0.18f, 0.24f, 1f);
                }

                _genreButtons[i].colors = colors;
            }

            if (_descriptionLabel != null)
            {
                var active = GenreRegistry.Get(activeId);
                _descriptionLabel.text = active?.Description ?? string.Empty;
            }
        }

        private void ShowDescription(string text)
        {
            if (_descriptionLabel != null)
                _descriptionLabel.text = text;
        }
    }
}
