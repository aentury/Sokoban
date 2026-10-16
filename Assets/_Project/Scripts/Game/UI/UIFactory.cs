using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sokoban.Game
{
    /// <summary>
    /// 用代码搭建 uGUI 控件的小工具，保证所有界面风格统一。
    /// 布局交给 LayoutGroup，适配由 CanvasScaler 按 1920x1080 参考分辨率缩放。
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;

        /// <summary>
        /// 界面字体：随项目附带的思源黑体子集（Resources/Fonts/NotoSansSC，含常用简体汉字），
        /// 保证在没有安装中文字体的电脑上也能正常显示。缺失时退回 Unity 内置字体。
        /// </summary>
        private static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/NotoSansSC");
                if (_font == null)
                {
                    Debug.LogWarning("Missing font Resources/Fonts/NotoSansSC, falling back to the built-in font.");
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }
        private static Sprite RoundedSprite => SpriteLibrary.GetUISprite("ui_rounded", 20f);

        public static Canvas CreateCanvas(Transform parent)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(parent, false);
            }
            return canvas;
        }

        // ---------- 基础 ----------

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>铺满父物体的空容器，用作一个界面的根节点。</summary>
        public static RectTransform CreateFullScreen(Transform parent, string name)
        {
            RectTransform rect = CreateRect(name, parent);
            Stretch(rect);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>以某个锚点（同时作为轴心）定位，例如 (0.5, 1) 表示贴着顶部居中。</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void SetPreferredSize(GameObject go, float width, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        // ---------- 控件 ----------

        public static Image CreateImage(Transform parent, string name, Color color, bool rounded = false)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = RoundedSprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static Text CreateText(Transform parent, string content, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = CreateRect("Text", parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Action onClick,
            float width = 360f, float height = 76f, bool primary = false, int fontSize = 32)
        {
            Image image = CreateImage(parent, (label ?? "Level") + " Button", Color.white, true);
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(width, height);
            SetPreferredSize(rect.gameObject, width, height);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // 禁用方向键导航：方向键用于控制角色，不应该切换按钮焦点
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            if (!string.IsNullOrEmpty(label))
            {
                Text text = CreateText(rect, label, fontSize, Palette.Text);
                Stretch(text.rectTransform);
            }

            ColorBlock colors = button.colors;
            colors.disabledColor = Palette.ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            SetHighlighted(button, primary);

            button.onClick.AddListener(() =>
            {
                // 点击后取消选中，避免之后按回车 / 空格再次触发这个按钮
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                Sfx.Play("click", 0.6f);
                onClick();
            });
            return button;
        }

        /// <summary>高亮（主按钮 / 当前选中的工具）用强调色，否则用普通按钮色。</summary>
        public static void SetHighlighted(Button button, bool highlighted)
        {
            Color normal = highlighted ? Palette.Accent : Palette.Button;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.selectedColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.2f);
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = highlighted ? Palette.TextOnAccent : Palette.Text;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }

        public static Slider CreateSlider(Transform parent, float value, Action<float> onChanged, float width = 420f)
        {
            RectTransform root = CreateRect("Slider", parent);
            root.sizeDelta = new Vector2(width, 40f);
            SetPreferredSize(root.gameObject, width, 40f);

            Image background = CreateImage(root, "Background", Palette.Button, true);
            Stretch(background.rectTransform);

            RectTransform fillArea = CreateRect("Fill Area", root);
            Stretch(fillArea);
            Image fill = CreateImage(fillArea, "Fill", Palette.Accent, true);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = CreateRect("Handle Slide Area", root);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(20f, 0f);
            handleArea.offsetMax = new Vector2(-20f, 0f);
            Image handle = CreateImage(handleArea, "Handle", Palette.Text, true);
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(40f, 16f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static InputField CreateInputField(Transform parent, string placeholder, float width, float height)
        {
            Image background = CreateImage(parent, "Input Field", Palette.Input, true);
            background.rectTransform.sizeDelta = new Vector2(width, height);
            SetPreferredSize(background.gameObject, width, height);

            Text text = CreateText(background.rectTransform, "", 28, Palette.Text, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetInnerPadding(text.rectTransform, 16f);

            Text hint = CreateText(background.rectTransform, placeholder, 28, Palette.TextDim, TextAnchor.MiddleLeft);
            hint.fontStyle = FontStyle.Italic;
            SetInnerPadding(hint.rectTransform, 16f);

            var input = background.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.targetGraphic = background;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 24;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        // ---------- 布局 ----------

        /// <summary>纵向排列子物体并自动适应内容大小的容器。</summary>
        public static RectTransform CreateColumn(Transform parent, float spacing, int padding = 0)
        {
            RectTransform rect = CreateRect("Column", parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, spacing, padding);
            AddFitter(rect.gameObject);
            return rect;
        }

        /// <summary>横向排列子物体并自动适应内容大小的容器。</summary>
        public static RectTransform CreateRow(Transform parent, float spacing)
        {
            RectTransform rect = CreateRect("Row", parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, spacing, 0);
            AddFitter(rect.gameObject);
            return rect;
        }

        /// <summary>固定列数的网格，常用于一排排同样大小的按钮。</summary>
        public static RectTransform CreateGrid(Transform parent, Vector2 cellSize, int columns, float spacing)
        {
            RectTransform rect = CreateRect("Grid", parent);
            var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.MiddleCenter;
            return rect;
        }

        /// <summary>可以用鼠标滚轮 / 拖动滚动的纵向列表。返回列表根节点，content 用来放列表项。</summary>
        public static RectTransform CreateScrollList(Transform parent, out RectTransform content)
        {
            RectTransform root = CreateRect("Scroll List", parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 透明背景让空白处也能接收滚轮和拖动
            viewport.gameObject.AddComponent<Image>().color = Color.clear;

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return root;
        }

        public static void CreateSpacer(Transform parent, float height)
        {
            RectTransform rect = CreateRect("Spacer", parent);
            SetPreferredSize(rect.gameObject, 0f, height);
        }

        /// <summary>半透明遮罩铺满全屏（同时挡住下层的点击），返回遮罩本身。</summary>
        public static RectTransform CreateOverlay(Transform parent, string name)
        {
            Image overlay = CreateImage(parent, name, Palette.Overlay);
            Stretch(overlay.rectTransform);
            return overlay.rectTransform;
        }

        /// <summary>居中的圆角卡片，内容纵向排列。</summary>
        public static RectTransform CreateCard(Transform parent)
        {
            RectTransform card = CreateColumn(parent, 20f, 56);
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Palette.Card;
            return card;
        }

        /// <summary>删除容器下的所有子物体（先隐藏，避免在销毁前这一帧还参与布局）。</summary>
        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        private static void SetInnerPadding(RectTransform rect, float padding)
        {
            Stretch(rect);
            rect.offsetMin = new Vector2(padding, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);
        }

        private static void ConfigureLayout(HorizontalOrVerticalLayoutGroup layout, float spacing, int padding)
        {
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void AddFitter(GameObject go)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
