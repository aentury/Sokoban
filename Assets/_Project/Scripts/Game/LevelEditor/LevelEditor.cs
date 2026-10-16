using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sokoban.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sokoban.Game
{
    public enum StatusKind
    {
        Info,
        Success,
        Error
    }

    /// <summary>
    /// 关卡编辑器的控制器：持有正在编辑的 LevelGrid，处理鼠标绘制、快捷键、撤销、
    /// 相机适配，以及在后台线程运行求解器。界面（EditorScreen）通过事件得知状态变化。
    /// </summary>
    public sealed class LevelEditor : MonoBehaviour
    {
        /// <summary>左侧工具栏和底部状态栏的尺寸（UI 参考单位），相机要避开这两块区域。</summary>
        public const float PanelWidth = 400f;
        public const float StatusBarHeight = 64f;

        private const int DefaultWidth = 10;
        private const int DefaultHeight = 8;
        private const int MaxUndo = 100;
        private const float CameraMargin = 1.5f;

        private static readonly KeyCode[] ToolKeys =
            { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
        private static readonly EditTool[] ToolOrder =
            { EditTool.Wall, EditTool.Box, EditTool.Goal, EditTool.Player, EditTool.Erase };

        /// <summary>工具、尺寸、可否撤销等变化时触发。</summary>
        public event Action Changed;
        public event Action<string, StatusKind> StatusChanged;

        /// <summary>由界面提供：正在输入关卡名时不响应快捷键。</summary>
        public Func<bool> IsTyping;

        public LevelGrid Grid { get; private set; }
        public bool CanUndo => _undo.Count > 0;

        public EditTool Tool
        {
            get => _tool;
            set
            {
                _tool = value;
                Changed?.Invoke();
            }
        }

        private readonly List<LevelGrid> _undo = new List<LevelGrid>();
        private EditorGridView _view;
        private Camera _camera;
        private Canvas _canvas;
        private EditTool _tool = EditTool.Wall;

        private bool _stroking;
        private EditTool _strokeTool;
        private LevelGrid _strokeBefore;
        private bool _strokeChanged;

        private Task<SolveResult> _solveTask;
        private int _version;          // 每次修改 +1，用来判断求解结果是否已经过期
        private int _solveVersion;

        private int _screenWidth;
        private int _screenHeight;
        private float _canvasScale;

        public void Init(Camera camera, Canvas canvas)
        {
            _camera = camera;
            _canvas = canvas;
            _view = new GameObject("Editor Grid").AddComponent<EditorGridView>();
            _view.transform.SetParent(transform, false);
            NewLevel();
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf == visible) return;
            gameObject.SetActive(visible);
            if (visible) FitCamera();
            else EndStroke();
        }

        // ---------- 编辑操作 ----------

        public void NewLevel()
        {
            SetGrid(LevelGrid.CreateWalled(DefaultWidth, DefaultHeight));
            _undo.Clear();
            Changed?.Invoke();
            ShowStatus("新关卡：画出墙、箱子、目标点和玩家。", StatusKind.Info);
        }

        public void Load(LevelData level)
        {
            SetGrid(LevelGrid.FromRows(level.rows));
            _undo.Clear();
            Changed?.Invoke();
            ShowStatus($"正在编辑「{level.name}」。", StatusKind.Info);
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            LevelGrid previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            SetGrid(previous);
            Changed?.Invoke();
        }

        public void ResizeBy(int deltaWidth, int deltaHeight)
        {
            LevelGrid before = Grid.Clone();
            Grid.Resize(Grid.Width + deltaWidth, Grid.Height + deltaHeight);
            if (Grid.Width == before.Width && Grid.Height == before.Height) return;

            PushUndo(before);
            SetGrid(Grid);
            Changed?.Invoke();
        }

        public LevelData ToLevelData(string levelName) => new LevelData { name = levelName, rows = Grid.ToRows() };

        public List<string> Validate() => LevelValidator.Validate(Grid.ToRows());

        /// <summary>先做结构检查，通过后在后台线程运行求解器，结果在 Update 里取回。</summary>
        public void CheckSolvable()
        {
            List<string> errors = Validate();
            if (errors.Count > 0)
            {
                ShowErrors(errors);
                return;
            }
            if (_solveTask != null) return;

            string[] rows = Grid.ToRows();
            _solveVersion = _version;
            _solveTask = Task.Run(() => Solver.Solve(rows));
            ShowStatus("正在检查关卡是否有解……", StatusKind.Info);
        }

        public void ShowStatus(string message, StatusKind kind) => StatusChanged?.Invoke(message, kind);

        public void ShowErrors(List<string> errors) => ShowStatus(string.Join("  ", errors), StatusKind.Error);

        // ---------- 每帧 ----------

        private void Update()
        {
            if (Screen.width != _screenWidth || Screen.height != _screenHeight ||
                !Mathf.Approximately(_canvas.scaleFactor, _canvasScale))
                FitCamera();

            PollSolver();
            if (IsTyping == null || !IsTyping()) HandleShortcuts();
            HandleMouse();
        }

        private void HandleShortcuts()
        {
            for (int i = 0; i < ToolKeys.Length; i++)
                if (Input.GetKeyDown(ToolKeys[i])) Tool = ToolOrder[i];

            if (Input.GetKeyDown(KeyCode.Z)) Undo();
        }

        /// <summary>
        /// 一次"按下 → 拖动 → 松开"算一笔：左键用当前工具，右键用橡皮擦。
        /// 一笔只记一次撤销，撤销时整笔一起撤回。
        /// </summary>
        private void HandleMouse()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            GridPos? cell = overUI ? null : _view.ScreenToCell(_camera, Input.mousePosition);
            _view.SetCursor(cell);

            bool leftDown = Input.GetMouseButtonDown(0), rightDown = Input.GetMouseButtonDown(1);
            if (!_stroking && (leftDown || rightDown) && cell.HasValue)
            {
                _stroking = true;
                _strokeTool = rightDown ? EditTool.Erase : _tool;
                _strokeBefore = Grid.Clone();
                _strokeChanged = false;
            }

            if (!_stroking) return;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                EndStroke();
                return;
            }

            if (cell.HasValue && Grid.Apply(_strokeTool, cell.Value))
            {
                _strokeChanged = true;
                _version++;
                _view.Refresh();
            }
        }

        private void EndStroke()
        {
            if (!_stroking) return;
            _stroking = false;
            if (!_strokeChanged) return;
            PushUndo(_strokeBefore);
            Changed?.Invoke();
        }

        private void PollSolver()
        {
            if (_solveTask == null || !_solveTask.IsCompleted) return;

            Task<SolveResult> task = _solveTask;
            _solveTask = null;
            if (_solveVersion != _version) return; // 检查期间关卡又被改过，结果作废

            if (task.IsFaulted)
            {
                ShowStatus("检查失败，请重试。", StatusKind.Error);
                return;
            }

            SolveResult result = task.Result;
            switch (result.Status)
            {
                case SolveStatus.Solvable:
                    ShowStatus($"有解！最少需要推 {result.Pushes} 次箱子。", StatusKind.Success);
                    break;
                case SolveStatus.Unsolvable:
                    ShowStatus("无解：这个关卡无法完成。", StatusKind.Error);
                    break;
                default:
                    ShowStatus("关卡太复杂，无法自动检查，请用试玩来验证。", StatusKind.Info);
                    break;
            }
        }

        // ---------- 内部 ----------

        private void SetGrid(LevelGrid grid)
        {
            Grid = grid;
            _version++;
            _view.Build(grid);
            FitCamera();
        }

        private void PushUndo(LevelGrid snapshot)
        {
            _undo.Add(snapshot);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        /// <summary>
        /// 让网格完整显示在"工具栏右边、状态栏上面"的区域中间：
        /// 先算出能放下网格所需的相机大小，再把相机往左下偏移，抵消工具栏和状态栏占掉的空间。
        /// </summary>
        private void FitCamera()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _canvasScale = _canvas.scaleFactor;
            if (Grid == null || !gameObject.activeInHierarchy) return;

            float panel = PanelWidth * _canvasScale;            // 工具栏占的像素
            float statusBar = StatusBarHeight * _canvasScale;   // 状态栏占的像素
            float availableWidth = Mathf.Max(1f, Screen.width - panel);
            float availableHeight = Mathf.Max(1f, Screen.height - statusBar);

            float sizeForWidth = (Grid.Width + CameraMargin) * 0.5f * Screen.height / availableWidth;
            float sizeForHeight = (Grid.Height + CameraMargin) * 0.5f * Screen.height / availableHeight;
            float size = Mathf.Max(sizeForWidth, sizeForHeight);
            float worldPerPixel = 2f * size / Screen.height;

            _camera.orthographicSize = size;
            _camera.transform.position = new Vector3(-panel * 0.5f * worldPerPixel, -statusBar * 0.5f * worldPerPixel, -10f);
        }
    }
}
