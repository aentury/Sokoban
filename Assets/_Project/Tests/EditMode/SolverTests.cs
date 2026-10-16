using System.IO;
using NUnit.Framework;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Tests
{
    public class SolverTests
    {
        [Test]
        public void StraightPush_NeedsTwoPushes()
        {
            SolveResult result = Solver.Solve(new[] { "#######", "#@$ .##", "#######" });

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(2, result.Pushes);
        }

        [Test]
        public void PlayerMustWalkAroundToPushBack()
        {
            // 箱子在玩家左边，需要绕到箱子左侧才能把它往右推回目标点
            SolveResult result = Solver.Solve(new[]
            {
                "######",
                "#    #",
                "# $@.#",
                "#    #",
                "######"
            });

            Assert.AreEqual(SolveStatus.Solvable, result.Status);
            Assert.AreEqual(2, result.Pushes);
        }

        [Test]
        public void BoxInCorner_IsUnsolvable()
        {
            SolveResult result = Solver.Solve(new[] { "#####", "#$ .#", "#  @#", "#####" });

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void BoxAgainstWallWithGoalOffTheWall_IsUnsolvable()
        {
            // 箱子贴着上边的墙只能左右推，永远到不了下面一行的目标点
            SolveResult result = Solver.Solve(new[] { "######", "# $  #", "#   .#", "#@   #", "######" });

            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
        }

        [Test]
        public void StateLimit_ReturnsTooComplex()
        {
            SolveResult result = Solver.Solve(new[]
            {
                "########",
                "#      #",
                "# $$$  #",
                "#  @   #",
                "#   ...#",
                "########"
            }, maxStates: 5);

            Assert.AreEqual(SolveStatus.TooComplex, result.Status);
        }

        [Test]
        public void AllBuiltInLevels_AreSolvable()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/Levels/levels.txt");
            foreach (LevelData level in LevelPackParser.Parse(File.ReadAllText(path)))
            {
                Assert.AreEqual(SolveStatus.Solvable, Solver.Solve(level.rows).Status, level.name);
            }
        }
    }
}
