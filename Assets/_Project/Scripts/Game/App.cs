using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.Game
{
    /// <summary>
    /// 游戏入口，挂在场景里唯一的 App 物体上。
    /// 负责创建各个模块，并用一个状态机管理界面流程：
    ///
    ///   MainMenu ─→ LevelSelect ─→ Playing ⇄ Paused
    ///      │             ↑            ↓
    ///      │             └────────── Won → 下一关 / 重玩 / 选关
    ///      ├─→ Editor ⇄ EditorTest（试玩，通关或按 Esc 回到编辑器）
    ///      └─→ Settings
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        private enum State
        {
            MainMenu,
            LevelSelect,
            Playing,
            Paused,
            Won,
            Settings,
            Editor,
            EditorTest
        }

        private const float FadeDuration = 0.25f;

        private List<LevelData> _builtInLevels;
        private List<LevelData> _customLevels;
        private List<LevelData> _playlist;   // 当前在玩的是哪一组关卡
        private bool _playingCustom;
        private int _levelIndex;

        private SaveData _save;
        private GameController _game;
        private LevelEditor _editor;

        private HudScreen _hud;
        private PauseScreen _pause;
        private WinScreen _win;
        private MainMenuScreen _mainMenu;
        private LevelSelectScreen _levelSelect;
        private SettingsScreen _settings;
        private EditorScreen _editorScreen;
        private Image _fader;
        private float _fade;

        private State _state;

        private bool HasNextLevel => !_playingCustom && _levelIndex + 1 < _playlist.Count;

        private void Awake()
        {
            _builtInLevels = LevelLibrary.LoadBuiltIn();
            _customLevels = CustomLevelStore.LoadAll();
            _save = SaveSystem.Load();
            AudioListener.volume = _save.volume;
            Sfx.Init(gameObject);

            Camera camera = SetupCamera();
            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Init(camera);
            _game.StateChanged += RefreshHud;
            _game.Won += OnLevelWon;

            Canvas canvas = UIFactory.CreateCanvas(transform);
            Transform root = canvas.transform;

            _editor = new GameObject("Level Editor").AddComponent<LevelEditor>();
            _editor.Init(camera, canvas);

            // 创建顺序 = 绘制顺序，后创建的显示在上层（暂停、通关面板要盖住 HUD）
            _hud = new HudScreen(root, _game.Undo, _game.Restart, OnHudMenu);
            _pause = new PauseScreen(root,
                () => SetState(State.Playing),
                () => { _game.Restart(); SetState(State.Playing); },
                () => SetState(State.LevelSelect),
                () => SetState(State.MainMenu));
            _win = new WinScreen(root, PlayNextLevel, () => PlayLevel(_levelIndex), () => SetState(State.LevelSelect));
            _mainMenu = new MainMenuScreen(root,
                () => SetState(State.LevelSelect),
                () => SetState(State.Editor),
                () => SetState(State.Settings),
                Quit);
            _levelSelect = new LevelSelectScreen(root, _builtInLevels, PlayBuiltInLevel,
                PlayCustomLevel, EditCustomLevel, DeleteCustomLevel,
                () => SetState(State.Editor),
                () => SetState(State.MainMenu));
            _settings = new SettingsScreen(root, _save.volume, OnVolumeChanged, CloseSettings);
            _editorScreen = new EditorScreen(root, _editor, SaveEditorLevel, TestEditorLevel, () => SetState(State.MainMenu));
            _editor.IsTyping = () => _editorScreen.IsTyping;

            // 最上层的淡入遮罩，切换界面时从背景色淡出
            _fader = UIFactory.CreateImage(root, "Fader", Palette.Background);
            UIFactory.Stretch(_fader.rectTransform);
            _fader.raycastTarget = false;

            SetState(State.MainMenu);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) OnBack();

            if (_state == State.Won &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                if (HasNextLevel) PlayNextLevel();
                else SetState(State.LevelSelect);
            }

            if (_fade > 0f) _fade = Mathf.Max(0f, _fade - Time.unscaledDeltaTime / FadeDuration);
            _fader.enabled = _fade > 0f;
            _fader.color = new Color(Palette.Background.r, Palette.Background.g, Palette.Background.b, _fade);
        }

        private void SetState(State state)
        {
            State previous = _state;
            _state = state;

            bool inLevel = state == State.Playing || state == State.Paused || state == State.Won || state == State.EditorTest;
            if (!inLevel) _game.Unload();

            _hud.SetVisible(inLevel);
            _pause.SetVisible(state == State.Paused);
            _win.SetVisible(state == State.Won);
            _mainMenu.SetVisible(state == State.MainMenu);
            _levelSelect.SetVisible(state == State.LevelSelect);
            _settings.SetVisible(state == State.Settings);
            _editorScreen.SetVisible(state == State.Editor);
            _editor.SetVisible(state == State.Editor);

            _game.InputEnabled = state == State.Playing || state == State.EditorTest;
            if (state == State.LevelSelect) _levelSelect.Refresh(_save, _customLevels);

            // 弹出暂停 / 结算面板、从暂停返回游戏时不淡入，其余界面切换都淡入
            bool overlayOnly = state == State.Paused || state == State.Won || (previous == State.Paused && state == State.Playing);
            if (!overlayOnly) _fade = 1f;
        }

        /// <summary>Esc 键：根据当前界面返回上一层。</summary>
        private void OnBack()
        {
            switch (_state)
            {
                case State.Playing: SetState(State.Paused); break;
                case State.Paused: SetState(State.Playing); break;
                case State.Won: SetState(State.LevelSelect); break;
                case State.LevelSelect: SetState(State.MainMenu); break;
                case State.Settings: CloseSettings(); break;
                case State.EditorTest: SetState(State.Editor); break;
                case State.Editor:
                    if (!_editorScreen.IsTyping) SetState(State.MainMenu);
                    break;
            }
        }

        private void OnHudMenu()
        {
            SetState(_state == State.EditorTest ? State.Editor : State.Paused);
        }

        // ---------- 游玩 ----------

        private void PlayBuiltInLevel(int index)
        {
            _playlist = _builtInLevels;
            _playingCustom = false;
            PlayLevel(index);
        }

        private void PlayCustomLevel(int index)
        {
            _playlist = _customLevels;
            _playingCustom = true;
            PlayLevel(index);
        }

        private void PlayLevel(int index)
        {
            _levelIndex = index;
            LevelData level = _playlist[index];
            _game.Load(level);
            _hud.SetLevel(_playingCustom ? level.name : $"{index + 1}. {level.name}");
            _hud.SetMenuLabel("菜单 (Esc)");
            SetState(State.Playing);
        }

        private void PlayNextLevel()
        {
            if (HasNextLevel) PlayLevel(_levelIndex + 1);
        }

        private void OnLevelWon(int moves)
        {
            if (_state == State.EditorTest)
            {
                _editor.ShowStatus($"用 {moves} 步通关，关卡可以正常游玩！满意的话记得保存。", StatusKind.Success);
                SetState(State.Editor);
                return;
            }

            LevelData level = _playlist[_levelIndex];
            string recordId = _playingCustom ? CustomLevelStore.RecordId(level.name) : level.name;
            bool isNewBest = _save.RecordWin(recordId, moves);
            SaveSystem.Save(_save);

            bool lastBuiltIn = !_playingCustom && !HasNextLevel;
            _win.Show(lastBuiltIn ? "全部通关！" : "过关！",
                moves, _save.GetBestMoves(recordId), isNewBest, HasNextLevel);
            SetState(State.Won);
        }

        private void RefreshHud()
        {
            Board board = _game.Board;
            if (board != null) _hud.SetStats(board.MoveCount, board.PushCount, board.CanUndo);
        }

        // ---------- 自制关卡 ----------

        private void EditCustomLevel(int index)
        {
            LevelData level = _customLevels[index];
            _editor.Load(level);
            _editorScreen.LevelName = level.name;
            SetState(State.Editor);
        }

        private void DeleteCustomLevel(int index)
        {
            CustomLevelStore.Delete(_customLevels[index].name);
            _customLevels = CustomLevelStore.LoadAll();
            _levelSelect.Refresh(_save, _customLevels);
        }

        private void SaveEditorLevel()
        {
            string levelName = _editorScreen.LevelName;
            if (string.IsNullOrEmpty(levelName))
            {
                _editor.ShowStatus("保存前请先输入关卡名称。", StatusKind.Error);
                return;
            }

            List<string> errors = _editor.Validate();
            if (errors.Count > 0)
            {
                _editor.ShowErrors(errors);
                return;
            }

            bool replaced = CustomLevelStore.Exists(levelName);
            if (!CustomLevelStore.Save(_editor.ToLevelData(levelName)))
            {
                _editor.ShowStatus("保存失败。", StatusKind.Error);
                return;
            }

            _customLevels = CustomLevelStore.LoadAll();
            _editor.ShowStatus(replaced
                ? $"已保存「{levelName}」（覆盖了之前的版本）。"
                : $"已保存「{levelName}」，可以在 开始游戏 > 我的关卡 中游玩。", StatusKind.Success);
        }

        private void TestEditorLevel()
        {
            List<string> errors = _editor.Validate();
            if (errors.Count > 0)
            {
                _editor.ShowErrors(errors);
                return;
            }

            string levelName = string.IsNullOrEmpty(_editorScreen.LevelName) ? "未命名" : _editorScreen.LevelName;
            _game.Load(_editor.ToLevelData(levelName));
            _hud.SetLevel("试玩：" + levelName);
            _hud.SetMenuLabel("编辑器 (Esc)");
            SetState(State.EditorTest);
        }

        // ---------- 设置 / 其他 ----------

        private void OnVolumeChanged(float volume)
        {
            AudioListener.volume = volume;
            _save.volume = volume;
        }

        private void CloseSettings()
        {
            SaveSystem.Save(_save);
            SetState(State.MainMenu);
        }

        private static Camera SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.gameObject.AddComponent<AudioListener>();
            }
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;
            return camera;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
