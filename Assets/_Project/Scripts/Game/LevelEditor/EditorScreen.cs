using System;
using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.Game
{
    /// <summary>
    /// 关卡编辑器的界面：左侧工具栏 + 底部状态栏。
    /// 编辑操作直接调用 LevelEditor；保存、试玩、返回涉及其他模块，交给 App 处理。
    /// </summary>
    public sealed class EditorScreen : ScreenBase
    {
        private readonly LevelEditor _editor;
        private readonly Dictionary<EditTool, Button> _toolButtons = new Dictionary<EditTool, Button>();
        private readonly Text _widthValue;
        private readonly Text _heightValue;
        private readonly Text _status;
        private readonly InputField _nameField;
        private readonly Button _undo;

        public bool IsTyping => _nameField.isFocused;

        public string LevelName
        {
            get => _nameField.text.Trim();
            set => _nameField.text = value;
        }

        public EditorScreen(Transform canvas, LevelEditor editor, Action onSave, Action onTestPlay, Action onBack)
        {
            _editor = editor;
            Root = UIFactory.CreateFullScreen(canvas, "Level Editor").gameObject;

            // ---- 左侧工具栏 ----
            Image panel = UIFactory.CreateImage(Root.transform, "Tool Panel", Palette.Card);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.sizeDelta = new Vector2(LevelEditor.PanelWidth, 0f);
            panelRect.anchoredPosition = Vector2.zero;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Text title = UIFactory.CreateText(panelRect, "关卡编辑器", 44, Palette.Accent);
            title.fontStyle = FontStyle.Bold;

            SectionLabel(panelRect, "工具");
            RectTransform tools = UIFactory.CreateGrid(panelRect, new Vector2(112f, 52f), 3, 8f);
            AddToolButton(tools, EditTool.Wall, "1 墙");
            AddToolButton(tools, EditTool.Box, "2 箱子");
            AddToolButton(tools, EditTool.Goal, "3 目标点");
            AddToolButton(tools, EditTool.Player, "4 玩家");
            AddToolButton(tools, EditTool.Erase, "5 橡皮");

            SectionLabel(panelRect, "尺寸");
            _widthValue = SizeRow(panelRect, "宽度", () => editor.ResizeBy(-1, 0), () => editor.ResizeBy(1, 0));
            _heightValue = SizeRow(panelRect, "高度", () => editor.ResizeBy(0, -1), () => editor.ResizeBy(0, 1));

            SectionLabel(panelRect, "名称");
            _nameField = UIFactory.CreateInputField(panelRect, "输入关卡名称", 352f, 52f);

            SectionLabel(panelRect, "操作");
            RectTransform actions = UIFactory.CreateGrid(panelRect, new Vector2(172f, 52f), 2, 8f);
            _undo = UIFactory.CreateButton(actions, "撤销 (Z)", editor.Undo, 172f, 52f, false, 26);
            UIFactory.CreateButton(actions, "新建", OnNew, 172f, 52f, false, 26);
            UIFactory.CreateButton(actions, "检查是否有解", editor.CheckSolvable, 172f, 52f, false, 26);
            UIFactory.CreateButton(actions, "保存", onSave, 172f, 52f, false, 26);
            UIFactory.CreateButton(actions, "试玩", onTestPlay, 172f, 52f, true, 26);
            UIFactory.CreateButton(actions, "返回", onBack, 172f, 52f, false, 26);

            UIFactory.CreateSpacer(panelRect, 4f);
            Text help = UIFactory.CreateText(panelRect,
                "左键：绘制    右键：擦除\n数字键 1-5：切换工具    Z：撤销", 22, Palette.TextDim);
            help.lineSpacing = 1.2f;

            // ---- 底部状态栏 ----
            Image bar = UIFactory.CreateImage(Root.transform, "Status Bar", Palette.Bar);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = Vector2.zero;
            barRect.offsetMin = new Vector2(LevelEditor.PanelWidth, 0f);
            barRect.offsetMax = new Vector2(0f, LevelEditor.StatusBarHeight);

            _status = UIFactory.CreateText(barRect, "", 26, Palette.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Stretch(_status.rectTransform);
            _status.rectTransform.offsetMin = new Vector2(24f, 0f);

            editor.Changed += Refresh;
            editor.StatusChanged += SetStatus;
            Refresh();
            SetStatus("画出墙、箱子、目标点和玩家，每个箱子都需要一个目标点。", StatusKind.Info);
        }

        private void OnNew()
        {
            _editor.NewLevel();
            LevelName = "";
        }

        private void Refresh()
        {
            foreach (KeyValuePair<EditTool, Button> pair in _toolButtons)
                UIFactory.SetHighlighted(pair.Value, pair.Key == _editor.Tool);
            _widthValue.text = _editor.Grid.Width.ToString();
            _heightValue.text = _editor.Grid.Height.ToString();
            _undo.interactable = _editor.CanUndo;
        }

        private void SetStatus(string message, StatusKind kind)
        {
            _status.text = message;
            _status.color = kind == StatusKind.Error ? Palette.Error
                : kind == StatusKind.Success ? Palette.Success
                : Palette.TextDim;
        }

        private void AddToolButton(Transform parent, EditTool tool, string label)
        {
            _toolButtons[tool] = UIFactory.CreateButton(parent, label, () => _editor.Tool = tool, 112f, 52f, false, 24);
        }

        private static void SectionLabel(Transform parent, string text)
        {
            Text label = UIFactory.CreateText(parent, text, 22, Palette.TextDim, TextAnchor.LowerLeft);
            UIFactory.SetPreferredSize(label.gameObject, 352f, 30f);
        }

        /// <summary>一行"标签  [-]  数值  [+]"，返回显示数值的文字。</summary>
        private static Text SizeRow(Transform parent, string label, Action onMinus, Action onPlus)
        {
            RectTransform row = UIFactory.CreateRow(parent, 12f);
            Text name = UIFactory.CreateText(row, label, 28, Palette.Text, TextAnchor.MiddleLeft);
            UIFactory.SetPreferredSize(name.gameObject, 120f, 52f);
            UIFactory.CreateButton(row, "-", onMinus, 64f, 52f, false, 32);
            Text value = UIFactory.CreateText(row, "", 30, Palette.Text);
            UIFactory.SetPreferredSize(value.gameObject, 64f, 52f);
            UIFactory.CreateButton(row, "+", onPlus, 64f, 52f, false, 32);
            return value;
        }
    }
}
