using System;
using NUnit.Framework;
using Sokoban.Core;

namespace Sokoban.Tests
{
    public class BoardTests
    {
        private static Board Parse(params string[] rows) => LevelParser.Parse(rows);

        // ---------- 解析 ----------

        [Test]
        public void Parse_ReadsSizeAndEntities()
        {
            Board board = Parse(
                "#####",
                "#@$.#",
                "#####");

            Assert.AreEqual(5, board.Width);
            Assert.AreEqual(3, board.Height);
            Assert.AreEqual(new GridPos(1, 1), board.Player);
            Assert.IsTrue(board.HasBox(new GridPos(2, 1)));
            Assert.IsTrue(board.IsGoal(new GridPos(3, 1)));
            Assert.IsTrue(board.IsWall(new GridPos(0, 0)));
        }

        [Test]
        public void Parse_BoxOnGoalAndPlayerOnGoal()
        {
            Board board = Parse("#*+#");

            Assert.IsTrue(board.HasBox(new GridPos(1, 0)));
            Assert.IsTrue(board.IsGoal(new GridPos(1, 0)));
            Assert.AreEqual(new GridPos(2, 0), board.Player);
            Assert.IsTrue(board.IsGoal(new GridPos(2, 0)));
        }

        [Test]
        public void Parse_RaggedRows_ArePaddedAndOutsideIsWall()
        {
            Board board = Parse(
                "####",
                "#@#");

            Assert.AreEqual(4, board.Width);
            Assert.IsFalse(board.IsWall(new GridPos(3, 1)), "padded cell is floor");
            Assert.IsTrue(board.IsWall(new GridPos(-1, 0)), "out of bounds counts as wall");
        }

        [Test]
        public void Parse_InvalidCharacter_Throws()
        {
            Assert.Throws<FormatException>(() => Parse("#@X#"));
        }

        [Test]
        public void Parse_NoPlayer_Throws()
        {
            Assert.Throws<FormatException>(() => Parse("#$.#"));
        }

        [Test]
        public void Parse_TwoPlayers_Throws()
        {
            Assert.Throws<FormatException>(() => Parse("#@@#"));
        }

        [Test]
        public void Parse_Empty_Throws()
        {
            Assert.Throws<FormatException>(() => LevelParser.Parse(new string[0]));
        }

        // ---------- 移动 ----------

        [Test]
        public void Move_OntoFloor_Walks()
        {
            Board board = Parse("#@ #");

            MoveResult result = board.TryMove(Direction.Right);

            Assert.AreEqual(MoveKind.Walked, result.Kind);
            Assert.AreEqual(new GridPos(2, 0), board.Player);
            Assert.AreEqual(1, board.MoveCount);
            Assert.AreEqual(0, board.PushCount);
        }

        [Test]
        public void Move_IntoWall_IsBlockedAndChangesNothing()
        {
            Board board = Parse("#@#");

            MoveResult result = board.TryMove(Direction.Left);

            Assert.AreEqual(MoveKind.Blocked, result.Kind);
            Assert.AreEqual(new GridPos(1, 0), board.Player);
            Assert.AreEqual(0, board.MoveCount);
        }

        [Test]
        public void Move_OutOfBounds_IsBlocked()
        {
            Board board = Parse("@");

            Assert.AreEqual(MoveKind.Blocked, board.TryMove(Direction.Up).Kind);
            Assert.AreEqual(MoveKind.Blocked, board.TryMove(Direction.Left).Kind);
        }

        [Test]
        public void Move_UpAndDown_UseRowDirection()
        {
            Board board = Parse(
                " ",
                "@",
                " ");

            board.TryMove(Direction.Up);
            Assert.AreEqual(new GridPos(0, 0), board.Player);
            board.TryMove(Direction.Down);
            board.TryMove(Direction.Down);
            Assert.AreEqual(new GridPos(0, 2), board.Player);
        }

        // ---------- 推箱子 ----------

        [Test]
        public void Push_BoxOntoFloor_MovesBoxAndPlayer()
        {
            Board board = Parse("#@$ #");

            MoveResult result = board.TryMove(Direction.Right);

            Assert.AreEqual(MoveKind.Pushed, result.Kind);
            Assert.AreEqual(new GridPos(2, 0), board.Player);
            Assert.IsFalse(board.HasBox(new GridPos(2, 0)));
            Assert.IsTrue(board.HasBox(new GridPos(3, 0)));
            Assert.AreEqual(new GridPos(2, 0), result.BoxFrom);
            Assert.AreEqual(new GridPos(3, 0), result.BoxTo);
            Assert.AreEqual(1, board.PushCount);
        }

        [Test]
        public void Push_BoxIntoWall_IsBlocked()
        {
            Board board = Parse("#@$#");

            MoveResult result = board.TryMove(Direction.Right);

            Assert.AreEqual(MoveKind.Blocked, result.Kind);
            Assert.AreEqual(new GridPos(1, 0), board.Player);
            Assert.IsTrue(board.HasBox(new GridPos(2, 0)));
            Assert.AreEqual(0, board.MoveCount);
        }

