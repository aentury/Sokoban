using System;
using System.Collections.Generic;

namespace Sokoban.Core
{
    [Serializable]
    public class LevelRecord
    {
        public string levelId;
        public int bestMoves;
    }

    /// <summary>
    /// 玩家存档：设置 + 每关最佳步数。读写 JSON 文件由表现层的 SaveSystem 负责，
    /// 这里只放规则（什么算新纪录、哪些关卡已解锁），方便单元测试。
    /// 关卡以名称作为 ID，所以内置关卡的名称不能重复。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public float volume = 1f;
        public List<LevelRecord> records = new List<LevelRecord>();

        public bool IsCompleted(string levelId) => Find(levelId) != null;

        /// <summary>该关最佳步数，没通关过返回 0。</summary>
        public int GetBestMoves(string levelId)
        {
            LevelRecord record = Find(levelId);
            return record != null ? record.bestMoves : 0;
        }

        /// <summary>记录一次通关。首次通关或步数更少时返回 true（新纪录）。</summary>
        public bool RecordWin(string levelId, int moves)
        {
            LevelRecord record = Find(levelId);
            if (record == null)
            {
                records.Add(new LevelRecord { levelId = levelId, bestMoves = moves });
                return true;
            }
            if (moves < record.bestMoves)
            {
                record.bestMoves = moves;
                return true;
            }
            return false;
        }

        /// <summary>第一关总是解锁；之后每一关需要先通关上一关。</summary>
        public bool IsUnlocked(IReadOnlyList<LevelData> levels, int index)
        {
            return index == 0 || IsCompleted(levels[index - 1].name);
        }

        private LevelRecord Find(string levelId)
        {
            foreach (LevelRecord record in records)
                if (record.levelId == levelId) return record;
            return null;
        }
    }
}
