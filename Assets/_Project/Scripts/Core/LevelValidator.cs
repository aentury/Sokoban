using System;
using System.Collections.Generic;

namespace Sokoban.Core
{
    /// <summary>
    /// 检查一个关卡能不能玩，返回给玩家看的错误说明；列表为空表示合法。
    /// 这里只做"结构"检查，"能不能解开"由 Solver 负责。
    /// </summary>
    public static class LevelValidator
    {
        public static List<string> Validate(string[] rows)
        {
            var errors = new List<string>();
            if (rows == null || rows.Length == 0)
            {
                errors.Add("关卡是空的。");
                return errors;
            }

            int players = 0, boxes = 0, goals = 0;
            foreach (string row in rows)
            {
                foreach (char c in row ?? string.Empty)
                {
                    if (c == '@' || c == '+') players++;
                    if (c == '$' || c == '*') boxes++;
                    if (c == '.' || c == '*' || c == '+') goals++;
                }
            }

            if (players == 0) errors.Add("请放置玩家。");
            else if (players > 1) errors.Add("只能有一个玩家。");
            if (boxes == 0) errors.Add("至少需要一个箱子。");
            if (boxes != goals) errors.Add($"箱子（{boxes} 个）和目标点（{goals} 个）的数量必须相同。");
            if (errors.Count > 0) return errors;

            Board board;
            try
            {
                board = LevelParser.Parse(rows);
            }
            catch (FormatException e)
            {
                errors.Add(e.Message);
                return errors;
            }

            bool[,] inside = board.GetInterior();
            bool open = false, unreachable = false;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    bool onEdge = x == 0 || y == 0 || x == board.Width - 1 || y == board.Height - 1;
                    if (inside[x, y] && onEdge) open = true;
                    if (!inside[x, y] && (board.HasBox(p) || board.IsGoal(p))) unreachable = true;
                }
            }

            if (open) errors.Add("玩家所在的区域必须用墙围起来。");
            if (unreachable) errors.Add("所有箱子和目标点都必须在玩家能走到的区域内。");
            if (errors.Count == 0 && board.IsSolved) errors.Add("所有箱子已经在目标点上了。");
            return errors;
        }
    }
}
