using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.UI
{
    /// <summary>
    /// World-space TextMesh that displays per-mode geometric inputs and sound outputs
    /// near its parent PatternVisualizer. Billboard-faces the user head each frame.
    /// </summary>
    public class ShapeParameterLabel : MonoBehaviour
    {
        private TextMesh _textMesh;
        private MeshRenderer _renderer;
        private Transform _userHead;
        private Transform _textTransform;
        private bool _visible = true;

        private static readonly float FontSize = 24;
        private static readonly float CharacterSize = 0.008f;
        private static readonly Vector3 Offset = new Vector3(0f, -0.06f, 0f);

        public void Initialize(Transform userHead)
        {
            _userHead = userHead;

            var child = new GameObject("ParamLabel");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Offset;
            _textTransform = child.transform;

            _textMesh = child.AddComponent<TextMesh>();
            _textMesh.fontSize = (int)FontSize;
            _textMesh.characterSize = CharacterSize;
            _textMesh.anchor = TextAnchor.UpperCenter;
            _textMesh.alignment = TextAlignment.Left;
            _textMesh.color = new Color(0.85f, 0.92f, 1f, 0.9f);
            _textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_textMesh.font == null)
                _textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            _renderer = child.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            // Use a font material that renders on top of everything for readability
            if (_textMesh.font != null && _textMesh.font.material != null)
            {
                var mat = new Material(_textMesh.font.material);
                _renderer.material = mat;
            }
        }

        public void SetData(PatternType type, ShapeProfile sp, SoundProfile snd)
        {
            if (_textMesh == null) return;
            _textMesh.text = PatternParameterHelper.FormatLabel(type, sp, snd);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_renderer != null)
                _renderer.enabled = _visible;
        }

        public void SetLocalOffset(Vector3 localOffset)
        {
            if (_textTransform != null)
                _textTransform.localPosition = localOffset;
        }

        public bool IsVisible => _visible;

        private void LateUpdate()
        {
            if (_textMesh == null || _userHead == null) return;

            // Billboard: face toward the user
            Vector3 toUser = _userHead.position - _textMesh.transform.position;
            if (toUser.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = toUser.normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                    ? Vector3.forward
                    : Vector3.up;
                _textMesh.transform.rotation = Quaternion.LookRotation(forward, up);
            }
        }
    }
}
