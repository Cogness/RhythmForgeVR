using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Fires a UI ray from the MX Ink stylus tip to interact with world-space Canvas panels.
    /// Hit detection uses pure local-space math — no Camera.main dependency (safe in OVR/VR).
    /// Front button: clicks hovered button. Does NOT cycle draw mode or confirm draft.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class StylusUIPointer : MonoBehaviour
    {
        [SerializeField] private InputMapper _input;
        [SerializeField] private LineRenderer _rayVisual;
        [SerializeField] private float _maxRayDistance = 4f;

        private Button _hoveredButton;
        private ColorBlock _hoveredOriginalColors;
        private GameObject _cursorDot;

        // Hover highlight: bright white tint
        private static readonly Color HoverTint = new Color(1f, 1f, 1f, 1f);

        /// <summary>True while the stylus ray is over an interactive UI button.</summary>
        public bool IsHoveringUI => _hoveredButton != null;

        /// <summary>
        /// True for exactly one frame when the front button was pressed AND a UI button
        /// was clicked. StrokeCapture reads this to skip mode-cycle and draft-confirm.
        /// </summary>
        public bool DidClickUI { get; private set; }

        public void Configure(InputMapper input, LineRenderer rayVisual, LayerMask uiLayer)
        {
            _input = input;
            _rayVisual = rayVisual;

            // Style ray: thin white like Quest system pointer
            if (_rayVisual != null)
            {
                _rayVisual.startWidth = 0.002f;
                _rayVisual.endWidth   = 0.001f;
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = Color.white;
                _rayVisual.material = mat;
            }

            // Cursor dot: small white sphere at ray hit point
            _cursorDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _cursorDot.name = "StylusUICursor";
            Object.Destroy(_cursorDot.GetComponent<SphereCollider>());
            _cursorDot.transform.localScale = Vector3.one * 0.012f;
            var dotMat = new Material(Shader.Find("Sprites/Default"));
            dotMat.color = Color.white;
            _cursorDot.GetComponent<Renderer>().material = dotMat;
            _cursorDot.SetActive(false);
        }

        private void Update()
        {
            DidClickUI = false;

            if (_input == null) return;

            if (!_input.IsStylusActive)
            {
                SetRay(false, Vector3.zero, Vector3.zero, Vector3.zero);
                ClearHover();
                return;
            }

            var pose = _input.StylusPose;
            Vector3 origin = pose.position;
            Vector3 direction = pose.rotation * Vector3.forward;

            Button foundButton = null;
            Vector3 hitPoint = origin + direction * _maxRayDistance;
            float closestDist = _maxRayDistance;

            var raycasters = Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);

            foreach (var gr in raycasters)
            {
                if (!gr.gameObject.activeInHierarchy) continue;
                var canvas = gr.GetComponent<Canvas>();
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;

                // Intersect ray with canvas plane (canvas faces +Z in local space)
                var canvasPlane = new Plane(canvas.transform.forward, canvas.transform.position);
                float dist;
                if (!canvasPlane.Raycast(new Ray(origin, direction), out dist)) continue;
                if (dist > closestDist || dist < 0.01f) continue;

                Vector3 worldHit = origin + direction * dist;

                // worldHit → canvas local space (unscaled)
                // Canvas localScale is 0.001, so InverseTransformPoint gives pixel coords
                Vector3 localHit = canvas.transform.InverseTransformPoint(worldHit);
                var canvasRt = canvas.GetComponent<RectTransform>();
                Vector2 canvasSize = canvasRt.sizeDelta;

                // Canvas pivot is bottom-left (0,0); check bounds
                if (localHit.x < 0f || localHit.x > canvasSize.x ||
                    localHit.y < 0f || localHit.y > canvasSize.y)
                    continue;

                // Hit is on this canvas — now find which Button contains the pixel point
                var buttons = gr.GetComponentsInChildren<Button>(false);
                foreach (var btn in buttons)
                {
                    if (!btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                    var btnRt = btn.GetComponent<RectTransform>();
                    if (btnRt == null) continue;

                    // Button local space: InverseTransformPoint handles any nesting
                    Vector3 btnLocal = btnRt.InverseTransformPoint(worldHit);
                    if (btnRt.rect.Contains(new Vector2(btnLocal.x, btnLocal.y)))
                    {
                        foundButton = btn;
                        closestDist = dist;
                        hitPoint = worldHit;
                        break;
                    }
                }
            }

            SetRay(true, origin, hitPoint, hitPoint);

            // Hover: apply/remove tint and update cursor dot color
            if (foundButton != _hoveredButton)
            {
                if (_hoveredButton != null)
                {
                    _hoveredButton.colors = _hoveredOriginalColors;
                    ExecuteEvents.Execute(_hoveredButton.gameObject,
                        new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                }

                _hoveredButton = foundButton;

                if (_hoveredButton != null)
                {
                    _hoveredOriginalColors = _hoveredButton.colors;
                    var tinted = _hoveredButton.colors;
                    tinted.normalColor      = new Color(1f, 0.9f, 0.3f);  // gold
                    tinted.highlightedColor = new Color(1f, 0.9f, 0.3f);
                    _hoveredButton.colors = tinted;
                    ExecuteEvents.Execute(_hoveredButton.gameObject,
                        new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                }
            }

            // Cursor dot: gold on button hover, white otherwise
            if (_cursorDot != null)
            {
                var r = _cursorDot.GetComponent<Renderer>();
                if (r != null)
                    r.material.color = _hoveredButton != null
                        ? new Color(1f, 0.9f, 0.3f)
                        : Color.white;
            }

            // Click — only fires when hovering a button; marks input as consumed
            if (_input.FrontButtonDown && _hoveredButton != null)
            {
                DidClickUI = true;
                _input.FrontButtonConsumed = true;
                var ed = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(_hoveredButton.gameObject, ed, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(_hoveredButton.gameObject, ed, ExecuteEvents.pointerClickHandler);
                ExecuteEvents.Execute(_hoveredButton.gameObject, ed, ExecuteEvents.pointerUpHandler);
            }
        }

        private void ClearHover()
        {
            if (_hoveredButton != null)
            {
                _hoveredButton.colors = _hoveredOriginalColors;
                ExecuteEvents.Execute(_hoveredButton.gameObject,
                    new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                _hoveredButton = null;
            }
        }

        private void SetRay(bool visible, Vector3 from, Vector3 to, Vector3 dotPos)
        {
            if (_rayVisual != null)
            {
                _rayVisual.enabled = visible;
                if (visible)
                {
                    _rayVisual.SetPosition(0, from);
                    _rayVisual.SetPosition(1, to);
                }
            }

            if (_cursorDot != null)
            {
                _cursorDot.SetActive(visible);
                if (visible) _cursorDot.transform.position = dotPos;
            }
        }
    }
}

