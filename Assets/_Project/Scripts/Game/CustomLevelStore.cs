using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 玩家自制关卡的存取：每个关卡一个 JSON 文件，放在 persistentDataPath/CustomLevels/。
    /// 关卡名称决定文件名，同名保存会覆盖。
    /// </summary>
    public static class CustomLevelStore
    {
        private static string Folder => Path.Combine(Application.persistentDataPath, "CustomLevels");

        /// <summary>自制关卡在存档里的 ID，加前缀避免和同名的内置关卡冲突。</summary>
        public static string RecordId(string levelName) => "custom:" + levelName;

        public static List<LevelData> LoadAll()
        {
            var levels = new List<LevelData>();
            if (!Directory.Exists(Folder)) return levels;

            foreach (string file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var level = JsonUtility.FromJson<LevelData>(File.ReadAllText(file));
                    if (level != null && level.rows != null && LevelValidator.Validate(level.rows).Count == 0)
                        levels.Add(level);
                    else
                        Debug.LogWarning($"Skipping invalid custom level: {file}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read custom level {file}: {e.Message}");
                }
            }
            levels.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return levels;
        }

        public static bool Exists(string name) => File.Exists(PathFor(name));

        /// <summary>保存关卡，成功返回 true。</summary>
        public static bool Save(LevelData level)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathFor(level.name), JsonUtility.ToJson(level, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not save custom level '{level.name}': {e.Message}");
                return false;
            }
        }

        public static void Delete(string name)
        {
            try
            {
                if (File.Exists(PathFor(name))) File.Delete(PathFor(name));
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not delete custom level '{name}': {e.Message}");
            }
        }

        /// <summary>把关卡名转换成安全的文件名（去掉 Windows 不允许的字符）。</summary>
        private static string PathFor(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return Path.Combine(Folder, sb + ".json");
        }
    }
}
