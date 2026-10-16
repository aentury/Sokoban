using System;
using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.Game
{
    /// <summary>
    /// 各个界面。每个界面只负责搭建自己的控件、显示数据，并把按钮点击转发给传入的回调；
    /// 界面之间怎么跳转由 App 的状态机决定。
    /// </summary>
    public abstract class ScreenBase
    {
        protected GameObject Root;

        public void SetVisible(bool visible)
        {
            if (Root.activeSelf != visible) Root.SetActive(visible);
        }
    }

    public sealed class MainMenuScreen : ScreenBase
    {
        public MainMenuScreen(Transform canvas, Action onPlay, Action onEditor, Action onSettings, Action onQuit)
        {
            Root = UIFactory.CreateFullScreen(canvas, "Main Menu").gameObject;
            RectTransform column = UIFactory.CreateColumn(Root.transform, 24f);

            Text title = UIFactory.CreateText(column, "推箱子", 128, Palette.Accent);
            title.fontStyle = FontStyle.Bold;
            UIFactory.CreateSpacer(column, 40f);
            UIFactory.CreateButton(column, "开始游戏", onPlay, primary: true);
            UIFactory.CreateButton(column, "关卡编辑器", onEditor);
            UIFactory.CreateButton(column, "设置", onSettings);
            UIFactory.CreateButton(column, "退出游戏", onQuit);
        }
    }

    /// <summary>选关界面：两个分页，"内置关卡"和"我的关卡"（关卡编辑器里保存的自制关卡）。</summary>
    public sealed class LevelSelectScreen : ScreenBase
    {
        private struct LevelButton
        {
            public Button Button;
            public Text Number;
            public Text Status;
        }

        private readonly IReadOnlyList<LevelData> _levels;
        private readonly List<LevelButton> _buttons = new List<LevelButton>();
        private readonly GameObject _builtInPanel;
        private readonly GameObject _customPanel;
        private readonly GameObject _customList;
        private readonly RectTransform _customContent;
        private readonly GameObject _customEmpty;
        private readonly Button _builtInTab;
        private readonly Button _customTab;
        private readonly Action<int> _onPlayCustom;
        private readonly Action<int> _onEditCustom;
        private readonly Action<int> _onDeleteCustom;

        public LevelSelectScreen(Transform canvas, IReadOnlyList<LevelData> levels, Action<int> onSelect,
            Action<int> onPlayCustom, Action<int> onEditCustom, Action<int> onDeleteCustom,
            Action onOpenEditor, Action onBack)
        {
            _levels = levels;
            _onPlayCustom = onPlayCustom;
            _onEditCustom = onEditCustom;
            _onDeleteCustom = onDeleteCustom;
            Root = UIFactory.CreateFullScreen(canvas, "Level Select").gameObject;

            Text title = UIFactory.CreateText(Root.transform, "选择关卡", 72, Palette.Text);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1000f, 100f));

            RectTransform tabs = UIFactory.CreateRow(Root.transform, 16f);
            UIFactory.Place(tabs, new Vector2(0.5f, 1f), new Vector2(0f, -165f), Vector2.zero);
            _builtInTab = UIFactory.CreateButton(tabs, "内置关卡", () => ShowTab(false), 260f, 60f);
            _customTab = UIFactory.CreateButton(tabs, "我的关卡", () => ShowTab(true), 260f, 60f);

            // ---- 内置关卡：按钮网格 ----
            _builtInPanel = UIFactory.CreateFullScreen(Root.transform, "Built-in Levels").gameObject;
            RectTransform grid = UIFactory.CreateGrid(_builtInPanel.transform, new Vector2(250f, 170f), 6, 24f);
            var fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < levels.Count; i++)
            {
                int index = i; // 闭包要捕获循环内的副本，否则所有按钮都会打开最后一关
                Button button = UIFactory.CreateButton(grid, null, () => onSelect(index), 250f, 170f);

                Text number = UIFactory.CreateText(button.transform, (i + 1).ToString(), 56, Palette.Text);
                UIFactory.SetAnchors(number.rectTransform, new Vector2(0f, 0.45f), Vector2.one);
                Text levelName = UIFactory.CreateText(button.transform, levels[i].name, 24, Palette.Text);
                UIFactory.SetAnchors(levelName.rectTransform, new Vector2(0f, 0.25f), new Vector2(1f, 0.45f));
                Text status = UIFactory.CreateText(button.transform, "", 22, Palette.TextDim);
                UIFactory.SetAnchors(status.rectTransform, Vector2.zero, new Vector2(1f, 0.25f));

                _buttons.Add(new LevelButton { Button = button, Number = number, Status = status });
            }

            // ---- 自制关卡：可滚动列表 ----
            _customPanel = UIFactory.CreateFullScreen(Root.transform, "Custom Levels").gameObject;
            RectTransform list = UIFactory.CreateScrollList(_customPanel.transform, out _customContent);
            UIFactory.Place(list, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1300f, 600f));
            _customList = list.gameObject;

            RectTransform empty = UIFactory.CreateColumn(_customPanel.transform, 24f);
            UIFactory.CreateText(empty, "还没有自制关卡", 40, Palette.Text);
            UIFactory.CreateText(empty, "在关卡编辑器中制作并保存后，就会显示在这里。", 28, Palette.TextDim);
            UIFactory.CreateButton(empty, "打开关卡编辑器", onOpenEditor, 400f, 76f, true);
            _customEmpty = empty.gameObject;

            Button back = UIFactory.CreateButton(Root.transform, "返回", onBack, 240f, 72f);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(240f, 72f));

            ShowTab(false);
        }

        public void ShowTab(bool custom)
        {
            _builtInPanel.SetActive(!custom);
            _customPanel.SetActive(custom);
            UIFactory.SetHighlighted(_builtInTab, !custom);
            UIFactory.SetHighlighted(_customTab, custom);
        }

        /// <summary>每次打开选关界面时根据存档刷新锁定状态、最佳步数和自制关卡列表。</summary>
        public void Refresh(SaveData save, IReadOnlyList<LevelData> customLevels)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                LevelButton entry = _buttons[i];
                string id = _levels[i].name;
                bool unlocked = save.IsUnlocked(_levels, i);
                entry.Button.interactable = unlocked;

                if (!unlocked)
                {
                    entry.Status.text = "未解锁";
                    entry.Number.color = Palette.TextDim;
                }
                else if (save.IsCompleted(id))
                {
                    entry.Status.text = $"最佳：{save.GetBestMoves(id)} 步";
                    entry.Number.color = Palette.Success;
                }
                else
                {
                    entry.Status.text = "未通关";
                    entry.Number.color = Palette.Text;
                }
            }

            UIFactory.ClearChildren(_customContent);
            for (int i = 0; i < customLevels.Count; i++)
                CreateCustomRow(i, customLevels[i], save);
            _customList.SetActive(customLevels.Count > 0);
            _customEmpty.SetActive(customLevels.Count == 0);
        }

        private void CreateCustomRow(int index, LevelData level, SaveData save)
        {
            Image row = UIFactory.CreateImage(_customContent, "Row", Palette.Card, true);
            UIFactory.SetPreferredSize(row.gameObject, 0f, 88f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(28, 16, 12, 12);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Text name = UIFactory.CreateText(row.transform, level.name, 34, Palette.Text, TextAnchor.MiddleLeft);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            int best = save.GetBestMoves(CustomLevelStore.RecordId(level.name));
            Text bestText = UIFactory.CreateText(row.transform, best > 0 ? $"最佳：{best} 步" : "未通关", 26,
                best > 0 ? Palette.Success : Palette.TextDim, TextAnchor.MiddleRight);
            UIFactory.SetPreferredSize(bestText.gameObject, 220f, 64f);

            UIFactory.CreateButton(row.transform, "开始", () => _onPlayCustom(index), 140f, 64f, true, 28);
            UIFactory.CreateButton(row.transform, "编辑", () => _onEditCustom(index), 140f, 64f, false, 28);

            // 删除需要点两次确认，防止误删
            Button delete = null;
            bool armed = false;
            delete = UIFactory.CreateButton(row.transform, "删除", () =>
            {
                if (armed)
                {
                    _onDeleteCustom(index);
                    return;
                }
                armed = true;
                UIFactory.SetButtonLabel(delete, "确定删除？");
            }, 140f, 64f, false, 28);
        }
    }

    public sealed class HudScreen : ScreenBase
    {
        private readonly Text _title;
        private readonly Text _stats;
        private readonly Button _undo;
        private readonly Button _menu;

        public HudScreen(Transform canvas, Action onUndo, Action onRestart, Action onMenu)
        {
            Root = UIFactory.CreateFullScreen(canvas, "HUD").gameObject;

            Image bar = UIFactory.CreateImage(Root.transform, "Top Bar", Palette.Bar);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(0f, -110f);
            barRect.offsetMax = Vector2.zero;

            _title = UIFactory.CreateText(barRect, "", 36, Palette.Text, TextAnchor.MiddleLeft);
            UIFactory.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -12f), new Vector2(800f, 46f));
            _stats = UIFactory.CreateText(barRect, "", 28, Palette.TextDim, TextAnchor.MiddleLeft);
            UIFactory.Place(_stats.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 12f), new Vector2(800f, 36f));

            RectTransform buttons = UIFactory.CreateRow(barRect, 12f);
            UIFactory.Place(buttons, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), Vector2.zero);
            _undo = UIFactory.CreateButton(buttons, "撤销 (Z)", onUndo, 190f, 64f);
            UIFactory.CreateButton(buttons, "重开 (R)", onRestart, 190f, 64f);
            _menu = UIFactory.CreateButton(buttons, "菜单 (Esc)", onMenu, 230f, 64f);

            Text hint = UIFactory.CreateText(Root.transform,
                "移动：方向键 / WASD      撤销：Z      重开：R      暂停：Esc", 26, Palette.TextDim);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1400f, 40f));
        }

        public void SetLevel(string title) => _title.text = title;

        /// <summary>正常游玩时是"菜单 (Esc)"，从编辑器试玩时是"编辑器 (Esc)"。</summary>
        public void SetMenuLabel(string label) => UIFactory.SetButtonLabel(_menu, label);

        public void SetStats(int moves, int pushes, bool canUndo)
        {
            _stats.text = $"步数  {moves}        推箱  {pushes}";
            _undo.interactable = canUndo;
        }
    }

    public sealed class PauseScreen : ScreenBase
    {
        public PauseScreen(Transform canvas, Action onResume, Action onRestart, Action onLevelSelect, Action onMainMenu)
        {
            RectTransform overlay = UIFactory.CreateOverlay(canvas, "Pause");
            Root = overlay.gameObject;
            RectTransform card = UIFactory.CreateCard(overlay);

            UIFactory.CreateText(card, "已暂停", 64, Palette.Text);
            UIFactory.CreateSpacer(card, 10f);
            UIFactory.CreateButton(card, "继续游戏", onResume, primary: true);
            UIFactory.CreateButton(card, "重新开始", onRestart);
            UIFactory.CreateButton(card, "选择关卡", onLevelSelect);
            UIFactory.CreateButton(card, "主菜单", onMainMenu);
        }
    }

    public sealed class WinScreen : ScreenBase
    {
        private readonly Text _title;
        private readonly Text _moves;
        private readonly Text _best;
        private readonly Button _next;

        public WinScreen(Transform canvas, Action onNext, Action onReplay, Action onLevelSelect)
        {
            RectTransform overlay = UIFactory.CreateOverlay(canvas, "Win");
            Root = overlay.gameObject;
            RectTransform card = UIFactory.CreateCard(overlay);

            _title = UIFactory.CreateText(card, "", 64, Palette.Accent);
            _moves = UIFactory.CreateText(card, "", 38, Palette.Text);
            _best = UIFactory.CreateText(card, "", 30, Palette.TextDim);
            UIFactory.CreateSpacer(card, 10f);
            _next = UIFactory.CreateButton(card, "下一关 (Enter)", onNext, 420f, 76f, true);
            UIFactory.CreateButton(card, "再玩一次", onReplay, 420f, 76f);
            UIFactory.CreateButton(card, "选择关卡", onLevelSelect, 420f, 76f);
        }

        public void Show(string title, int moves, int bestMoves, bool isNewBest, bool hasNextLevel)
        {
            _title.text = title;
            _moves.text = $"步数：{moves}";
            _best.text = isNewBest ? "新纪录！" : $"最佳：{bestMoves} 步";
            _best.color = isNewBest ? Palette.Success : Palette.TextDim;
            _next.gameObject.SetActive(hasNextLevel);
        }
    }

    public sealed class SettingsScreen : ScreenBase
    {
        private readonly Text _volumeValue;

        public SettingsScreen(Transform canvas, float volume, Action<float> onVolumeChanged, Action onBack)
        {
            Root = UIFactory.CreateFullScreen(canvas, "Settings").gameObject;
            RectTransform card = UIFactory.CreateCard(Root.transform);

            UIFactory.CreateText(card, "设置", 64, Palette.Text);
            UIFactory.CreateSpacer(card, 10f);

            RectTransform row = UIFactory.CreateRow(card, 24f);
            UIFactory.CreateText(row, "音量", 34, Palette.Text);
            UIFactory.CreateSlider(row, volume, value =>
            {
                _volumeValue.text = ToPercent(value);
                onVolumeChanged(value);
            });
            _volumeValue = UIFactory.CreateText(row, ToPercent(volume), 34, Palette.TextDim);
            UIFactory.SetPreferredSize(_volumeValue.gameObject, 100f, 40f);

            UIFactory.CreateSpacer(card, 10f);
            UIFactory.CreateButton(card, "返回", onBack, primary: true);
        }

        private static string ToPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
