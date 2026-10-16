using NUnit.Framework;
using Sokoban.Core;

namespace Sokoban.Tests
{
    public class LevelGridTests
    {
        private static readonly GridPos A = new GridPos(1, 1);
        private static readonly GridPos B = new GridPos(2, 1);

        [Test]
        public void CreateWalled_HasWallBorderAndEmptyInside()
        {
            LevelGrid grid = LevelGrid.CreateWalled(5, 4);

            CollectionAssert.AreEqual(new[] { "#####", "#   #", "#   #", "#####" }, grid.ToRows());
        }

        [Test]
        public void Size_IsClampedToLimits()
        {
            var tiny = new LevelGrid(1, 1);
            var huge = new LevelGrid(100, 100);

            Assert.AreEqual(LevelGrid.MinSize, tiny.Width);
            Assert.AreEqual(LevelGrid.MaxWidth, huge.Width);
            Assert.AreEqual(LevelGrid.MaxHeight, huge.Height);
        }

        [Test]
        public void Box_OnGoal_BecomesBoxOnGoal()
        {
            var grid = new LevelGrid(5, 5);
            grid.Apply(EditTool.Goal, A);
            grid.Apply(EditTool.Box, A);

            Assert.AreEqual('*', grid.Get(A));
        }

        [Test]
        public void Goal_UnderPlayer_BecomesPlayerOnGoal()
        {
            var grid = new LevelGrid(5, 5);
            grid.Apply(EditTool.Player, A);
            grid.Apply(EditTool.Goal, A);

            Assert.AreEqual('+', grid.Get(A));
        }

        [Test]
        public void Player_IsMovedNotDuplicated_AndLeavesGoalBehind()
        {
            var grid = new LevelGrid(5, 5);
            grid.Apply(EditTool.Goal, A);
            grid.Apply(EditTool.Player, A);   // '+'
            grid.Apply(EditTool.Player, B);

            Assert.AreEqual('.', grid.Get(A));
            Assert.AreEqual('@', grid.Get(B));
        }

        [Test]
        public void Apply_ReturnsFalseWhenNothingChanges()
        {
            var grid = new LevelGrid(5, 5);

            Assert.IsTrue(grid.Apply(EditTool.Wall, A));
            Assert.IsFalse(grid.Apply(EditTool.Wall, A));
            Assert.IsFalse(grid.Apply(EditTool.Wall, new GridPos(-1, 0)), "out of bounds");
        }

        [Test]
        public void Erase_ClearsEverything()
        {
            var grid = new LevelGrid(5, 5);
            grid.Apply(EditTool.Goal, A);
            grid.Apply(EditTool.Box, A);
            grid.Apply(EditTool.Erase, A);

            Assert.AreEqual(' ', grid.Get(A));
        }

        [Test]
        public void Resize_KeepsTopLeftContent()
        {
            LevelGrid grid = LevelGrid.CreateWalled(5, 5);
            grid.Resize(7, 3);

            Assert.AreEqual(7, grid.Width);
            Assert.AreEqual(3, grid.Height);
            Assert.AreEqual('#', grid.Get(new GridPos(0, 0)));
            Assert.AreEqual(' ', grid.Get(new GridPos(6, 0)), "new cells are empty");
        }

        [Test]
        public void ToRows_CropsEmptyBorder()
        {
            var grid = new LevelGrid(8, 6);
            grid.Apply(EditTool.Wall, new GridPos(2, 1));
            grid.Apply(EditTool.Player, new GridPos(3, 2));

            CollectionAssert.AreEqual(new[] { "#", " @" }, grid.ToRows());
        }

        [Test]
        public void ToRows_EmptyGrid_ReturnsNoRows()
        {
            Assert.AreEqual(0, new LevelGrid(5, 5).ToRows().Length);
        }

        [Test]
        public void FromRows_RoundTripsALevel()
        {
            string[] rows = { "#####", "#@$.#", "#####" };

            CollectionAssert.AreEqual(rows, LevelGrid.FromRows(rows).ToRows());
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var grid = new LevelGrid(5, 5);
            LevelGrid copy = grid.Clone();
            grid.Apply(EditTool.Wall, A);

            Assert.AreEqual(' ', copy.Get(A));
        }

        [Test]
        public void GetPlayerArea_StopsAtWalls()
        {
            LevelGrid grid = LevelGrid.FromRows(new[] { "#####", "#@#  ", "#####" });

            bool[,] area = grid.GetPlayerArea();

            Assert.IsTrue(area[1, 1]);
            Assert.IsFalse(area[3, 1]);
        }
    }
}
