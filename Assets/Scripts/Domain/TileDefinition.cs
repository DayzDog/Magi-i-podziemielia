using UnityEngine;

// ScriptableObject, описывающий тип тайла:
// какая у него форма при повороте R0 и какой префаб рисовать.
[CreateAssetMenu(menuName = "Dungeon/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public TileType tileType;

    [Header("Базовые сокеты для R0 (Up/Right/Down/Left)")]
    public Sockets baseSockets;   // используем твой struct Sockets

    [Header("Префаб внешнего вида")]
    public GameObject WorldPrefab;

    /// <summary>
    /// Вернуть сокеты (Up/Right/Down/Left) для конкретного поворота.
    /// </summary>
    public Sockets GetSockets(Rotation rot)
    {
        Sockets s = baseSockets;

        int steps = ((int)rot / 90) % 4; // 0,1,2,3

        for (int i = 0; i < steps; i++)
        {
            s = RotateClockwise(s);
        }

        return s;
    }

    private static Sockets RotateClockwise(Sockets src)
    {
        // Up -> Right, Right -> Down, Down -> Left, Left -> Up
        return new Sockets
        {
            Up = src.Left,
            Right = src.Up,
            Down = src.Right,
            Left = src.Down
        };
    }
}
