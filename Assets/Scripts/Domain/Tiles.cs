using System;
using UnityEngine;

public enum TileType
{
    Cross,   // Крестовая
    Tee,     // Т-образная
    Turn,    // Поворот
    Sanctum  // Комната с гримуаром (на будущее)
}

public enum Side
{
    Up,
    Right,
    Down,
    Left
}

public enum Rotation
{
    R0 = 0,
    R90 = 90,
    R180 = 180,
    R270 = 270
}

[Serializable]
public struct Sockets
{
    public bool Up;
    public bool Right;
    public bool Down;
    public bool Left;
}

public static class SideUtil
{
    public static Side Opposite(Side s)
    {
        return s switch
        {
            Side.Up => Side.Down,
            Side.Right => Side.Left,
            Side.Down => Side.Up,
            _ => Side.Right
        };
    }

    public static Vector2Int Offset(this Side s)
    {
        return s switch
        {
            Side.Up => new Vector2Int(0, 1),
            Side.Right => new Vector2Int(1, 0),
            Side.Down => new Vector2Int(0, -1),
            _ => new Vector2Int(-1, 0)
        };
    }

    public static bool Get(this Sockets s, Side side)
    {
        return side switch
        {
            Side.Up => s.Up,
            Side.Right => s.Right,
            Side.Down => s.Down,
            _ => s.Left
        };
    }
}