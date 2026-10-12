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
        public MainMenuScreen(Transform canvas, Action onPlay, Action onSettings, Action onQuit)
        {
            Root = UIFactory.CreateFullScreen(canvas, "Main Menu").gameObject;
            RectTransform column = UIFactory.CreateColumn(Root.transform, 24f);

            Text title = UIFactory.CreateText(column, "SOKOBAN", 128, Palette.Accent);
            title.fontStyle = FontStyle.Bold;
            UIFactory.CreateText(column, "Push every crate onto a target.", 34, Palette.TextDim);
            UIFactory.CreateSpacer(column, 40f);
            UIFactory.CreateButton(column, "Play", onPlay, primary: true);
            UIFactory.CreateButton(column, "Settings", onSettings);
            UIFactory.CreateButton(column, "Quit", onQuit);
        }
    }

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

        public LevelSelectScreen(Transform canvas, IReadOnlyList<LevelData> levels, Action<int> onSelect, Action onBack)
        {
            _levels = levels;
            Root = UIFactory.CreateFullScreen(canvas, "Level Select").gameObject;

            Text title = UIFactory.CreateText(Root.transform, "Select Level", 72, Palette.Text);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 100f));

            RectTransform grid = UIFactory.CreateRect("Grid", Root.transform);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(250f, 170f);
            layout.spacing = new Vector2(24f, 24f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.MiddleCenter;
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

            Button back = UIFactory.CreateButton(Root.transform, "Back", onBack, 240f, 72f);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(240f, 72f));
        }

        /// <summary>每次打开选关界面时根据存档刷新锁定状态和最佳步数。</summary>
        public void Refresh(SaveData save)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                LevelButton entry = _buttons[i];
                string id = _levels[i].name;
                bool unlocked = save.IsUnlocked(_levels, i);
                entry.Button.interactable = unlocked;

                if (!unlocked)
                {
                    entry.Status.text = "Locked";
                    entry.Number.color = Palette.TextDim;
                }
                else if (save.IsCompleted(id))
                {
                    entry.Status.text = $"Best: {save.GetBestMoves(id)} moves";
                    entry.Number.color = Palette.Success;
                }
                else
                {
                    entry.Status.text = "Not cleared";
                    entry.Number.color = Palette.Text;
                }
            }
        }
    }

    public sealed class HudScreen : ScreenBase
    {
        private readonly Text _title;
        private readonly Text _stats;
        private readonly Button _undo;

        public HudScreen(Transform canvas, Action onUndo, Action onRestart, Action onPause)
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
            _undo = UIFactory.CreateButton(buttons, "Undo (Z)", onUndo, 190f, 64f);
            UIFactory.CreateButton(buttons, "Restart (R)", onRestart, 210f, 64f);
            UIFactory.CreateButton(buttons, "Menu (Esc)", onPause, 210f, 64f);

            Text hint = UIFactory.CreateText(Root.transform,
                "Move: Arrow keys / WASD      Undo: Z      Restart: R      Pause: Esc", 26, Palette.TextDim);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1400f, 40f));
        }

        public void SetLevel(string title) => _title.text = title;

        public void SetStats(int moves, int pushes, bool canUndo)
        {
            _stats.text = $"Moves  {moves}        Pushes  {pushes}";
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

            UIFactory.CreateText(card, "Paused", 64, Palette.Text);
            UIFactory.CreateSpacer(card, 10f);
            UIFactory.CreateButton(card, "Resume", onResume, primary: true);
            UIFactory.CreateButton(card, "Restart", onRestart);
            UIFactory.CreateButton(card, "Level Select", onLevelSelect);
            UIFactory.CreateButton(card, "Main Menu", onMainMenu);
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
            _next = UIFactory.CreateButton(card, "Next Level (Enter)", onNext, 420f, 76f, true);
            UIFactory.CreateButton(card, "Replay", onReplay, 420f, 76f);
            UIFactory.CreateButton(card, "Level Select", onLevelSelect, 420f, 76f);
        }

        public void Show(int moves, int bestMoves, bool isNewBest, bool hasNextLevel)
        {
            _title.text = hasNextLevel ? "Level Complete!" : "All Levels Complete!";
            _moves.text = $"Moves: {moves}";
            _best.text = isNewBest ? "New best!" : $"Best: {bestMoves}";
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

            UIFactory.CreateText(card, "Settings", 64, Palette.Text);
            UIFactory.CreateSpacer(card, 10f);

            RectTransform row = UIFactory.CreateRow(card, 24f);
            UIFactory.CreateText(row, "Volume", 34, Palette.Text);
            UIFactory.CreateSlider(row, volume, value =>
            {
                _volumeValue.text = ToPercent(value);
                onVolumeChanged(value);
            });
            _volumeValue = UIFactory.CreateText(row, ToPercent(volume), 34, Palette.TextDim);
            UIFactory.SetPreferredSize(_volumeValue.gameObject, 100f, 40f);

            UIFactory.CreateSpacer(card, 10f);
            UIFactory.CreateButton(card, "Back", onBack, primary: true);
        }

        private static string ToPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
