using UnityEngine;
using System;

public static class PlacementValidator
{
    public static bool CanPlace(
        BoardModel board,
        TileInstance startTile,      // <-- НОВОЕ: тайл старта
        int x,
        int y,
        TileDefinition def,
        Rotation rot)
    {
        if (def == null)
            return false;

        if (!board.IsInside(x, y))
            return false;

        if (!board.IsEmpty(x, y))
            return false;

        // сокеты кандидата с учётом поворота
        Sockets candidateSockets = def.GetSockets(rot);

        bool hasConnection = false; // есть ли хотя бы одно path–path

        // 1) проверяем соседей на самом поле 5×5
        foreach (Side side in Enum.GetValues(typeof(Side)))
        {
            Vector2Int offset = side.Offset();
            int nx = x + offset.x;
            int ny = y + offset.y;

            if (!board.IsInside(nx, ny))
                continue;

            TileInstance neighbor = board.Get(nx, ny);
            if (neighbor == null)
                continue;

            bool ourEdge = candidateSockets.Get(side);
            Side opposite = SideUtil.Opposite(side);
            bool theirEdge = neighbor.Connections.Get(opposite);

            // путь–стена / стена–путь запрещаем
            if (ourEdge != theirEdge)
                return false;

            // путь–путь — считаем соединением
            if (ourEdge && theirEdge)
                hasConnection = true;
        }

        // 2) отдельный случай: сосед "снизу" — стартовый тайл
        // логически старт стоит под клеткой (2,0), на виртуальной позиции (2,-1)
        if (startTile != null && x == 2 && y == 0)
        {
            bool ourEdge = candidateSockets.Down;          // наш путь вниз
            bool theirEdge = startTile.Connections.Up;       // путь старта вверх

            if (ourEdge != theirEdge)
                return false;

            if (ourEdge && theirEdge)
                hasConnection = true;
        }

        // 3) запрещаем островки: хотя бы одно соединение должно быть
        return hasConnection;
    }
}
