using System;
using System.Collections.Generic;
using System.Text;

namespace Sokoban.Core
{
    public enum SolveStatus
    {
        Solvable,
        Unsolvable,
        TooComplex
    }

    public readonly struct SolveResult
    {
        public readonly SolveStatus Status;
        /// <summary>最少推箱次数，仅在 Solvable 时有意义。</summary>
        public readonly int Pushes;

        public SolveResult(SolveStatus status, int pushes)
        {
            Status = status;
            Pushes = pushes;
        }
    }

    /// <summary>
    /// 推箱子求解器：按"推箱次数"做广度优先搜索，第一个找到的解就是推箱次数最少的解。
    /// 两个优化：
    ///  1. 玩家在同一片连通区域里怎么走都等价，所以一个状态 = 箱子位置 + 玩家所在区域
    ///     （用区域里编号最小的格子代表这片区域）
    ///  2. 箱子被推进"不是目标点的死角"后再也推不出来，直接跳过
    /// 状态数超过 maxStates 时放弃并返回 TooComplex，避免复杂关卡算太久。
    /// 格子用一维编号表示：index = y * width + x。
    /// </summary>
    public static class Solver
    {
        private sealed class Node
        {
            public int[] Boxes;   // 排好序的箱子格子编号
            public int Player;
            public int Pushes;
        }

        private static readonly int[] DX = { 0, 0, -1, 1 };
        private static readonly int[] DY = { -1, 1, 0, 0 };

        public static SolveResult Solve(string[] rows, int maxStates = 200000)
        {
            Board board = LevelParser.Parse(rows);
            var search = new Search(board);
            return search.Run(maxStates);
        }

        private sealed class Search
        {
            private readonly int _width;
            private readonly int _height;
            private readonly bool[] _wall;
            private readonly bool[] _goal;
            private readonly bool[] _dead;
            private readonly bool[] _hasBox;
            private readonly bool[] _blocked;
            private readonly bool[] _nodeReach;
            private readonly bool[] _childReach;
            private readonly Stack<int> _stack = new Stack<int>();
            private readonly Node _start;

            public Search(Board board)
            {
                _width = board.Width;
                _height = board.Height;
                int size = _width * _height;
                _wall = new bool[size];
                _goal = new bool[size];
                _hasBox = new bool[size];
                _blocked = new bool[size];
                _nodeReach = new bool[size];
                _childReach = new bool[size];

                for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    var p = new GridPos(x, y);
                    _wall[Index(x, y)] = board.IsWall(p);
                    _goal[Index(x, y)] = board.IsGoal(p);
                }
                _dead = FindDeadCorners();

                var boxes = new List<int>();
                foreach (GridPos p in board.Boxes) boxes.Add(Index(p.X, p.Y));
                boxes.Sort();
                _start = new Node { Boxes = boxes.ToArray(), Player = Index(board.Player.X, board.Player.Y) };
            }

            public SolveResult Run(int maxStates)
            {
                var visited = new HashSet<string> { Key(_start.Boxes, Flood(_start.Boxes, _start.Player, _nodeReach)) };
                var queue = new Queue<Node>();
                queue.Enqueue(_start);

                while (queue.Count > 0)
                {
                    Node node = queue.Dequeue();
                    if (AllOnGoals(node.Boxes)) return new SolveResult(SolveStatus.Solvable, node.Pushes);

                    Flood(node.Boxes, node.Player, _nodeReach);
                    foreach (int b in node.Boxes) _hasBox[b] = true;

                    for (int i = 0; i < node.Boxes.Length; i++)
                    {
                        int bx = node.Boxes[i] % _width, by = node.Boxes[i] / _width;
                        for (int d = 0; d < 4; d++)
                        {
                            int sx = bx - DX[d], sy = by - DY[d];   // 推箱时玩家站的位置
                            int tx = bx + DX[d], ty = by + DY[d];   // 箱子被推到的位置
                            if (!Inside(sx, sy) || !Inside(tx, ty)) continue;
                            int stand = Index(sx, sy), target = Index(tx, ty);
                            if (!_nodeReach[stand] || _wall[target] || _hasBox[target] || _dead[target]) continue;

                            var boxes = (int[])node.Boxes.Clone();
                            boxes[i] = target;
                            Array.Sort(boxes);
                            int player = node.Boxes[i]; // 推完后玩家站在箱子原来的位置

                            if (!visited.Add(Key(boxes, Flood(boxes, player, _childReach)))) continue;
                            if (visited.Count > maxStates)
                                return new SolveResult(SolveStatus.TooComplex, 0);
                            queue.Enqueue(new Node { Boxes = boxes, Player = player, Pushes = node.Pushes + 1 });
                        }
                    }

                    foreach (int b in node.Boxes) _hasBox[b] = false;
                }
                return new SolveResult(SolveStatus.Unsolvable, 0);
            }

            /// <summary>把玩家可达的格子写进 reach，返回其中编号最小的格子。</summary>
            private int Flood(int[] boxes, int player, bool[] reach)
            {
                Array.Clear(reach, 0, reach.Length);
                foreach (int b in boxes) _blocked[b] = true;

                int min = player;
                reach[player] = true;
                _stack.Clear();
                _stack.Push(player);
                while (_stack.Count > 0)
                {
                    int cell = _stack.Pop();
                    if (cell < min) min = cell;
                    int x = cell % _width, y = cell / _width;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + DX[d], ny = y + DY[d];
                        if (!Inside(nx, ny)) continue;
                        int next = Index(nx, ny);
                        if (reach[next] || _wall[next] || _blocked[next]) continue;
                        reach[next] = true;
                        _stack.Push(next);
                    }
                }

                foreach (int b in boxes) _blocked[b] = false;
                return min;
            }

            /// <summary>上下至少一面墙、左右至少一面墙（即拐角），并且不是目标点。</summary>
            private bool[] FindDeadCorners()
            {
                var dead = new bool[_wall.Length];
                for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    int i = Index(x, y);
                    if (_wall[i] || _goal[i]) continue;
                    bool vertical = IsWall(x, y - 1) || IsWall(x, y + 1);
                    bool horizontal = IsWall(x - 1, y) || IsWall(x + 1, y);
                    dead[i] = vertical && horizontal;
                }
                return dead;
            }

            private bool AllOnGoals(int[] boxes)
            {
                foreach (int b in boxes)
                    if (!_goal[b]) return false;
                return true;
            }

            private bool IsWall(int x, int y) => !Inside(x, y) || _wall[Index(x, y)];
            private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;
            private int Index(int x, int y) => y * _width + x;

            private static string Key(int[] boxes, int region)
            {
                var sb = new StringBuilder(boxes.Length * 4 + 4);
                sb.Append(region);
                foreach (int b in boxes) sb.Append(',').Append(b);
                return sb.ToString();
            }
        }
    }
}
