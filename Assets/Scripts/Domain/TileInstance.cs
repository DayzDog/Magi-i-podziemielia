using UnityEngine;

/// <summary>
/// Конкретный тайл на поле: какой деф, какой поворот и какие стороны открыты.
/// </summary>
public class TileInstance
{
    public TileDefinition Definition { get; private set; }
    public Rotation Rotation { get; set; }

    // Готовые Up/Right/Down/Left для этого инстанса
    public Sockets Connections;

    public TileInstance(TileDefinition def, Rotation rot)
    {
        Definition = def;
        Rotation = rot;
        Connections = def.GetSockets(rot);
    }

    public bool Get(Side side)
    {
        return Connections.Get(side);
    }
}