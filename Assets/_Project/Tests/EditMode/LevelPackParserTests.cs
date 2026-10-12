using NUnit.Framework;
using Sokoban.Core;

namespace Sokoban.Tests
{
    public class LevelPackParserTests
    {
        [Test]
        public void Parse_SplitsLevelsAndReadsTitles()
        {
            const string text =
                "; First\n" +
                "#####\n" +
                "#@$.#\n" +
                "#####\n" +
                "\n" +
                "; Second\n" +
                "####\n" +
                "#+*#\n" +
                "####\n";

            var levels = LevelPackParser.Parse(text);

            Assert.AreEqual(2, levels.Count);
            Assert.AreEqual("First", levels[0].name);
            Assert.AreEqual("Second", levels[1].name);
            CollectionAssert.AreEqual(new[] { "####", "#+*#", "####" }, levels[1].rows);
        }

        [Test]
        public void Parse_HandlesWindowsLineEndingsAndExtraBlankLines()
        {
            const string text = "\r\n\r\n; A\r\n#@#\r\n\r\n\r\n; B\r\n#+#\r\n";

            var levels = LevelPackParser.Parse(text);

            Assert.AreEqual(2, levels.Count);
            CollectionAssert.AreEqual(new[] { "#@#" }, levels[0].rows);
        }

        [Test]
        public void Parse_KeepsLeadingSpacesInRows()
        {
            var levels = LevelPackParser.Parse("  ###\n###@#\n#####");

            Assert.AreEqual("  ###", levels[0].rows[0]);
        }

        [Test]
        public void Parse_UntitledLevel_GetsDefaultName()
        {
            var levels = LevelPackParser.Parse("#@#\n\n#@#");

            Assert.AreEqual("Level 1", levels[0].name);
            Assert.AreEqual("Level 2", levels[1].name);
        }

        [Test]
        public void Parse_EmptyText_ReturnsNoLevels()
        {
            Assert.AreEqual(0, LevelPackParser.Parse("").Count);
            Assert.AreEqual(0, LevelPackParser.Parse(null).Count);
        }
    }
}
