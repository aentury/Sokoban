using System;
using System.Collections.Generic;
using System.IO;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>把 SaveData 以 JSON 形式读写到 persistentDataPath/save.json。存档损坏时从空存档开始，不让游戏崩溃。</summary>
    public static class SaveSystem
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (data != null)
                    {
                        if (data.records == null) data.records = new List<LevelRecord>();
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not read save file, starting fresh: {e.Message}");
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not write save file: {e.Message}");
            }
        }
    }
}