        [Test]
        public void Push_TwoBoxesInARow_IsBlocked()
        {
            Board board = Parse("#@$$ #");

            MoveResult result = board.TryMove(Direction.Right);

            Assert.AreEqual(MoveKind.Blocked, result.Kind);
            Assert.IsTrue(board.HasBox(new GridPos(2, 0)));
            Assert.IsTrue(board.HasBox(new GridPos(3, 0)));
        }

        [Test]
        public void Walk_AwayFromBox_DoesNotPullIt()
        {
            Board board = Parse("# $@ #");

            board.TryMove(Direction.Right);

            Assert.IsTrue(board.HasBox(new GridPos(2, 0)));
            Assert.AreEqual(new GridPos(4, 0), board.Player);
        }

        // ---------- 胜利判定 ----------

        [Test]
        public void IsSolved_FalseUntilAllBoxesOnGoals()
        {
            Board board = Parse(
                "######",
                "#@$ .#",
                "#*   #",
                "######");

            Assert.IsFalse(board.IsSolved);
            board.TryMove(Direction.Right);
            Assert.IsFalse(board.IsSolved);
            board.TryMove(Direction.Right);
            Assert.IsTrue(board.IsSolved);
        }

        [Test]
        public void IsSolved_BecomesFalseWhenBoxPushedOffGoal()
        {
            Board board = Parse("#@*  #");

            Assert.IsTrue(board.IsSolved);
            board.TryMove(Direction.Right);
            Assert.IsFalse(board.IsSolved);
        }

        // ---------- 撤销 / 重开 ----------

        [Test]
        public void Undo_WithNoHistory_ReturnsFalse()
        {
            Board board = Parse("#@ #");

            Assert.IsFalse(board.CanUndo);
            Assert.IsFalse(board.Undo());
        }

        [Test]
        public void Undo_AfterPush_RestoresPlayerAndBox()
        {
            Board board = Parse("#@$ #");
            board.TryMove(Direction.Right);

            Assert.IsTrue(board.Undo());

            Assert.AreEqual(new GridPos(1, 0), board.Player);
            Assert.IsTrue(board.HasBox(new GridPos(2, 0)));
            Assert.IsFalse(board.HasBox(new GridPos(3, 0)));
            Assert.AreEqual(0, board.MoveCount);
            Assert.AreEqual(0, board.PushCount);
        }

        [Test]
        public void Undo_BlockedMoveIsNotRecorded()
        {
            Board board = Parse("#@ #");
            board.TryMove(Direction.Right);
            board.TryMove(Direction.Right); // 撞墙

            Assert.AreEqual(1, board.MoveCount);
            board.Undo();
            Assert.AreEqual(new GridPos(1, 0), board.Player);
        }

        [Test]
        public void Undo_AllMoves_ReturnsToInitialState()
        {
            string[] level =
            {
                "#######",
                "#     #",
                "# $@$ #",
                "#.   .#",
                "#######"
            };
            Board board = LevelParser.Parse(level);
            string[] initial = LevelParser.Serialize(board);

            board.TryMove(Direction.Right);
            board.TryMove(Direction.Up);
            board.TryMove(Direction.Left);
            board.TryMove(Direction.Left);
            board.TryMove(Direction.Down);
            board.TryMove(Direction.Left);
            while (board.Undo()) { }

            CollectionAssert.AreEqual(initial, LevelParser.Serialize(board));
            Assert.AreEqual(0, board.PushCount);
        }

        [Test]
        public void Restart_ResetsBoxesPlayerAndHistory()
        {
            Board board = Parse("#@$ .#");
            string[] initial = LevelParser.Serialize(board);
            board.TryMove(Direction.Right);
            board.TryMove(Direction.Right);

            board.Restart();

            CollectionAssert.AreEqual(initial, LevelParser.Serialize(board));
            Assert.AreEqual(0, board.MoveCount);
            Assert.AreEqual(0, board.PushCount);
            Assert.IsFalse(board.CanUndo);
        }

        // ---------- 统计 / 室内区域 ----------

        [Test]
        public void Counts_BoxesAndGoals()
        {
            Board board = Parse("#@$*..#");

            Assert.AreEqual(2, board.BoxCount);
            Assert.AreEqual(3, board.GoalCount);
        }

        [Test]
        public void GetInterior_ExcludesSpaceOutsideWalls()
        {
            Board board = Parse(
                "  ###",
                "###@#",
                "#$ .#",
                "#####");

            bool[,] inside = board.GetInterior();

            Assert.IsFalse(inside[0, 0], "outside the walls");
            Assert.IsTrue(inside[3, 1], "player cell");
            Assert.IsTrue(inside[1, 2], "box cell counts as interior");
            Assert.IsFalse(inside[0, 2], "wall");
        }

        // ---------- 序列化 ----------

        [Test]
        public void Serialize_RoundTripsAllSymbols()
        {
            string[] level =
            {
                "#######",
                "#.$*  #",
                "#  +  #",
                "#######"
            };

            string[] output = LevelParser.Serialize(LevelParser.Parse(level));

            CollectionAssert.AreEqual(level, output);
        }

        [Test]
        public void Serialize_PlayerLeavingGoal_RestoresGoalSymbol()
        {
            Board board = Parse("#+ #");
            board.TryMove(Direction.Right);

            CollectionAssert.AreEqual(new[] { "#.@#" }, LevelParser.Serialize(board));
        }
    }
}
