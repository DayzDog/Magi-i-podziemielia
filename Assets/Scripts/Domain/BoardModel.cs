using System.Collections.Generic;
using UnityEngine;

public sealed class BoardModel
{
    public const int Width = 5;
    public const int Height = 5;

    // Сетка 5×5 с тайлами
    private readonly TileInstance[,] grid = new TileInstance[Width, Height];

    public bool IsInside(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;

    public TileInstance Get(int x, int y)
        => IsInside(x, y) ? grid[x, y] : null;

    public bool IsEmpty(int x, int y)
        => IsInside(x, y) && grid[x, y] == null;

    public void Set(int x, int y, TileInstance tile)
    {
        if (IsInside(x, y))
            grid[x, y] = tile;
    }

    /// <summary>
    /// BFS-достижимость от виртуального старта: (2, -1) -> (2,0) через Down.
    /// Нужна, чтобы не было «островков» оторванных от выхода.
    /// </summary>
    public bool[,] ComputeReachableFromStart()
    {
        var visited = new bool[Width, Height];
        var queue = new Queue<Vector2Int>();

        int sx = 2; // центр по X
        int sy = 0; // нижний ряд

        if (!IsInside(sx, sy))
            return visited;

        var first = Get(sx, sy);

        // В стартовую комнату можно попасть, только если у неё есть путь вниз
        if (first != null && first.Def.GetSockets(first.Rot).Down)
        {
            visited[sx, sy] = true;
            queue.Enqueue(new Vector2Int(sx, sy));
        }

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            var cur = Get(v.x, v.y);
            if (cur == null) continue;

            var curSockets = cur.Def.GetSockets(cur.Rot);

            foreach (Side side in System.Enum.GetValues(typeof(Side)))
            {
                var off = side.Offset();
                int nx = v.x + off.x;
                int ny = v.y + off.y;

                if (!IsInside(nx, ny) || visited[nx, ny]) continue;

                var nb = Get(nx, ny);
                if (nb == null) continue;

                var nbSockets = nb.Def.GetSockets(nb.Rot);

                if (curSockets.Get(side) && nbSockets.Get(SideUtil.Opposite(side)))
                {
                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        return visited;
    }
}