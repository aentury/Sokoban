using System;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 一局游戏的控制器：持有 Board，读取键盘输入，驱动 BoardView，并在通关时通知外部。
    /// 输入策略：
    ///  - 动画播放期间按下的方向键会缓存 1 个，动画结束后立即执行（不吞键，也不越积越多）
    ///  - 按住方向键超过 RepeatDelay 后连续行走
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float RepeatDelay = 0.22f;
        private const float WinDelay = 0.6f;
        private const float VerticalMargin = 3f;   // 相机上下留出的格数（给 HUD 留空间）
        private const float HorizontalMargin = 1f;

        private static readonly KeyCode[] MoveKeys =
        {
            KeyCode.UpArrow, KeyCode.W, KeyCode.DownArrow, KeyCode.S,
            KeyCode.LeftArrow, KeyCode.A, KeyCode.RightArrow, KeyCode.D
        };

        private static readonly Direction[] MoveDirections =
        {
            Direction.Up, Direction.Up, Direction.Down, Direction.Down,
            Direction.Left, Direction.Left, Direction.Right, Direction.Right
        };

        /// <summary>步数、可否撤销等状态变化时触发，HUD 据此刷新。</summary>
        public event Action StateChanged;

        /// <summary>通关时触发，参数为步数。</summary>
        public event Action<int> Won;

        public Board Board { get; private set; }
        public bool InputEnabled { get; set; }

        private BoardView _view;
        private Camera _camera;
        private Direction? _buffered;
        private Direction? _held;
        private float _holdTimer;
        private bool _won;
        private float _winTimer;
        private int _screenWidth;
        private int _screenHeight;

        public void Init(Camera camera)
        {
            _camera = camera;
            _view = new GameObject("Board View").AddComponent<BoardView>();
            _view.transform.SetParent(transform, false);
        }

        public void Load(LevelData level)
        {
            Board = LevelParser.Parse(level.rows);
            _view.Build(Board);
            _won = false;
            ResetInput();
            FitCamera();
            StateChanged?.Invoke();
        }

        public void Unload()
        {
            Board = null;
            _view.Clear();
        }

        public void Undo()
        {
            if (Board == null || _won) return;
            if (!Board.Undo()) return;
            _view.SyncToBoard();
            ResetInput();
            StateChanged?.Invoke();
        }

        public void Restart()
        {
            if (Board == null || _won) return;
            Board.Restart();
            _view.SyncToBoard();
            ResetInput();
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (Board == null) return;
            if (Screen.width != _screenWidth || Screen.height != _screenHeight) FitCamera();

            if (_won)
            {
                // 通关后先播放庆祝动画，稍等片刻再通知外部弹出结算面板
                if (_winTimer > 0f)
                {
                    _winTimer -= Time.deltaTime;
                    if (_winTimer <= 0f) Won?.Invoke(Board.MoveCount);
                }
                return;
            }
            if (!InputEnabled) return;

            if (Input.GetKeyDown(KeyCode.Z)) Undo();
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            ReadMovementKeys();

            if (_view.IsAnimating) return;

            // 等最后一步动画播完再判定通关，玩家能看到箱子落到目标点上
            if (Board.IsSolved)
            {
                _won = true;
                _winTimer = WinDelay;
                _view.Celebrate();
                Sfx.Play("win");
                return;
            }

            if (_buffered.HasValue)
            {
                Direction direction = _buffered.Value;
                _buffered = null;
                Move(direction, true);
            }
            else if (_held.HasValue && _holdTimer <= 0f)
            {
                Move(_held.Value, false);
            }
        }

        /// <summary>执行一步并播放对应的动画和音效。撞墙反馈只在"新按下"时播放，按住不放撞墙不会一直响。</summary>
        private void Move(Direction direction, bool freshPress)
        {
            MoveResult result = Board.TryMove(direction);
            switch (result.Kind)
            {
                case MoveKind.Blocked:
                    if (freshPress)
                    {
                        _view.Bump(direction);
                        Sfx.Play("blocked", 0.7f);
                    }
                    return;
                case MoveKind.Walked:
                    Sfx.Play("step", 0.5f);
                    break;
                case MoveKind.Pushed:
                    Sfx.Play(Board.IsGoal(result.BoxTo) ? "goal" : "push");
                    break;
            }
            _view.Animate(result);
            StateChanged?.Invoke();
        }

        private void ReadMovementKeys()
        {
            for (int i = 0; i < MoveKeys.Length; i++)
            {
                if (Input.GetKeyDown(MoveKeys[i]))
                {
                    _buffered = MoveDirections[i];
                    _held = MoveDirections[i];
                    _holdTimer = RepeatDelay;
                }
            }

            if (_held.HasValue && !IsHeld(_held.Value)) _held = null;
            _holdTimer -= Time.deltaTime;
        }

        private static bool IsHeld(Direction direction)
        {
            for (int i = 0; i < MoveKeys.Length; i++)
                if (MoveDirections[i] == direction && Input.GetKey(MoveKeys[i])) return true;
            return false;
        }

        private void ResetInput()
        {
            _buffered = null;
            _held = null;
            _holdTimer = 0f;
        }

        /// <summary>根据关卡尺寸和窗口比例调整正交相机，保证整个关卡都能看到。</summary>
        private void FitCamera()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            if (Board == null) return;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float sizeForHeight = (Board.Height + VerticalMargin) * 0.5f;
            float sizeForWidth = (Board.Width + HorizontalMargin) * 0.5f / aspect;
            _camera.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
            _camera.transform.position = new Vector3(0f, 0f, -10f);
        }
    }
}
