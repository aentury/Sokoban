using System.Collections.Generic;

namespace Sokoban.Core
{
    /// <summary>
    /// 解析包含多个关卡的文本文件（常见的 Sokoban 关卡包格式）：
    ///   ; 关卡标题
    ///   XSB 行...
    ///   （空行分隔下一个关卡）
    /// 没写标题的关卡自动命名为 "Level N"。这里只负责切分，关卡内容是否合法由 LevelParser 检查。
    /// </summary>
    public static class LevelPackParser
    {
        public static List<LevelData> Parse(string text)
        {
            var levels = new List<LevelData>();
            var rows = new List<string>();
            string title = null;

            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(";"))
                {
                    Flush(levels, rows, ref title);
                    title = trimmed.Substring(1).Trim();
                }
                else if (trimmed.Length == 0)
                {
                    Flush(levels, rows, ref title);
                }
                else
                {
                    rows.Add(line.TrimEnd());
                }
            }
            Flush(levels, rows, ref title);
            return levels;
        }

        private static void Flush(List<LevelData> levels, List<string> rows, ref string title)
        {
            if (rows.Count == 0) return;
            levels.Add(new LevelData
            {
                name = string.IsNullOrEmpty(title) ? $"Level {levels.Count + 1}" : title,
                rows = rows.ToArray()
            });
            rows.Clear();
            title = null;
        }
    }
}
