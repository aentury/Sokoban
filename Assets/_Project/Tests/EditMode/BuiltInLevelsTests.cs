using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Tests
{
    /// <summary>检查随游戏发布的关卡文件：每关都能解析、箱子数等于目标数、名称不重复。</summary>
    public class BuiltInLevelsTests
    {
        private static List<LevelData> LoadLevels()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/Levels/levels.txt");
            return LevelPackParser.Parse(File.ReadAllText(path));
        }

        [Test]
        public void BuiltInLevels_AreAllValid()
        {
            List<LevelData> levels = LoadLevels();
            Assert.Greater(levels.Count, 0);

            var names = new HashSet<string>();
            foreach (LevelData level in levels)
            {
                Board board = LevelParser.Parse(level.rows);
                Assert.Greater(board.BoxCount, 0, level.name);
                Assert.AreEqual(board.GoalCount, board.BoxCount, level.name);
                Assert.IsFalse(board.IsSolved, level.name + " must not start solved");
                Assert.IsTrue(names.Add(level.name), "duplicate level name: " + level.name);
            }
        }
    }
}
