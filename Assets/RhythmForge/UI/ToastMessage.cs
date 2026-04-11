using UnityEngine;
using UnityEngine.UI;

namespace RhythmForge.UI
{
    /// <summary>
    /// Floating notification text in world-space that fades after a short duration.
    /// </summary>
    public class ToastMessage : MonoBehaviour
    {
        [SerializeField] private Text _text;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _displayDuration = 2.4f;
        [SerializeField] private float _fadeDuration = 0.4f;
        [SerializeField] private Transform _followTarget; // e.g. camera

        private float _timer;
        private bool _showing;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponentInChildren<CanvasGroup>();
            if (_text == null)
                _text = GetComponentInChildren<Text>();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (!_showing) return;

            _timer -= Time.deltaTime;

            if (_timer <= 0f)
            {
                float fadeProgress = Mathf.Clamp01((-_timer) / _fadeDuration);
                if (_canvasGroup != null)
                    _canvasGroup.alpha = 1f - fadeProgress;

                if (fadeProgress >= 1f)
                    _showing = false;
            }

            // Face user
            if (_followTarget != null)
            {
                Vector3 lookDir = _followTarget.position - transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(-lookDir.normalized, Vector3.up);
            }
        }

        public void Show(string message)
        {
            if (_text != null)
                _text.text = message;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;

            _timer = _displayDuration;
            _showing = true;
        }
    }
}
