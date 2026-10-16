using System.Collections.Generic;
using Sokoban.Core;
using UnityEngine;

namespace Sokoban.Game
{
    /// <summary>
    /// 把 Board 画出来并播放动画。只读取 Board，从不修改它。
    /// 逻辑在按键时就已经生效，这里的动画只是"追赶"逻辑状态；
    /// 撤销 / 重开时直接 SyncToBoard 对齐，因此画面永远不会和逻辑错位。
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float MoveDuration = 0.12f;
        private const float BumpDuration = 0.12f;
        private const float BumpDistance = 0.12f;
        private const float CelebrateDuration = 0.45f;

        private const int OrderFloor = 0;
        private const int OrderGoal = 1;
        private const int OrderWall = 1;
        private const int OrderBox = 2;
        private const int OrderPlayer = 3;

        private struct Tween
        {
            public Transform Target;
            public Vector3 From;
            public Vector3 To;
        }

        private Board _board;
        private SpriteRenderer _player;
        private readonly Dictionary<GridPos, SpriteRenderer> _boxes = new Dictionary<GridPos, SpriteRenderer>();
        private readonly List<Tween> _tweens = new List<Tween>();
        private float _elapsed = MoveDuration;
        private Vector3 _bumpOffset;
        private float _bumpElapsed = BumpDuration;
        private float _celebrateElapsed = CelebrateDuration;

        /// <summary>移动或撞墙动画播放中。播放期间输入会被缓存，等动画结束再执行。</summary>
        public bool IsAnimating => _elapsed < MoveDuration || _bumpElapsed < BumpDuration;

        public void Build(Board board)
        {
            Clear();
            _board = board;

            bool[,] interior = board.GetInterior();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    if (board.IsWall(p))
                    {
                        CreateSprite("Wall", "wall", p, OrderWall);
                    }
                    else if (interior[x, y] || board.IsGoal(p) || board.HasBox(p))
                    {
                        CreateSprite("Floor", "floor", p, OrderFloor);
                        if (board.IsGoal(p)) CreateSprite("Goal", "goal", p, OrderGoal);
                    }
                }
            }

            foreach (GridPos p in board.Boxes)
                _boxes[p] = CreateSprite("Box", "box", p, OrderBox);
            _player = CreateSprite("Player", "player", board.Player, OrderPlayer);
            RefreshBoxSprites();
        }

        public void Clear()
        {
            UIFactory.ClearChildren(transform);
            _boxes.Clear();
            _tweens.Clear();
            _player = null;
            _board = null;
            ResetAnimations();
        }

        /// <summary>为一次成功的移动播放动画。</summary>
        public void Animate(MoveResult move)
        {
            CompleteTweens();
            _bumpElapsed = BumpDuration;
            if (move.Kind == MoveKind.Blocked) return;

            AddTween(_player.transform, move.PlayerTo);
            if (move.Kind == MoveKind.Pushed)
            {
                SpriteRenderer box = _boxes[move.BoxFrom];
                _boxes.Remove(move.BoxFrom);
                _boxes[move.BoxTo] = box;
                AddTween(box.transform, move.BoxTo);
                box.sprite = BoxSprite(move.BoxTo);
            }
            _elapsed = 0f;
        }

        /// <summary>撞墙 / 推不动时，角色朝那个方向轻轻顶一下再弹回来。</summary>
        public void Bump(Direction direction)
        {
            if (_player == null) return;
            CompleteTweens();
            GridPos offset = direction.ToOffset();
            _bumpOffset = new Vector3(offset.X, -offset.Y, 0f) * BumpDistance;
            _bumpElapsed = 0f;
        }

        /// <summary>通关时所有箱子一起弹跳一下。</summary>
        public void Celebrate()
        {
            _celebrateElapsed = 0f;
        }

        /// <summary>立即停止动画，把所有物体摆到 Board 当前的位置（用于撤销、重开）。</summary>
        public void SyncToBoard()
        {
            _tweens.Clear();
            ResetAnimations();
            _player.transform.localPosition = GridToLocal(_board.Player);

            var renderers = new List<SpriteRenderer>(_boxes.Values);
            _boxes.Clear();
            int i = 0;
            foreach (GridPos p in _board.Boxes)
            {
                SpriteRenderer box = renderers[i++];
                box.transform.localPosition = GridToLocal(p);
                box.transform.localScale = Vector3.one;
                _boxes[p] = box;
            }
            RefreshBoxSprites();
        }

        private void Update()
        {
            if (_board == null) return;
            float dt = Time.deltaTime;

            if (_elapsed < MoveDuration)
            {
                _elapsed += dt;
                float t = Mathf.Clamp01(_elapsed / MoveDuration);
                t = t * t * (3f - 2f * t); // 缓入缓出
                foreach (Tween tween in _tweens)
                    tween.Target.localPosition = Vector3.LerpUnclamped(tween.From, tween.To, t);
            }

            if (_bumpElapsed < BumpDuration)
            {
                _bumpElapsed += dt;
                float t = Mathf.Clamp01(_bumpElapsed / BumpDuration);
                _player.transform.localPosition = GridToLocal(_board.Player) + _bumpOffset * Mathf.Sin(t * Mathf.PI);
            }

            if (_celebrateElapsed < CelebrateDuration)
            {
                _celebrateElapsed += dt;
                float t = Mathf.Clamp01(_celebrateElapsed / CelebrateDuration);
                Vector3 scale = Vector3.one * (1f + 0.2f * Mathf.Sin(t * Mathf.PI));
                foreach (SpriteRenderer box in _boxes.Values)
                    box.transform.localScale = scale;
            }
        }

        private void ResetAnimations()
        {
            _elapsed = MoveDuration;
            _bumpElapsed = BumpDuration;
            _celebrateElapsed = CelebrateDuration;
        }

        private void CompleteTweens()
        {
            foreach (Tween tween in _tweens)
                tween.Target.localPosition = tween.To;
            _tweens.Clear();
            _elapsed = MoveDuration;
        }

        private void AddTween(Transform target, GridPos to)
        {
            _tweens.Add(new Tween { Target = target, From = target.localPosition, To = GridToLocal(to) });
        }

        private void RefreshBoxSprites()
        {
            foreach (KeyValuePair<GridPos, SpriteRenderer> pair in _boxes)
                pair.Value.sprite = BoxSprite(pair.Key);
        }

        private Sprite BoxSprite(GridPos p) =>
            SpriteLibrary.GetTile(_board.IsGoal(p) ? "box_on_goal" : "box");

        /// <summary>格子坐标 → 本地坐标。棋盘中心在原点，格子 Y 向下而世界 Y 向上，所以要翻转。</summary>
        private Vector3 GridToLocal(GridPos p) =>
            new Vector3(p.X - (_board.Width - 1) * 0.5f, (_board.Height - 1) * 0.5f - p.Y, 0f);

        private SpriteRenderer CreateSprite(string objectName, string spriteName, GridPos p, int order)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = GridToLocal(p);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = SpriteLibrary.GetTile(spriteName);
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }
    }
}
