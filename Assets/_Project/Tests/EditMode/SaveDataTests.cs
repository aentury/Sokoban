using System.Collections.Generic;
using NUnit.Framework;
using Sokoban.Core;

namespace Sokoban.Tests
{
    public class SaveDataTests
    {
        [Test]
        public void RecordWin_FirstTime_IsNewBest()
        {
            var save = new SaveData();

            Assert.IsTrue(save.RecordWin("A", 30));
            Assert.IsTrue(save.IsCompleted("A"));
            Assert.AreEqual(30, save.GetBestMoves("A"));
        }

        [Test]
        public void RecordWin_KeepsOnlyFewerMoves()
        {
            var save = new SaveData();
            save.RecordWin("A", 30);

            Assert.IsFalse(save.RecordWin("A", 35), "more moves is not a record");
            Assert.IsFalse(save.RecordWin("A", 30), "same moves is not a record");
            Assert.AreEqual(30, save.GetBestMoves("A"));

            Assert.IsTrue(save.RecordWin("A", 25));
            Assert.AreEqual(25, save.GetBestMoves("A"));
        }

        [Test]
        public void GetBestMoves_NotCompleted_ReturnsZero()
        {
            Assert.AreEqual(0, new SaveData().GetBestMoves("A"));
        }

        [Test]
        public void IsUnlocked_RequiresPreviousLevelCompleted()
        {
            var levels = new List<LevelData>
            {
                new LevelData { name = "A" },
                new LevelData { name = "B" },
                new LevelData { name = "C" }
            };
            var save = new SaveData();

            Assert.IsTrue(save.IsUnlocked(levels, 0));
            Assert.IsFalse(save.IsUnlocked(levels, 1));

            save.RecordWin("A", 10);

            Assert.IsTrue(save.IsUnlocked(levels, 1));
            Assert.IsFalse(save.IsUnlocked(levels, 2));
        }
    }
}
