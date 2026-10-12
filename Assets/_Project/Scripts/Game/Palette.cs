using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>全游戏统一使用的颜色，集中在这里方便整体调整风格。</summary>
    public static class Palette
    {
        public static readonly Color Background = Hex("#1F2430");
        public static readonly Color Overlay = new Color(0.07f, 0.08f, 0.11f, 0.78f);
        public static readonly Color Card = Hex("#2B3242");
        public static readonly Color Bar = new Color(0.12f, 0.14f, 0.19f, 0.9f);
        public static readonly Color Text = Hex("#F2EEE6");
        public static readonly Color TextDim = Hex("#9AA3B5");
        public static readonly Color TextOnAccent = Hex("#2A1E0C");
        public static readonly Color Accent = Hex("#F2A541");
        public static readonly Color Success = Hex("#6DBF73");
        public static readonly Color Button = Hex("#3B4458");
        public static readonly Color ButtonDisabled = Hex("#262B37");

        private static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color color);
            return color;
        }
    }
}
