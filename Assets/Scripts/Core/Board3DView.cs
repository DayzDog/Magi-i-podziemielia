using UnityEngine;

public class Board3DView : MonoBehaviour
{
    [Header("Tile defs")]
    public TileDefinition crossTile; // временно только крест для теста

    [Header("Выровнять тайл относительно клетки")]
    public Vector3 tilePositionOffset = Vector3.zero; // сдвиг относительно anchor
    public float baseRotationY = 0f;                  // базовый поворот для Rotation.R0

    private BoardModel board;
    private Transform[,] anchors = new Transform[BoardModel.Width, BoardModel.Height];

    private void Awake()
    {
        board = new BoardModel();

        // ищем все маркеры-клетки внутри этого BoardRoot
        var markers = GetComponentsInChildren<BoardCellMarker>();
        foreach (var m in markers)
        {
            if (m.x < 0 || m.x >= BoardModel.Width || m.y < 0 || m.y >= BoardModel.Height)
            {
                Debug.LogWarning($"Cell marker {m.name} has invalid coords ({m.x},{m.y})");
                continue;
            }

            anchors[m.x, m.y] = m.transform;
        }
    }

    /// <summary>
    /// Попытаться поставить тайл на клетку (x,y) по правилам.
    /// Возвращает true, если всё ок.
    /// </summary>
    public bool TryPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        if (def == null)
        {
            Debug.LogError("TryPlaceTile: def == null");
            return false;
        }

        if (!board.IsInside(x, y))
        {
            Debug.LogWarning($"TryPlaceTile: ({x},{y}) вне поля");
            return false;
        }

        if (board.Get(x, y) != null)
        {
            Debug.Log($"TryPlaceTile: клетка ({x},{y}) уже занята");
            return false;
        }

        bool canPlace = PlacementValidator.CanPlace(board, x, y, def, rot);
        Debug.Log($"TryPlaceTile: CanPlace({x},{y}) = {canPlace}");

        if (!canPlace)
            return false;

        // записываем в модель
        board.Set(x, y, new TileInstance(def, rot));

        // рисуем в мире
        SpawnTileWorld(def, rot, x, y);
        return true;
    }

    private void SpawnTileWorld(TileDefinition def, Rotation rot, int x, int y)
    {
        if (def.WorldPrefab == null)
        {
            Debug.LogError("У TileDefinition нет WorldPrefab! Проверь Tile_Cross.");
            return;
        }

        var anchor = anchors[x, y];
        if (anchor == null)
        {
            Debug.LogError($"Нет anchor для клетки ({x},{y}) — проверь BoardCellMarker.");
            return;
        }

        // спауним префаб прямо в точку клетки
        var go = Instantiate(def.WorldPrefab, anchor.position, Quaternion.identity, transform);

        // Общий поворот: базовый угол + угол по Rotation (0,90,180,270)
        float yRot = baseRotationY + (int)rot;
        go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

        // Дополнительный сдвиг (если pivot не в центре клетки)
        go.transform.position += go.transform.TransformVector(tilePositionOffset);
    }
}