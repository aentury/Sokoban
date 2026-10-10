namespace Sokoban.Core
{
    public enum MoveKind
    {
        Blocked,
        Walked,
        Pushed
    }

    /// <summary>
    /// 一次移动的结果。表现层用它播放动画，Board 也用它作为撤销记录。
    /// BoxFrom / BoxTo 只在 Kind == Pushed 时有意义。
    /// </summary>
    public readonly struct MoveResult
    {
        public readonly MoveKind Kind;
        public readonly GridPos PlayerFrom;
        public readonly GridPos PlayerTo;
        public readonly GridPos BoxFrom;
        public readonly GridPos BoxTo;

        private MoveResult(MoveKind kind, GridPos playerFrom, GridPos playerTo, GridPos boxFrom, GridPos boxTo)
        {
            Kind = kind;
            PlayerFrom = playerFrom;
            PlayerTo = playerTo;
            BoxFrom = boxFrom;
            BoxTo = boxTo;
        }

        public static MoveResult Blocked(GridPos player) =>
            new MoveResult(MoveKind.Blocked, player, player, default, default);

        public static MoveResult Walked(GridPos from, GridPos to) =>
            new MoveResult(MoveKind.Walked, from, to, default, default);

        public static MoveResult Pushed(GridPos from, GridPos to, GridPos boxFrom, GridPos boxTo) =>
            new MoveResult(MoveKind.Pushed, from, to, boxFrom, boxTo);
    }
}
