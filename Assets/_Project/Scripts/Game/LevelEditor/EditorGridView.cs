using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 把编辑中的 LevelGrid 画出来。每个格子三层：底层（墙 / 地板 / 空格子）、目标点、物体（箱子 / 玩家）。
    /// 尺寸不变时只换贴图，不重新创建物体，拖动绘制时也很流畅。
    /// </summary>
    public sealed class EditorGridView : MonoBehaviour
    {
        private const int OrderBase = 0;
        private const int OrderGoal = 1;
        private const int OrderItem = 2;
        private const int OrderCursor = 5;

        private LevelGrid _grid;
        private SpriteRenderer[,] _base;
        private SpriteRenderer[,] _goal;
        private SpriteRenderer[,] _item;
        private SpriteRenderer _cursor;

        /// <summary>网格对象或尺寸变化后调用，重新创建所有格子。</summary>
        public void Build(LevelGrid grid)
        {
            _grid = grid;
            UIFactory.ClearChildren(transform);

            _base = new SpriteRenderer[grid.Width, grid.Height];
            _goal = new SpriteRenderer[grid.Width, grid.Height];
            _item = new SpriteRenderer[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var p = new GridPos(x, y);
                    _base[x, y] = CreateRenderer("Cell", p, OrderBase);
                    _goal[x, y] = CreateRenderer("Goal", p, OrderGoal);
                    _goal[x, y].sprite = SpriteLibrary.GetTile("goal");
                    _item[x, y] = CreateRenderer("Item", p, OrderItem);
                }
            }

            _cursor = CreateRenderer("Cursor", new GridPos(0, 0), OrderCursor);
            _cursor.sprite = SpriteLibrary.GetTile("editor_cursor");
            _cursor.enabled = false;
            Refresh();
        }

        /// <summary>格子内容变化后调用，只更新贴图。</summary>
        public void Refresh()
        {
            bool[,] area = _grid.GetPlayerArea();
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    char c = _grid.Get(new GridPos(x, y));
                    _base[x, y].sprite = SpriteLibrary.GetTile(c == '#' ? "wall" : area[x, y] ? "floor" : "editor_empty");
                    _goal[x, y].enabled = LevelGrid.HasGoal(c);

                    string item = LevelGrid.HasBox(c) ? (LevelGrid.HasGoal(c) ? "box_on_goal" : "box")
                        : LevelGrid.IsPlayer(c) ? "player" : null;
                    _item[x, y].enabled = item != null;
                    if (item != null) _item[x, y].sprite = SpriteLibrary.GetTile(item);
                }
            }
        }

        /// <summary>屏幕坐标（鼠标位置）转换成格子坐标，不在网格内时返回 null。</summary>
        public GridPos? ScreenToCell(Camera camera, Vector3 screenPosition)
        {
            Vector3 local = transform.InverseTransformPoint(camera.ScreenToWorldPoint(screenPosition));
            int x = Mathf.RoundToInt(local.x + (_grid.Width - 1) * 0.5f);
            int y = Mathf.RoundToInt((_grid.Height - 1) * 0.5f - local.y);
            var p = new GridPos(x, y);
            return _grid.InBounds(p) ? p : (GridPos?)null;
        }

        public void SetCursor(GridPos? cell)
        {
            _cursor.enabled = cell.HasValue;
            if (cell.HasValue) _cursor.transform.localPosition = CellToLocal(cell.Value);
        }

        private Vector3 CellToLocal(GridPos p) =>
            new Vector3(p.X - (_grid.Width - 1) * 0.5f, (_grid.Height - 1) * 0.5f - p.Y, 0f);

        private SpriteRenderer CreateRenderer(string objectName, GridPos p, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = CellToLocal(p);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }
    }
}
