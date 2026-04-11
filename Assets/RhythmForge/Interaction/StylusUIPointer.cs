using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmForge.Interaction
{
    /// <summary>
    /// Fires a UI ray from the MX Ink stylus tip to interact with world-space Canvas panels.
    /// Uses GraphicRaycaster on each active Canvas to find hovered Buttons.
    /// Front button click = pointer click on hovered button.
    /// Shows a yellow line renderer from the stylus tip toward the nearest UI hit.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class StylusUIPointer : MonoBehaviour
    {
        [SerializeField] private InputMapper _input;
        [SerializeField] private LineRenderer _rayVisual;
        [SerializeField] private float _maxRayDistance = 4f;

        private Button _hoveredButton;

        /// <summary>True while the stylus ray is pointing at an interactive UI button.</summary>
        public bool IsHoveringUI => _hoveredButton != null;

        public void Configure(InputMapper input, LineRenderer rayVisual, LayerMask uiLayer)
        {
            _input = input;
            _rayVisual = rayVisual;
        }

        private void Update()
        {
            if (_input == null) return;

            bool stylusActive = _input.IsStylusActive;
            if (!stylusActive)
            {
                SetRay(false, Vector3.zero, Vector3.zero);
                ClearHover();
                return;
            }

            var pose = _input.StylusPose;
            Vector3 origin = pose.position;
            Vector3 direction = pose.rotation * Vector3.forward;

            Button foundButton = null;
            Vector3 hitPoint = origin + direction * _maxRayDistance;

            // Test against every active GraphicRaycaster in the scene
            var raycasters = Object.FindObjectsByType<GraphicRaycaster>(
                FindObjectsSortMode.None);

            float closestDist = _maxRayDistance;

            foreach (var gr in raycasters)
            {
                if (!gr.gameObject.activeInHierarchy) continue;

                var canvas = gr.GetComponent<Canvas>();
                if (canvas == null || canvas.renderMode != UnityEngine.RenderMode.WorldSpace)
                    continue;

                // Build a fake PointerEventData with a screen-space position derived
                // from projecting the ray onto the canvas plane.
                var plane = new Plane(-canvas.transform.forward, canvas.transform.position);
                float dist;
                if (!plane.Raycast(new Ray(origin, direction), out dist)) continue;
                if (dist > closestDist) continue;

                Vector3 worldHit = origin + direction * dist;

                // Convert world hit to canvas local space
                Vector3 localHit = canvas.transform.InverseTransformPoint(worldHit);
                var rt = canvas.GetComponent<RectTransform>();
                Vector2 canvasSize = rt.sizeDelta;

                // Check if hit is within canvas bounds
                if (localHit.x < 0 || localHit.x > canvasSize.x ||
                    localHit.y < 0 || localHit.y > canvasSize.y)
                    continue;

                // Find which Button contains this local point
                var buttons = gr.GetComponentsInChildren<Button>(false);
                foreach (var btn in buttons)
                {
                    if (!btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                    var btnRt = btn.GetComponent<RectTransform>();
                    if (btnRt == null) continue;

                    // Convert world hit to button's local space
                    Vector2 localPos;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            btnRt,
                            RectTransformUtility.WorldToScreenPoint(Camera.main, worldHit),
                            Camera.main,
                            out localPos))
                    {
                        if (btnRt.rect.Contains(localPos))
                        {
                            foundButton = btn;
                            closestDist = dist;
                            hitPoint = worldHit;
                            break;
                        }
                    }
                }
            }

            // Update ray visual
            SetRay(true, origin, hitPoint);

            // Hover state
            if (foundButton != _hoveredButton)
            {
                if (_hoveredButton != null)
                    ExecuteEvents.Execute(_hoveredButton.gameObject,
                        new PointerEventData(EventSystem.current),
                        ExecuteEvents.pointerExitHandler);

                _hoveredButton = foundButton;

                if (_hoveredButton != null)
                    ExecuteEvents.Execute(_hoveredButton.gameObject,
                        new PointerEventData(EventSystem.current),
                        ExecuteEvents.pointerEnterHandler);
            }

            // Click on front button press (edge)
            bool clickNow = _input.FrontButtonDown;
            if (clickNow && _hoveredButton != null)
            {
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
                ExecuteEvents.Execute(_hoveredButton.gameObject,
                    new PointerEventData(EventSystem.current),
                    ExecuteEvents.pointerExitHandler);
                _hoveredButton = null;
            }
        }

        private void SetRay(bool visible, Vector3 from, Vector3 to)
        {
            if (_rayVisual == null) return;
            _rayVisual.enabled = visible;
            if (visible)
            {
                _rayVisual.SetPosition(0, from);
                _rayVisual.SetPosition(1, to);
            }
        }
    }
}

