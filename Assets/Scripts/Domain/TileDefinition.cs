using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public TileType Type;

    // Картинка для UI (карта в руке, мини-иконка на ячейке)
    public Sprite Sprite;

    // 3D-модель тайла для мира
    public GameObject WorldPrefab;

    public Sockets BaseSockets;

    public Sockets GetSockets(Rotation rotation)
    {
        var s = BaseSockets;
        int steps = ((int)rotation / 90) & 3;
        for (int i = 0; i < steps; i++)
            s = new Sockets { Up = s.Left, Right = s.Up, Down = s.Right, Left = s.Down };
        return s;
    }
}

public sealed class TileInstance
{
    public TileDefinition Def;
    public Rotation Rot;

    public TileInstance(TileDefinition def, Rotation rot)
    {
        Def = def;
        Rot = rot;
    }
}