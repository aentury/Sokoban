using System.Collections.Generic;

namespace Sokoban.Core
{
    /// <summary>
    /// 推箱子的纯逻辑状态：地形、目标点、箱子、玩家、移动历史。
    /// 不依赖 UnityEngine，通过 LevelParser.Parse 创建。
    /// </summary>
    public sealed class Board
    {
        private readonly bool[,] _walls;
        private readonly bool[,] _goals;
        private readonly bool[,] _boxes;
        private readonly GridPos[] _startBoxes;
        private readonly GridPos _startPlayer;
        private readonly Stack<MoveResult> _history = new Stack<MoveResult>();

        public int Width { get; }
        public int Height { get; }
        public int BoxCount => _startBoxes.Length;
        public int GoalCount { get; }
        public GridPos Player { get; private set; }
        public int MoveCount => _history.Count;
        public int PushCount { get; private set; }
        public bool CanUndo => _history.Count > 0;

        internal Board(bool[,] walls, bool[,] goals, IReadOnlyList<GridPos> boxes, GridPos player)
        {
            Width = walls.GetLength(0);
            Height = walls.GetLength(1);
            _walls = walls;
            _goals = goals;
            _boxes = new bool[Width, Height];
            _startBoxes = new GridPos[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                _startBoxes[i] = boxes[i];
                _boxes[boxes[i].X, boxes[i].Y] = true;
            }
            _startPlayer = player;
            Player = player;
            foreach (bool goal in goals)
                if (goal) GoalCount++;
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        /// <summary>棋盘外一律视为墙。</summary>
        public bool IsWall(GridPos p) => !InBounds(p) || _walls[p.X, p.Y];
        public bool IsGoal(GridPos p) => InBounds(p) && _goals[p.X, p.Y];
        public bool HasBox(GridPos p) => InBounds(p) && _boxes[p.X, p.Y];

        public IEnumerable<GridPos> Boxes
        {
            get
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_boxes[x, y]) yield return new GridPos(x, y);
            }
        }

        public bool IsSolved
        {
            get
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_boxes[x, y] && !_goals[x, y]) return false;
                return true;
            }
        }

        /// <summary>
        /// 从玩家初始位置出发、不穿墙能到达的所有格子（忽略箱子），即关卡的"室内"区域。
        /// 表现层只给这些格子铺地板，墙外的空白不画。
        /// </summary>
        public bool[,] GetInterior()
        {
            var inside = new bool[Width, Height];
            var stack = new Stack<GridPos>();
            stack.Push(_startPlayer);
            while (stack.Count > 0)
            {
                GridPos p = stack.Pop();
                if (IsWall(p) || inside[p.X, p.Y]) continue;
                inside[p.X, p.Y] = true;
                stack.Push(p + Direction.Up.ToOffset());
                stack.Push(p + Direction.Down.ToOffset());
                stack.Push(p + Direction.Left.ToOffset());
                stack.Push(p + Direction.Right.ToOffset());
            }
            return inside;
        }

        public MoveResult TryMove(Direction direction)
        {
            GridPos offset = direction.ToOffset();
            GridPos from = Player;
            GridPos to = from + offset;

            if (IsWall(to)) return MoveResult.Blocked(from);

            MoveResult result;
            if (HasBox(to))
            {
                GridPos boxTo = to + offset;
                if (IsWall(boxTo) || HasBox(boxTo)) return MoveResult.Blocked(from);

                _boxes[to.X, to.Y] = false;
                _boxes[boxTo.X, boxTo.Y] = true;
                PushCount++;
                result = MoveResult.Pushed(from, to, to, boxTo);
            }
            else
            {
                result = MoveResult.Walked(from, to);
            }

            Player = to;
            _history.Push(result);
            return result;
        }

        /// <summary>撤销上一步。没有可撤销的步骤时返回 false。</summary>
        public bool Undo()
        {
            if (_history.Count == 0) return false;

            MoveResult last = _history.Pop();
            if (last.Kind == MoveKind.Pushed)
            {
                _boxes[last.BoxTo.X, last.BoxTo.Y] = false;
                _boxes[last.BoxFrom.X, last.BoxFrom.Y] = true;
                PushCount--;
            }
            Player = last.PlayerFrom;
            return true;
        }

        /// <summary>回到关卡初始状态，并清空移动历史。</summary>
        public void Restart()
        {
            System.Array.Clear(_boxes, 0, _boxes.Length);
            foreach (GridPos box in _startBoxes) _boxes[box.X, box.Y] = true;
            Player = _startPlayer;
            PushCount = 0;
            _history.Clear();
        }
    }
}
