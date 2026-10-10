using System;
using System.Collections.Generic;
using System.Text;

namespace Sokoban.Core
{
    /// <summary>
    /// XSB 文本与 Board 之间的转换。
    /// # 墙   空格/-/_ 地板   . 目标   $ 箱子   * 箱子在目标上   @ 玩家   + 玩家在目标上
    /// </summary>
    public static class LevelParser
    {
        public static Board Parse(string[] rows)
        {
            if (rows == null || rows.Length == 0)
                throw new FormatException("Level is empty.");

            int height = rows.Length;
            int width = 0;
            foreach (string row in rows)
                if (row != null && row.Length > width) width = row.Length;
            if (width == 0)
                throw new FormatException("Level is empty.");

            var walls = new bool[width, height];
            var goals = new bool[width, height];
            var boxes = new List<GridPos>();
            var players = new List<GridPos>();

            for (int y = 0; y < height; y++)
            {
                string row = rows[y] ?? string.Empty;
                for (int x = 0; x < row.Length; x++)
                {
                    var pos = new GridPos(x, y);
                    switch (row[x])
                    {
                        case '#': walls[x, y] = true; break;
                        case ' ': case '-': case '_': break;
                        case '.': goals[x, y] = true; break;
                        case '$': boxes.Add(pos); break;
                        case '*': boxes.Add(pos); goals[x, y] = true; break;
                        case '@': players.Add(pos); break;
                        case '+': players.Add(pos); goals[x, y] = true; break;
                        default:
                            throw new FormatException($"Invalid character '{row[x]}' at row {y + 1}, column {x + 1}.");
                    }
                }
            }

            if (players.Count != 1)
                throw new FormatException($"Level must contain exactly one player, found {players.Count}.");

            return new Board(walls, goals, boxes, players[0]);
        }

        /// <summary>把棋盘当前状态输出为 XSB 文本行（去掉行尾空格）。</summary>
        public static string[] Serialize(Board board)
        {
            var rows = new string[board.Height];
            var sb = new StringBuilder(board.Width);
            for (int y = 0; y < board.Height; y++)
            {
                sb.Clear();
                for (int x = 0; x < board.Width; x++)
                    sb.Append(ToChar(board, new GridPos(x, y)));
                rows[y] = sb.ToString().TrimEnd();
            }
            return rows;
        }

        private static char ToChar(Board board, GridPos p)
        {
            if (board.IsWall(p)) return '#';
            bool goal = board.IsGoal(p);
            if (board.Player == p) return goal ? '+' : '@';
            if (board.HasBox(p)) return goal ? '*' : '$';
            return goal ? '.' : ' ';
        }
    }
}
