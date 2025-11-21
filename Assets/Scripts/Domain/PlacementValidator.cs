using UnityEngine;
using System;

public static class PlacementValidator
{
    public static bool CanPlace(BoardModel board, int x, int y,
                                TileDefinition def, Rotation rot)
    {
        if (def == null)
            return false;

        // клетка должна быть внутри поля
        if (!board.IsInside(x, y))
            return false;

        // и пустой
        if (!board.IsEmpty(x, y))
            return false;

        // сокеты кандидата с учётом текущего поворота
        Sockets candidateSockets = def.GetSockets(rot);

        bool hasConnection = false; // есть ли хотя бы одно соединение path–path

        // проверяем всех четырёх соседей
        foreach (Side side in Enum.GetValues(typeof(Side)))
        {
            Vector2Int offset = side.Offset();
            int nx = x + offset.x;
            int ny = y + offset.y;

            // край поля игнорируем (подземелье обрезано рамкой)
            if (!board.IsInside(nx, ny))
                continue;

            TileInstance neighbor = board.Get(nx, ny);
            if (neighbor == null)
                continue;

            bool ourEdge = candidateSockets.Get(side);
            Side opposite = SideUtil.Opposite(side);
            bool theirEdge = neighbor.Connections.Get(opposite);

            // ПРАВИЛО 1: путь–стена / стена–путь — нельзя
            if (ourEdge != theirEdge)
            {
                return false;
            }

            // путь–путь — считаем это реальным соединением
            if (ourEdge && theirEdge)
            {
                hasConnection = true;
            }
        }

        // ПРАВИЛО 2: либо есть хотя бы одно соединение,
        // либо это ПЕРВЫЙ тайл над стартовым полем.
        bool isFirstFromStart = false;

        // у нас старт находится под клеткой (2,0),
        // и у стартового тайла путь ВВЕРХ, значит у первого тайла
        // в (2,0) должен быть путь ВНИЗ
        if (x == 2 && y == 0)
        {
            if (candidateSockets.Down)
                isFirstFromStart = true;
        }

        // обычным тайлам без соединения — НЕЛЬЗЯ
        return hasConnection || isFirstFromStart;
    }
}
