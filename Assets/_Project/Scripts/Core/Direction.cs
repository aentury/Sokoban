using System;

namespace Sokoban.Core
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class DirectionExtensions
    {
        public static GridPos ToOffset(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new GridPos(0, -1);
                case Direction.Down: return new GridPos(0, 1);
                case Direction.Left: return new GridPos(-1, 0);
                case Direction.Right: return new GridPos(1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}
