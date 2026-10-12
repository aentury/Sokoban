using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 游戏入口，挂在场景里唯一的 App 物体上。
    /// 负责创建各个模块，并用一个简单的状态机管理界面流程：
    ///   MainMenu → LevelSelect → Playing ⇄ Paused
    ///                               ↓
    ///                              Won → 下一关 / 重玩 / 选关
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
            Settings
        }

        private List<LevelData> _levels;
        private SaveData _save;
        private GameController _game;

        private HudScreen _hud;
        private PauseScreen _pause;
        private WinScreen _win;
        private MainMenuScreen _mainMenu;
        private LevelSelectScreen _levelSelect;
        private SettingsScreen _settings;

        private State _state;
        private int _levelIndex;

        private bool HasNextLevel => _levelIndex + 1 < _levels.Count;

        private void Awake()
        {
            _levels = LevelLibrary.LoadBuiltIn();
            _save = SaveSystem.Load();
            AudioListener.volume = _save.volume;

            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Init(SetupCamera());
            _game.StateChanged += RefreshHud;
            _game.Won += OnLevelWon;

            // 创建顺序 = 绘制顺序，后创建的显示在上层（暂停、通关面板要盖住 HUD）
            Transform canvas = UIFactory.CreateCanvas(transform).transform;
            _hud = new HudScreen(canvas, _game.Undo, _game.Restart, () => SetState(State.Paused));
            _pause = new PauseScreen(canvas,
                () => SetState(State.Playing),
                () => { _game.Restart(); SetState(State.Playing); },
                () => SetState(State.LevelSelect),
                () => SetState(State.MainMenu));
            _win = new WinScreen(canvas, PlayNextLevel, () => PlayLevel(_levelIndex), () => SetState(State.LevelSelect));
            _mainMenu = new MainMenuScreen(canvas, () => SetState(State.LevelSelect), () => SetState(State.Settings), Quit);
            _levelSelect = new LevelSelectScreen(canvas, _levels, PlayLevel, () => SetState(State.MainMenu));
            _settings = new SettingsScreen(canvas, _save.volume, OnVolumeChanged, CloseSettings);

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
        }

        private void SetState(State state)
        {
            _state = state;

            bool inLevel = state == State.Playing || state == State.Paused || state == State.Won;
            if (!inLevel) _game.Unload();

            _hud.SetVisible(inLevel);
            _pause.SetVisible(state == State.Paused);
            _win.SetVisible(state == State.Won);
            _mainMenu.SetVisible(state == State.MainMenu);
            _levelSelect.SetVisible(state == State.LevelSelect);
            _settings.SetVisible(state == State.Settings);

            _game.InputEnabled = state == State.Playing;
            if (state == State.LevelSelect) _levelSelect.Refresh(_save);
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
            }
        }

        private void PlayLevel(int index)
        {
            _levelIndex = index;
            _game.Load(_levels[index]);
            _hud.SetLevel($"{index + 1}. {_levels[index].name}");
            SetState(State.Playing);
        }

        private void PlayNextLevel()
        {
            if (HasNextLevel) PlayLevel(_levelIndex + 1);
        }

        private void OnLevelWon(int moves)
        {
            string levelId = _levels[_levelIndex].name;
            bool isNewBest = _save.RecordWin(levelId, moves);
            SaveSystem.Save(_save);

            _win.Show(moves, _save.GetBestMoves(levelId), isNewBest, HasNextLevel);
            SetState(State.Won);
        }

        private void RefreshHud()
        {
            Board board = _game.Board;
            if (board != null) _hud.SetStats(board.MoveCount, board.PushCount, board.CanUndo);
        }

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
