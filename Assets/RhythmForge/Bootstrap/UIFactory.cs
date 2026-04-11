using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmForge.Bootstrap
{
    /// <summary>
    /// Builds Unity UI elements entirely from code at runtime.
    /// All panels, buttons, text labels, sliders and canvases created here
    /// — no prefabs or manual Inspector wiring needed.
    /// </summary>
    public static class UIFactory
    {
        // Built-in font loaded once
        private static Font _font;
        private static Font DefaultFont
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        // ───────────────────── CANVAS ─────────────────────

        /// <summary>
        /// Creates a world-space Canvas at the given position.
        /// </summary>
        public static Canvas CreateWorldCanvas(string name, Transform parent,
            Vector2 size, Vector3 worldPosition, float worldScale = 0.001f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one * worldScale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            // BoxCollider so Physics.Raycast can hit this canvas.
            // Size is in canvas pixels; world size = pixels * worldScale.
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 1f);
            col.center = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);

            // Layer 5 = Unity built-in "UI" layer
            go.layer = 5;

            return canvas;
        }

        // ───────────────────── BACKGROUND ─────────────────────

        public static Image CreateBackground(Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return img;
        }

        // ───────────────────── TEXT ─────────────────────

        public static Text CreateText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.font = DefaultFont;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return text;
        }

        /// <summary>Centered text spanning the full canvas area.</summary>
        public static Text CreateCenteredText(Transform parent, string name, string content,
            int fontSize, Color color)
        {
            return CreateText(parent, name, content, fontSize, color,
                TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>Text positioned by pixel rect inside the canvas.</summary>
        public static Text CreateRectText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment,
            Rect rect)
        {
            return CreateText(parent, name, content, fontSize, color, alignment,
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMax));
        }

        // ───────────────────── BUTTON ─────────────────────

        public static Button CreateButton(Transform parent, string name, string label,
            Rect rect, Color bgColor, Color textColor, int fontSize, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.25f);
            btn.colors = colors;

            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var rt = go.GetComponent<RectTransform>();
            SetAnchoredRect(rt, rect);

            // Label child
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            return btn;
        }

        // ───────────────────── SLIDER ─────────────────────

        public static Slider CreateSlider(Transform parent, string name,
            float minVal, float maxVal, float value, Rect rect,
            Action<float> onChanged)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            SetAnchoredRect(rt, rect);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.25f);
            bgRt.anchorMax = new Vector2(1, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1, 0.75f);
            fillAreaRt.offsetMin = new Vector2(5, 0);
            fillAreaRt.offsetMax = new Vector2(-15, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.24f, 0.72f, 0.96f, 1f);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(1, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(10, 0);
            handleAreaRt.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(20, 0);

            // Slider component
            var slider = go.AddComponent<Slider>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = value;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));

            return slider;
        }

        // ───────────────────── IMAGE ─────────────────────

        public static Image CreateImage(Transform parent, string name, Color color, Rect rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            SetAnchoredRect(rt, rect);
            return img;
        }

        // ───────────────────── CANVAS GROUP ─────────────────────

        public static CanvasGroup AddCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        // ───────────────────── DROPDOWN ─────────────────────

        public static Dropdown CreateDropdown(Transform parent, string name,
            List<string> options, Rect rect, Action<int> onChanged)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.24f, 1f);
            var rt = go.GetComponent<RectTransform>();
            SetAnchoredRect(rt, rect);

            var dropdown = go.AddComponent<Dropdown>();
            dropdown.options.Clear();
            foreach (var opt in options)
                dropdown.options.Add(new Dropdown.OptionData(opt));

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.font = DefaultFont;
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(10, 2);
            labelRt.offsetMax = new Vector2(-25, -2);

            dropdown.captionText = labelText;
            dropdown.targetGraphic = img;

            if (onChanged != null) dropdown.onValueChanged.AddListener(v => onChanged(v));

            return dropdown;
        }

        // ───────────────────── SCROLLVIEW (simple) ─────────────────────

        public static ScrollRect CreateScrollView(Transform parent, string name, Rect rect)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var rt = go.GetComponent<RectTransform>();
            SetAnchoredRect(rt, rect);
            img.color = new Color(0.1f, 0.1f, 0.14f, 0.6f);

            // Viewport
            var vp = new GameObject("Viewport");
            vp.transform.SetParent(go.transform, false);
            var vpImg = vp.AddComponent<Image>();
            var mask = vp.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var vpRt = vp.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(vp.transform, false);
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var vl = content.AddComponent<VerticalLayoutGroup>();
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.spacing = 4f;

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = go.AddComponent<ScrollRect>();
            scroll.viewport = vpRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20f;

            return scroll;
        }

        // ───────────────────── LAYOUT HELPERS ─────────────────────

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go,
            float spacing = 8f, RectOffset padding = null)
        {
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            if (padding != null) hlg.padding = padding;
            return hlg;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject go,
            float spacing = 6f, RectOffset padding = null)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            if (padding != null) vlg.padding = padding;
            return vlg;
        }

        // ───────────────────── UTILITY ─────────────────────

        /// <summary>
        /// Sets RectTransform using pixel offsets from parent origin.
        /// rect.x/y = bottom-left corner; rect.width/height = size.
        /// </summary>
        public static void SetAnchoredRect(RectTransform rt, Rect rect)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(rect.x, rect.y);
            rt.sizeDelta = new Vector2(rect.width, rect.height);
        }

        public static GameObject CreateEmpty(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }
    }
}
