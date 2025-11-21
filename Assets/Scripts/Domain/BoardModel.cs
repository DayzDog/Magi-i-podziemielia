using System.Collections.Generic;
using UnityEngine;

public sealed class BoardModel
{
    public const int Width = 5;
    public const int Height = 5;

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
    /// BFS-достижимость от стартовой клетки (2,0).
    /// Использует Connections.Up/Right/Down/Left у тайлов.
    /// </summary>
    public bool[,] ComputeReachableFromStart()
    {
        var visited = new bool[Width, Height];
        var queue = new Queue<Vector2Int>();

        int sx = 2; // стартовый X (центр снизу)
        int sy = 0; // стартовый Y

        if (!IsInside(sx, sy))
            return visited;

        var first = Get(sx, sy);
        if (first == null)
            return visited;

        // В стартовую комнату можно попасть, только если у неё есть путь вниз (к стартовому тайлу)
        if (!first.Connections.Down)
            return visited;

        visited[sx, sy] = true;
        queue.Enqueue(new Vector2Int(sx, sy));

        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            var cur = Get(v.x, v.y);
            if (cur == null) continue;

            var curSockets = cur.Connections;

            foreach (Side side in System.Enum.GetValues(typeof(Side)))
            {
                var off = side.Offset();
                int nx = v.x + off.x;
                int ny = v.y + off.y;

                if (!IsInside(nx, ny) || visited[nx, ny])
                    continue;

                var nb = Get(nx, ny);
                if (nb == null) continue;

                var nbSockets = nb.Connections;

                bool ourEdge = curSockets.Get(side);
                bool theirEdge = nbSockets.Get(SideUtil.Opposite(side));

                if (ourEdge && theirEdge)
                {
                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
        }

        return visited;
    }
}