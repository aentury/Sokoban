using System;
using System.Collections.Generic;
using System.Text;

namespace Sokoban.Core
{
    public enum EditTool
    {
        Wall,
        Box,
        Goal,
        Player,
        Erase
    }

    /// <summary>
    /// 关卡编辑器里正在编辑的网格。每个格子直接存一个 XSB 字符，
    /// 画刷规则保证任何时候格子里都是合法的字符组合（例如箱子画在目标点上会变成 '*'）。
    /// 导出时裁掉四周的空白，得到可以直接交给 LevelParser 的 XSB 文本。
    /// </summary>
    public sealed class LevelGrid
    {
        public const int MinSize = 3;
        public const int MaxWidth = 20;
        public const int MaxHeight = 14;

        private char[,] _cells;

        public int Width => _cells.GetLength(0);
        public int Height => _cells.GetLength(1);

        public LevelGrid(int width, int height)
        {
            _cells = new char[Clamp(width, MaxWidth), Clamp(height, MaxHeight)];
            Fill(' ');
        }

        /// <summary>新建关卡的默认模板：四周一圈墙。</summary>
        public static LevelGrid CreateWalled(int width, int height)
        {
            var grid = new LevelGrid(width, height);
            for (int x = 0; x < grid.Width; x++)
            {
                grid._cells[x, 0] = '#';
                grid._cells[x, grid.Height - 1] = '#';
            }
            for (int y = 0; y < grid.Height; y++)
            {
                grid._cells[0, y] = '#';
                grid._cells[grid.Width - 1, y] = '#';
            }
            return grid;
        }

        /// <summary>从 XSB 行创建（用于编辑已保存的关卡）。不认识的字符当作空白。</summary>
        public static LevelGrid FromRows(string[] rows)
        {
            int width = 0;
            foreach (string row in rows)
                if (row != null && row.Length > width) width = row.Length;

            var grid = new LevelGrid(width, rows.Length);
            for (int y = 0; y < Math.Min(rows.Length, grid.Height); y++)
            {
                string row = rows[y] ?? string.Empty;
                for (int x = 0; x < Math.Min(row.Length, grid.Width); x++)
                    grid._cells[x, y] = IsKnown(row[x]) ? Normalize(row[x]) : ' ';
            }
            return grid;
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public char Get(GridPos p) => InBounds(p) ? _cells[p.X, p.Y] : ' ';

        /// <summary>在某个格子上使用工具，格子内容发生变化时返回 true。</summary>
        public bool Apply(EditTool tool, GridPos p)
        {
            if (!InBounds(p)) return false;

            char current = _cells[p.X, p.Y];
            char next;
            switch (tool)
            {
                case EditTool.Wall:
                    next = '#';
                    break;
                case EditTool.Erase:
                    next = ' ';
                    break;
                case EditTool.Goal:
                    next = current == '$' ? '*' : current == '@' ? '+' : HasGoal(current) ? current : '.';
                    break;
                case EditTool.Box:
                    next = HasGoal(current) ? '*' : '$';
                    break;
                case EditTool.Player:
                    if (IsPlayer(current)) return false;
                    RemovePlayer();
                    next = HasGoal(current) ? '+' : '@';
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tool), tool, null);
            }

            if (next == current) return false;
            _cells[p.X, p.Y] = next;
            return true;
        }

        /// <summary>改变尺寸，保留左上角的内容。尺寸会被限制在 MinSize ~ Max 之间。</summary>
        public void Resize(int width, int height)
        {
            var resized = new char[Clamp(width, MaxWidth), Clamp(height, MaxHeight)];
            for (int y = 0; y < resized.GetLength(1); y++)
            for (int x = 0; x < resized.GetLength(0); x++)
                resized[x, y] = x < Width && y < Height ? _cells[x, y] : ' ';
            _cells = resized;
        }

        public void Clear() => Fill(' ');

        public LevelGrid Clone()
        {
            var copy = new LevelGrid(Width, Height);
            copy._cells = (char[,])_cells.Clone();
            return copy;
        }

        public GridPos? FindPlayer()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (IsPlayer(_cells[x, y])) return new GridPos(x, y);
            return null;
        }

        /// <summary>从玩家出发、不穿墙能走到的格子（忽略箱子）。没有玩家时全部为 false。</summary>
        public bool[,] GetPlayerArea()
        {
            var area = new bool[Width, Height];
            GridPos? start = FindPlayer();
            if (!start.HasValue) return area;

            var stack = new Stack<GridPos>();
            stack.Push(start.Value);
            while (stack.Count > 0)
            {
                GridPos p = stack.Pop();
                if (!InBounds(p) || area[p.X, p.Y] || _cells[p.X, p.Y] == '#') continue;
                area[p.X, p.Y] = true;
                stack.Push(new GridPos(p.X + 1, p.Y));
                stack.Push(new GridPos(p.X - 1, p.Y));
                stack.Push(new GridPos(p.X, p.Y + 1));
                stack.Push(new GridPos(p.X, p.Y - 1));
            }
            return area;
        }

        /// <summary>导出为 XSB 行：裁掉四周空白，去掉行尾空格。整张图都是空白时返回空数组。</summary>
        public string[] ToRows()
        {
            int minX = Width, minY = Height, maxX = -1, maxY = -1;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (_cells[x, y] == ' ') continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            if (maxX < 0) return new string[0];

            var rows = new string[maxY - minY + 1];
            var sb = new StringBuilder();
            for (int y = minY; y <= maxY; y++)
            {
                sb.Clear();
                for (int x = minX; x <= maxX; x++) sb.Append(_cells[x, y]);
                rows[y - minY] = sb.ToString().TrimEnd();
            }
            return rows;
        }

        public static bool HasGoal(char c) => c == '.' || c == '*' || c == '+';
        public static bool HasBox(char c) => c == '$' || c == '*';
        public static bool IsPlayer(char c) => c == '@' || c == '+';

        private void RemovePlayer()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (_cells[x, y] == '@') _cells[x, y] = ' ';
                else if (_cells[x, y] == '+') _cells[x, y] = '.';
            }
        }

        private void Fill(char c)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _cells[x, y] = c;
        }

        private static int Clamp(int value, int max) => Math.Max(MinSize, Math.Min(max, value));

        private static bool IsKnown(char c) => "#.$*@+ -_".IndexOf(c) >= 0;

        private static char Normalize(char c) => c == '-' || c == '_' ? ' ' : c;
    }
}
