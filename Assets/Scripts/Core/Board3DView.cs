using UnityEngine;

public class Board3DView : MonoBehaviour
{
    [Header("Tile defs")]
    public TileDefinition crossTile; // можно не использовать, карты сами передают tileDef

    [Header("Start tile")]
    public TileDefinition startTileDefinition; // Tile_Start (SO)
    public Transform startCellAnchor;

    [Header("Выровнять тайл относительно клетки")]
    public Vector3 tilePositionOffset = Vector3.zero; // сдвиг относительно anchor
    public float baseRotationY = 0f;                  // базовый поворот для Rotation.R0

    [Header("Превью тайла")]
    public Material previewCanMaterial;   // зелёный прозрачный
    public Material previewCantMaterial;  // красный прозрачный
    public float previewOverlayHeight = 0.02f; // насколько выше тайла лежит оверлей
    public float previewOverlayScale = 1.05f;  // чуть больше тайла по размеру

    private BoardModel board;
    private TileInstance startTileInstance;
    private Transform[,] anchors = new Transform[BoardModel.Width, BoardModel.Height];

    // данные для призрачного тайла
    private GameObject previewGO;
    private GameObject previewOverlayGO;
    private TileDefinition previewDef;

    private void Awake()
    {
        board = new BoardModel();
        anchors = new Transform[BoardModel.Width, BoardModel.Height];

        // Собираем якоря клеток (Cell_x_y) из BoardRoot/BoardMesh
        var markers = GetComponentsInChildren<BoardCellMarker>();
        foreach (var m in markers)
        {
            if (m.x < 0 || m.x >= BoardModel.Width || m.y < 0 || m.y >= BoardModel.Height)
                continue;

            anchors[m.x, m.y] = m.transform;
        }

        // создаём "виртуальный" инстанс стартового тайла (для логики)
        if (startTileDefinition != null)
        {
            startTileInstance = new TileInstance(startTileDefinition, Rotation.R0);
        }

        /*board = new BoardModel();

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
        }*/
    }
    private void Start()
    {
        SpawnStartTileVisual();
    }

    private void SpawnStartTileVisual()
    {
        if (startTileDefinition == null ||
            startTileDefinition.WorldPrefab == null ||
            startCellAnchor == null)
        {
            Debug.LogWarning("StartTileDefinition или startCellAnchor не настроены в Board3DView");
            return;
        }

        // Берём МИРОВУЮ позицию Cell_Start, но НЕ делаем префаб его дочкой
        Vector3 worldPos = startCellAnchor.position;

        // Родителем ставим тот же объект, что и для обычных тайлов (Board3DView)
        var go = Instantiate(
            startTileDefinition.WorldPrefab,
            worldPos,
            Quaternion.identity,
            transform);   // <--- ВАЖНО: transform, а не startCellAnchor

        // Доп. поворот, если надо развернуть "дверь" вверх
        // (можно сделать публичное поле startTileRotationY, если хочешь крутить в инспекторе)
        // go.transform.rotation = Quaternion.Euler(0f, startTileRotationY, 0f);
    }

    /// <summary>
    /// Проверка, можно ли поставить тайл на клетку, без изменения модели.
    /// </summary>
    public bool CanPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        if (def == null)
            return false;

        if (x < 0 || x >= BoardModel.Width || y < 0 || y >= BoardModel.Height)
            return false;

        if (board.Get(x, y) != null)
            return false;

        return PlacementValidator.CanPlace(board, startTileInstance, x, y, def, rot);
    }

    /// <summary>
    /// Попытаться поставить тайл по правилам; при успехе обновляет модель и спавнит реальный тайл.
    /// </summary>

    public bool TryPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        bool canPlace = PlacementValidator.CanPlace(board, startTileInstance, x, y, def, rot); // <-- передаём стартовый тайл

        if (!canPlace)
            return false;

        var instance = new TileInstance(def, rot);
        board.Set(x, y, instance);

        SpawnTileWorld(def, rot, x, y);

        HidePreview();

        // если у тебя есть логика скрытия превью — её оставь
        // HidePreview();

        return true;
    }


    /*public bool TryPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        if (!PlacementValidator.CanPlace(board, x, y, def, rot))
            return false;

        board.Set(x, y, new TileInstance(def, rot));
        SpawnTileWorld(def, rot, x, y);
        HidePreview();

        return true;
    }*/

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
        if (def == null)
        {
            HidePreview();
            return;
        }

        if (x < 0 || x >= BoardModel.Width || y < 0 || y >= BoardModel.Height)
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
            previewDef = def;

            // создаём или перенастраиваем оверлей
            CreateOrSetupOverlay();
        }

        // позиция и поворот такие же, как у реального тайла
        previewGO.transform.position = anchor.position;

        float yRot = baseRotationY + (int)rot;
        previewGO.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        previewGO.transform.position += previewGO.transform.TransformVector(tilePositionOffset);

        // обновляем оверлей (цвет и положение)
        if (previewOverlayGO != null)
        {
            previewOverlayGO.transform.localPosition = new Vector3(0f, previewOverlayHeight, 0f);
            previewOverlayGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            previewOverlayGO.transform.localScale = new Vector3(previewOverlayScale, previewOverlayScale, previewOverlayScale);

            var rend = previewOverlayGO.GetComponent<Renderer>();
            if (rend != null)
            {
                if (canPlace && previewCanMaterial != null)
                    rend.material = previewCanMaterial;
                else if (!canPlace && previewCantMaterial != null)
                    rend.material = previewCantMaterial;
            }
        }

        previewGO.SetActive(true);
    }

    private void CreateOrSetupOverlay()
    {
        if (previewGO == null)
            return;

        if (previewOverlayGO == null)
        {
            previewOverlayGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            previewOverlayGO.name = "PreviewOverlay";
            previewOverlayGO.transform.SetParent(previewGO.transform, false);

            // коллайдер от квадрата нам не нужен
            var col = previewOverlayGO.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
        else
        {
            previewOverlayGO.transform.SetParent(previewGO.transform, false);
        }
    }

    public void HidePreview()
    {
        if (previewGO != null)
        {
            previewGO.SetActive(false);
        }
    }
}