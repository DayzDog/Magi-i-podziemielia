using UnityEngine;

public static class PlacementValidator
{
    /// <summary>
    /// Правила:
    /// 1) Клетка пустая.
    /// 2) Любой соседний тайл, если существует, должен стыковаться путь↔путь (оба true).
    /// 3) Должен хотя бы к чему-то примыкать:
    ///    - либо к существующему тайлу,
    ///    - либо к виртуальному старту (x=2,y=0) через Down.
    /// 4) Новый тайл должен подключаться к компоненту, достижимому от старта (не создаём “островов”).
    /// </summary>
    public static bool CanPlace(BoardModel board, int x, int y, TileDefinition def, Rotation rot)
    {
        // 1) Клетка должна быть внутри поля и пустой
        if (!board.IsInside(x, y) || !board.IsEmpty(x, y))
            return false;

        var sockets = def.GetSockets(rot);

        bool hasNeighbor = false;
        bool touchesVirtualStart = (x == 2 && y == 0 && sockets.Down);

        // 2) Проверка стыковки со всеми соседями
        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            var off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            if (!board.IsInside(nx, ny)) continue;

            var neighbor = board.Get(nx, ny);
            if (neighbor == null) continue;

            hasNeighbor = true;

            var myOut = sockets.Get(side);
            var nbOut = neighbor.Def.GetSockets(neighbor.Rot)
                                    .Get(SideUtil.Opposite(side));

            // Должно быть путь↔путь, иначе ставить нельзя
            if (!(myOut && nbOut))
                return false;
        }

        // Если нет соседей и нет связи с виртуальным стартом — нельзя
        if (!hasNeighbor && !touchesVirtualStart)
            return false;

        // 3) Проверка подключения к уже достижимой от старта области
        var reachable = board.ComputeReachableFromStart();
        bool connectsToReachable = touchesVirtualStart; // прямое соединение со стартом

        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            var off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            if (!board.IsInside(nx, ny)) continue;
            if (!reachable[nx, ny]) continue;

            var neighbor = board.Get(nx, ny);
            if (neighbor == null) continue;

            var nbSockets = neighbor.Def.GetSockets(neighbor.Rot);

            if (sockets.Get(side) && nbSockets.Get(SideUtil.Opposite(side)))
            {
                connectsToReachable = true;
                break;
            }
        }

        return connectsToReachable;
    }
}