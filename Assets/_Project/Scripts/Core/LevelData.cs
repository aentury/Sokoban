using System;

namespace Sokoban.Core
{
    /// <summary>
    /// 关卡的存储格式：名称 + XSB 文本行。字段小写是为了配合 Unity 的 JsonUtility 序列化。
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public string name;
        public string[] rows;
    }
}
