using System;

public sealed class TileInstance
{
    /// <summary>ScriptableObject тайла.</summary>
    public TileDefinition Def { get; }

    /// <summary>Поворот тайла (0 / 90 / 180 / 270).</summary>
    public Rotation Rot { get; set; }

    /// <summary>Текущие соединения (куда есть путь). Можно менять магией.</summary>
    public Sockets Connections { get; set; }

    public TileInstance(TileDefinition def, Rotation rot)
    {
        Def = def ?? throw new ArgumentNullException(nameof(def));
        Rot = rot;
        Connections = def.GetSockets(rot);
    }
}
