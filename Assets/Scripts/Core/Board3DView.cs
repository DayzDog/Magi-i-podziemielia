using UnityEngine;

public class Board3DView : MonoBehaviour
{
    [Header("Tile defs")]
    public TileDefinition crossTile; // пока что можешь игнорировать, мы используем tileDef из карты

    [Header("Выровнять тайл относительно клетки")]
    public Vector3 tilePositionOffset = Vector3.zero; // сдвиг относительно anchor
    public float baseRotationY = 0f;                  // базовый поворот для Rotation.R0

    [Header("Превью тайла")]
    public Color previewCanColor = new Color(0f, 1f, 0f, 0.5f);
    public Color previewCantColor = new Color(1f, 0f, 0f, 0.5f);

    private BoardModel board;
    private Transform[,] anchors = new Transform[BoardModel.Width, BoardModel.Height];

    // данные для призрачного тайла
    private GameObject previewGO;
    private Renderer[] previewRenderers;
    private TileDefinition previewDef; // какой деф сейчас визуализируем

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
    /// Проверка, можно ли поставить тайл на клетку, без изменения модели.
    /// </summary>
    public bool CanPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        if (def == null)
            return false;

        if (!board.IsInside(x, y))
            return false;

        if (board.Get(x, y) != null)
            return false;

        return PlacementValidator.CanPlace(board, x, y, def, rot);
    }

    /// <summary>
    /// Попытаться поставить тайл по правилам; при успехе обновляет модель и спавнит реальный тайл.
    /// </summary>
    public bool TryPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        if (!CanPlaceTile(def, rot, x, y))
            return false;

        board.Set(x, y, new TileInstance(def, rot));
        SpawnTileWorld(def, rot, x, y);

        // после реального размещения убираем призрак (если был)
        HidePreview();

        return true;
    }

    private void SpawnTileWorld(TileDefinition def, Rotation rot, int x, int y)
    {
        if (def.WorldPrefab == null)
        {
            Debug.LogError("У TileDefinition нет WorldPrefab! Проверь TileDefinition.");
            return;
        }

        var anchor = anchors[x, y];
        if (anchor == null)
        {
            Debug.LogError($"Нет anchor для клетки ({x},{y}) — проверь BoardCellMarker.");
            return;
        }

        var go = Instantiate(def.WorldPrefab, anchor.position, Quaternion.identity, transform);

        float yRot = baseRotationY + (int)rot;
        go.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        go.transform.position += go.transform.TransformVector(tilePositionOffset);
    }

    /// <summary>
    /// Обновляет/показывает призрачный тайл на клетке (x,y).
    /// </summary>
    public void UpdatePreview(TileDefinition def, Rotation rot, int x, int y, bool canPlace)
    {
        if (def == null || !board.IsInside(x, y))
        {
            HidePreview();
            return;
        }

        var anchor = anchors[x, y];
        if (anchor == null)
        {
            HidePreview();
            return;
        }

        // если нет превью или деф сменился – создаём новый призрак
        if (previewGO == null || previewDef != def)
        {
            if (previewGO != null)
                Destroy(previewGO);

            previewGO = Instantiate(def.WorldPrefab, anchor.position, Quaternion.identity, transform);
            previewRenderers = previewGO.GetComponentsInChildren<Renderer>();
            previewDef = def;
        }

        // позиция и поворот такие же, как у реального тайла
        previewGO.transform.position = anchor.position;

        float yRot = baseRotationY + (int)rot;
        previewGO.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        previewGO.transform.position += previewGO.transform.TransformVector(tilePositionOffset);

        // цвет в зависимости от валидности
        Color c = canPlace ? previewCanColor : previewCantColor;

        if (previewRenderers != null)
        {
            foreach (var r in previewRenderers)
            {
                if (r == null) continue;
                // renderer.material создаёт копию материала для этого инстанса — для прототипа норм
                if (r.material.HasProperty("_Color"))
                {
                    var col = c;
                    r.material.color = col;
                }
            }
        }

        previewGO.SetActive(true);
    }

    public void HidePreview()
    {
        if (previewGO != null)
        {
            previewGO.SetActive(false);
        }
    }
}
