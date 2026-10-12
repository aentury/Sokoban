using System.Collections.Generic;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 从 Resources/Sprites 加载贴图并缓存。
    /// 统一在代码里创建 Sprite，这样不依赖每张图片的导入设置（像素密度、九宫格边距）。
    /// 换美术素材时只需替换同名 PNG。
    /// </summary>
    public static class SpriteLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>棋盘贴图：一张图正好占一个格子（1 个世界单位）。</summary>
        public static Sprite GetTile(string name) => Get(name, 0f, Vector4.zero);

        /// <summary>UI 九宫格底图，border 为四边不拉伸的像素宽度。</summary>
        public static Sprite GetUISprite(string name, float border) =>
            Get(name, 100f, new Vector4(border, border, border, border));

        private static Sprite Get(string name, float pixelsPerUnit, Vector4 border)
        {
            if (Cache.TryGetValue(name, out Sprite cached)) return cached;

            var texture = Resources.Load<Texture2D>("Sprites/" + name);
            if (texture == null)
            {
                Debug.LogError($"Missing texture: Resources/Sprites/{name}.png");
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : texture.width;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            Cache[name] = sprite;
            return sprite;
        }
    }
}
