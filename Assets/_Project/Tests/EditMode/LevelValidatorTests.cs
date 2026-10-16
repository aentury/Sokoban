using NUnit.Framework;
using Sokoban.Core;

namespace Sokoban.Tests
{
    public class LevelValidatorTests
    {
        private static System.Collections.Generic.List<string> Validate(params string[] rows) =>
            LevelValidator.Validate(rows);

        [Test]
        public void ValidLevel_HasNoErrors()
        {
            CollectionAssert.IsEmpty(Validate("#####", "#@$.#", "#####"));
        }

        [Test]
        public void Empty_IsReported()
        {
            Assert.AreEqual(1, LevelValidator.Validate(new string[0]).Count);
        }

        [Test]
        public void MissingPlayer_IsReported()
        {
            StringAssert.Contains("玩家", Validate("#####", "# $.#", "#####")[0]);
        }

        [Test]
        public void TwoPlayers_IsReported()
        {
            StringAssert.Contains("只能有一个玩家", Validate("######", "#@@$.#", "######")[0]);
        }

        [Test]
        public void NoBoxes_IsReported()
        {
            CollectionAssert.Contains(Validate("####", "#@ #", "####"), "至少需要一个箱子。");
        }

        [Test]
        public void BoxGoalMismatch_IsReported()
        {
            StringAssert.Contains("数量必须相同", Validate("######", "#@$$.#", "######")[0]);
        }

        [Test]
        public void OpenArea_IsReported()
        {
            CollectionAssert.Contains(Validate("#####", " @$.#", "#####"),
                "玩家所在的区域必须用墙围起来。");
        }

        [Test]
        public void BoxOutsidePlayerArea_IsReported()
        {
            CollectionAssert.Contains(Validate("#########", "#@ #$  .#", "#########"),
                "所有箱子和目标点都必须在玩家能走到的区域内。");
        }

        [Test]
        public void AlreadySolved_IsReported()
        {
            CollectionAssert.Contains(Validate("####", "#@*#", "####"), "所有箱子已经在目标点上了。");
        }
    }
}
