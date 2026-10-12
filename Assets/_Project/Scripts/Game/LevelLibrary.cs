using System;
using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>加载内置关卡（Resources/Levels/levels.txt）。格式不合法的关卡会被跳过并报错。</summary>
    public static class LevelLibrary
    {
        private const string BuiltInPath = "Levels/levels";

        public static List<LevelData> LoadBuiltIn()
        {
            var levels = new List<LevelData>();
            var asset = Resources.Load<TextAsset>(BuiltInPath);
            if (asset == null)
            {
                Debug.LogError($"Missing level file: Resources/{BuiltInPath}.txt");
                return levels;
            }

            foreach (LevelData level in LevelPackParser.Parse(asset.text))
            {
                try
                {
                    LevelParser.Parse(level.rows);
                    levels.Add(level);
                }
                catch (FormatException e)
                {
                    Debug.LogError($"Skipping invalid level '{level.name}': {e.Message}");
                }
            }
            return levels;
        }
    }
}
