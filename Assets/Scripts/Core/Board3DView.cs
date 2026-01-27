using System;
using UnityEngine;
using System.Collections.Generic;

public class Board3DView : MonoBehaviour
{
    [Header("Tile defs")]
    public TileDefinition crossTile;

    [Header("Sanctum / Гримуар")]
    public TileDefinition sanctumTileDefinition;
    [Header("Sanctum visuals")]
    public SanctumVisualEntry[] sanctumVisuals;

    [Header("Start tile")]
    public TileDefinition startTileDefinition;        // обычный стартовый тайл (дверь открыта вверх)
    public Transform startCellAnchor;
    // НОВОЕ: отдельный деф для "заблокированного" старта
    public TileDefinition startTileBlockedDefinition; // вариант, когда проход наверх закрыт камнем


    [Header("Выровнять тайл относительно клетки")]
    public Vector3 tilePositionOffset = Vector3.zero;
    public float baseRotationY = 0f;

    [Header("Превью тайла")]
    public Material previewCanMaterial;
    public Material previewCantMaterial;
    public float previewOverlayHeight = 0.02f;
    public float previewOverlayScale = 1.05f;

    [Header("Mage")]
    public GameObject magePrefab;
    public float mageHeightOffset = 0.2f;

    [Header("Movement highlight (ghost)")]
    public GameObject moveHighlightPrefab;
    public float moveHighlightHeight = 0.01f;
    public Material moveAllowedMaterial;
    public Material moveBlockedMaterial;

    [Header("Dungeon tile set (auto rebuild)")]
    public TileDefinition tileCrossDef;    // Tile_Cross
    public TileDefinition tileStraightDef; // Tile_Straight
    public TileDefinition tileTurnDef;     // Tile_Turn
    public TileDefinition tileTeeDef;      // Tile_Tee
    public TileDefinition tileDeadEndDef;  // Tile_Dead_End
    public TileDefinition tileBlockedDef;  // Tile_Blocked (полный тупик)

    // --- состояние поля ---
    private BoardModel board;
    private Transform[,] anchors = new Transform[BoardModel.Width, BoardModel.Height];
    private TileWorld[,] tileWorlds;
    private TileWorld startTileWorld;
    private TileInstance startTileInstance;

    // --- маг ---
    private GameObject mageGO;
    private bool mageOnStart = true;
    private int mageX = 2;
    private int mageY = 0;

    // --- санктум ---
    private int sanctumX;
    private int sanctumY = 4;
    private bool sanctumRevealed;
    private Sockets sanctumSockets;
    private TileInstance sanctumInstance;
    private GameObject sanctumWorldGO;

    // --- превью тайла ---
    private GameObject previewGO;
    private GameObject previewOverlayGO;
    private TileDefinition previewDef;

    // --- хайлайты ходов ---
    private readonly List<GameObject> activeMoveHighlights = new List<GameObject>();

    [Serializable]
    public class SanctumVisualEntry
    {
        public bool up;
        public bool right;
        public bool down;
        public bool left;
        public GameObject prefab;
    }

    private void Awake()
    {
        board = new BoardModel();
        anchors = new Transform[BoardModel.Width, BoardModel.Height];
        tileWorlds = new TileWorld[BoardModel.Width, BoardModel.Height];

        // Собираем якоря клеток (Cell_x_y) из BoardRoot/BoardMesh
        var markers = GetComponentsInChildren<BoardCellMarker>();
        foreach (var m in markers)
        {
            if (m.x < 0 || m.x >= BoardModel.Width || m.y < 0 || m.y >= BoardModel.Height)
                continue;

            anchors[m.x, m.y] = m.transform;
        }

        InitSanctum();

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
    private void InitSanctum()
    {
        // выбираем случайный X из [0..4]
        sanctumX = UnityEngine.Random.Range(0, BoardModel.Width);
        sanctumY = 4; // верхний ряд

        sanctumRevealed = false;

        // Начальные сокеты в зависимости от позиции
        // y=4 — дальний край, значит Up всегда стена (false)
        sanctumSockets.Up = false;
        sanctumSockets.Down = true; // во всех вариантах есть путь вниз

        if (sanctumX == 0)
        {
            // Левый угол: поворот вниз+вправо
            sanctumSockets.Left = false;
            sanctumSockets.Right = true;
        }
        else if (sanctumX == BoardModel.Width - 1) // x == 4
        {
            // Правый угол: поворот вниз+влево
            sanctumSockets.Left = true;
            sanctumSockets.Right = false;
        }
        else
        {
            // Средние 3 клетки: T-образный тайл (вниз, влево и вправо)
            sanctumSockets.Left = true;
            sanctumSockets.Right = true;
        }

        sanctumInstance = null;
        sanctumWorldGO = null;

        Debug.Log($"[Sanctum] Hidden at ({sanctumX},{sanctumY}), sockets: " +
                  $"U:{sanctumSockets.Up} R:{sanctumSockets.Right} D:{sanctumSockets.Down} L:{sanctumSockets.Left}");
    }

    private GameObject GetSanctumPrefabForSockets(Sockets s)
    {
        if (sanctumVisuals != null)
        {
            foreach (var entry in sanctumVisuals)
            {
                if (entry == null || entry.prefab == null)
                    continue;

                if (entry.up == s.Up &&
                    entry.right == s.Right &&
                    entry.down == s.Down &&
                    entry.left == s.Left)
                {
                    return entry.prefab;
                }
            }
        }

        // если ничего не нашли — используем дефолтный префаб из TileDefinition
        if (sanctumTileDefinition != null)
            return sanctumTileDefinition.WorldPrefab;

        return null;
    }

    private void HandleSanctumAfterPlacement(int x, int y, TileInstance placed)
    {
        if (sanctumRevealed)
            return; // уже открыт — пока ничего особенного не делаем

        // Проверяем все 4 стороны тайла: не стоит ли рядом скрытый Гримуар
        foreach (Side side in Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            if (nx != sanctumX || ny != sanctumY)
                continue; // этот сосед не Гримуар

            // Мы стоим рядом с клеткой Гримуара.
            bool pathFromTile = placed.Connections.Get(side);

            // Сторона Гримуара, которая смотрит на нас — противоположная
            Side sanctumSide = SideUtil.Opposite(side);
            bool pathFromSanctum = sanctumSockets.Get(sanctumSide);

            if (pathFromTile && pathFromSanctum)
            {
                // Путь-путь: раскрываем комнату с гримуаром
                RevealSanctum();
            }
            else if (!pathFromTile && pathFromSanctum)
            {
                // У тайла стена к Гримуару: не раскрываем, но "запоминаем" стену
                // т.е. эта сторона Sanctum тоже становится стеной
                SetSanctumSocket(sanctumSide, false);
                Debug.Log($"[Sanctum] Side {sanctumSide} blocked by wall at ({x},{y})");
            }
            // остальные случаи:
            //  - pathFromTile && !pathFromSanctum → путь уткнулся в уже "заблокированную" сторону
            //  - !pathFromTile && !pathFromSanctum → стена к стене
            // просто ничего не меняем
        }
    }

    private void SetSanctumSocket(Side side, bool value)
    {
        switch (side)
        {
            case Side.Up: sanctumSockets.Up = value; break;
            case Side.Right: sanctumSockets.Right = value; break;
            case Side.Down: sanctumSockets.Down = value; break;
            case Side.Left: sanctumSockets.Left = value; break;
        }
    }


    private void RevealSanctum()
    {
        if (sanctumRevealed)
            return;

        sanctumRevealed = true;
        Debug.Log($"[Sanctum] Revealed at ({sanctumX},{sanctumY})");

        if (sanctumTileDefinition == null)
        {
            Debug.LogWarning("SanctumTileDefinition не назначен, не могу заспавнить модель");
            return;
        }

        // Создаём инстанс в модели
        sanctumInstance = new TileInstance(sanctumTileDefinition, Rotation.R0);
        // Перезаписываем Connections в соответствии с накопленными сокетами
        sanctumInstance.Connections = sanctumSockets;

        // Пишем его в BoardModel, чтобы дальнейшая логика (передвижения и т.п.) его видела
        board.Set(sanctumX, sanctumY, sanctumInstance);

        // Визуал: спавним 3D-модель в нужной клетке
        var anchor = anchors[sanctumX, sanctumY];
        if (anchor == null)
        {
            Debug.LogWarning("Нет anchor для клетки с Гримуаром");
            return;
        }

        // выбираем подходящий префаб по текущим sanctumSockets
        GameObject prefab = GetSanctumPrefabForSockets(sanctumSockets);
        if (prefab == null)
        {
            Debug.LogWarning("Не найден подходящий префаб для текущих сокетов Sanctum");
            return;
        }

        sanctumWorldGO = Instantiate(prefab, anchor.position, Quaternion.identity, transform);

        var tw = sanctumWorldGO.GetComponent<TileWorld>();
        if (tw == null) tw = sanctumWorldGO.AddComponent<TileWorld>();

        tw.board = this;
        tw.x = sanctumX;
        tw.y = sanctumY;
        tw.isStart = false;

        tileWorlds[sanctumX, sanctumY] = tw;

        // TODO: сюда позже можно добавить выбор подходящего префаба
        // по sanctumSockets (угол, T, полностью окружённый и т.д.)
    }

    private void SpawnMage()
    {
        if (magePrefab == null || startCellAnchor == null)
        {
            Debug.LogWarning("MagePrefab или startCellAnchor не назначены");
            return;
        }

        Vector3 pos = startCellAnchor.position + Vector3.up * mageHeightOffset;
        mageGO = Instantiate(magePrefab, pos, Quaternion.identity, transform);

        var pawn = mageGO.GetComponent<MagePawn>();
        if (pawn != null)
            pawn.board = this;

        mageOnStart = true;
        mageX = 2;
        mageY = 0;
    }
    private void ClearHighlights()
    {
        // сбрасываем флаг хода у всех тайлов
        if (tileWorlds != null)
        {
            for (int x = 0; x < BoardModel.Width; x++)
                for (int y = 0; y < BoardModel.Height; y++)
                {
                    var tw = tileWorlds[x, y];
                    if (tw != null)
                        tw.canMoveTarget = false;
                }
        }

        if (startTileWorld != null)
            startTileWorld.canMoveTarget = false;

        // удаляем все призраки
        foreach (var g in activeMoveHighlights)
        {
            if (g != null)
                Destroy(g);
        }
        activeMoveHighlights.Clear();
    }
    private void PaintGhost(GameObject ghost, bool canMove)
    {
        if (ghost == null) return;

        var rend = ghost.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        var mat = canMove ? moveAllowedMaterial : moveBlockedMaterial;
        if (mat == null) return;

        var mats = rend.materials;
        for (int i = 0; i < mats.Length; i++)
            mats[i] = mat;
        rend.materials = mats;
    }

    private void HighlightTile(int x, int y, bool canMove)
    {
        // логика — сюда можно ходить или нет
        var tw = tileWorlds[x, y];
        if (tw != null)
            tw.canMoveTarget = canMove;

        // визуал
        if (moveHighlightPrefab == null) return;

        var anchor = anchors[x, y];
        if (anchor == null) return;

        var ghost = Instantiate(
            moveHighlightPrefab,
            anchor.position + Vector3.up * moveHighlightHeight,
            Quaternion.identity,
            transform);

        PaintGhost(ghost, canMove);
        activeMoveHighlights.Add(ghost);
    }
    private void HighlightStart(bool canMove)
    {
        if (startTileWorld != null)
            startTileWorld.canMoveTarget = canMove;

        if (moveHighlightPrefab == null || startCellAnchor == null)
            return;

        var ghost = Instantiate(
            moveHighlightPrefab,
            startCellAnchor.position + Vector3.up * moveHighlightHeight,
            Quaternion.identity,
            transform);

        PaintGhost(ghost, canMove);
        activeMoveHighlights.Add(ghost);
    }

    public void OnMageClicked()
    {
        ClearHighlights();

        if (mageOnStart)
            ShowMovesFromStart();
        else
            ShowMovesFromCell();
    }

    private void ShowMovesFromStart()
    {
        // Маг на старте, единственный возможный ход — в клетку (2,0) над стартом
        int x = 2;
        int y = 0;

        TileInstance neighbor = board.Get(x, y);
        if (neighbor == null)
            return; // комнаты ещё нет

        bool fromPath = startTileInstance != null ? startTileInstance.Connections.Up : false;
        bool toPath = neighbor.Connections.Down;

        bool canMove = fromPath && toPath;

        HighlightTile(x, y, canMove);
    }

    private void ShowMovesFromCell()
    {
        TileInstance current = board.Get(mageX, mageY);
        if (current == null)
            return;

        Sockets curSockets = current.Connections;

        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = mageX + off.x;
            int ny = mageY + off.y;

            // Особый случай: из клетки (2,0) вниз на старт
            if (mageX == 2 && mageY == 0 && side == Side.Down)
            {
                bool fromPathStart = curSockets.Down;
                bool toPathStart = startTileInstance != null
                    ? startTileInstance.Connections.Up
                    : false;

                bool canMoveStart = fromPathStart && toPathStart;
                HighlightStart(canMoveStart);
                continue;
            }

            if (!board.IsInside(nx, ny))
                continue;

            TileInstance neighbor = board.Get(nx, ny);
            if (neighbor == null)
                continue; // нет комнаты — не подсвечиваем

            bool fromPath = curSockets.Get(side);
            Side opposite = SideUtil.Opposite(side);
            bool toPath = neighbor.Connections.Get(opposite);

            bool canMove = fromPath && toPath;
            HighlightTile(nx, ny, canMove);
        }
    }


    public void OnTileClicked(TileWorld tile)
    {
        if (tile == null || !tile.canMoveTarget)
            return; // клики по непроходимым/несветящимся игнорируем

        if (tile.isStart)
        {
            MoveMageToStart();
        }
        else
        {
            MoveMageToCell(tile.x, tile.y);
        }
    }

    private void MoveMageToCell(int x, int y)
    {
        mageOnStart = false;
        mageX = x;
        mageY = y;

        Transform anchor = anchors[x, y];
        if (anchor != null && mageGO != null)
        {
            Vector3 pos = anchor.position + Vector3.up * mageHeightOffset;
            mageGO.transform.position = pos;
        }

        ClearHighlights();
    }

    private void MoveMageToStart()
    {
        mageOnStart = true;

        if (startCellAnchor != null && mageGO != null)
        {
            Vector3 pos = startCellAnchor.position + Vector3.up * mageHeightOffset;
            mageGO.transform.position = pos;
        }

        ClearHighlights();
    }


    private void Start()
    {
        RefreshStartTileVisual();  // <-- новый метод вместо SpawnStartTileVisual
        SpawnMage();
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

        var go = Instantiate(startTileDefinition.WorldPrefab,
                         startCellAnchor.position,
                         Quaternion.identity,
                         transform);

        // ...

        var tw = go.GetComponent<TileWorld>();
        if (tw == null) tw = go.AddComponent<TileWorld>();

        tw.board = this;
        tw.isStart = true;
        tw.x = 2;
        tw.y = 0;

        startTileWorld = tw;

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

        // НОВОЕ: сообщаем системе Гримуара, что мы поставили тайл
        HandleSanctumAfterPlacement(x, y, instance);

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
        if (def == null || def.WorldPrefab == null)
        {
            Debug.LogError("SpawnTileWorld: TileDefinition или WorldPrefab == null");
            return;
        }

        var anchor = anchors[x, y];
        if (anchor == null)
        {
            Debug.LogError($"SpawnTileWorld: нет anchor для клетки ({x},{y})");
            return;
        }

        // Сносим старый визуал, если был
        var oldTw = tileWorlds[x, y];
        if (oldTw != null && oldTw.gameObject != null)
            Destroy(oldTw.gameObject);

        // Создаём новый
        var go = Instantiate(
            def.WorldPrefab,
            anchor.position,
            Quaternion.Euler(0f, (int)rot, 0f),
            transform);

        var tw = go.GetComponent<TileWorld>();
        if (tw == null) tw = go.AddComponent<TileWorld>();

        tw.board = this;
        tw.x = x;
        tw.y = y;
        tw.isStart = false;

        tileWorlds[x, y] = tw;
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

    private void SetConnection(TileInstance tile, Side side, bool value)
    {
        if (tile == null) return;

        var c = tile.Connections;

        switch (side)
        {
            case Side.Up: c.Up = value; break;
            case Side.Right: c.Right = value; break;
            case Side.Down: c.Down = value; break;
            case Side.Left: c.Left = value; break;
        }

        tile.Connections = c;
    }

    private Rotation RotateRotation(Rotation rot, int steps90)
    {
        int angle = (int)rot + steps90 * 90;
        angle %= 360;
        if (angle < 0) angle += 360;

        switch (angle)
        {
            case 0: return Rotation.R0;
            case 90: return Rotation.R90;
            case 180: return Rotation.R180;
            case 270: return Rotation.R270;
            default: return Rotation.R0;
        }
    }
    /// <summary>
    /// Магия камня: переключить проход между клеткой, где стоит маг,
    /// и соседней клеткой по указанной стороне.
    /// </summary>
    public bool TryCastStone(Side side)
    {
        TileInstance current;
        int cx, cy;

        if (mageOnStart)
        {
            // Маг на стартовом тайле: логическая клетка над ним (2,0).
            // По правилам старт имеет только проход ВВЕРХ.
            if (side != Side.Up)
                return false;

            current = startTileInstance;
            cx = 2;
            cy = 0;
        }
        else
        {
            current = board.Get(mageX, mageY);
            cx = mageX;
            cy = mageY;
        }

        if (current == null)
            return false;

        Vector2Int off = side.Offset();
        int nx = cx + off.x;
        int ny = cy + off.y;

        // Стены края подземелья ломать нельзя
        if (!board.IsInside(nx, ny))
            return false;

        TileInstance neighbor = board.Get(nx, ny);
        if (neighbor == null)
            return false; // камень работает только между существующими комнатами

        bool curEdge = current.Connections.Get(side);
        Side opposite = SideUtil.Opposite(side);
        bool neighEdge = neighbor.Connections.Get(opposite);

        // Проход есть только если пути с обеих сторон
        bool isOpenNow = curEdge && neighEdge;
        bool newState = !isOpenNow; // инверсия

        SetConnection(current, side, newState);
        SetConnection(neighbor, opposite, newState);

        // если сосед — гримуар, обновляем его сокеты
        if (neighbor == sanctumInstance)
            SetSanctumSocket(opposite, newState);

        ClearHighlights(); // ходы мага изменились

        return true;
    }
    /// <summary>
    /// Магия камня по клетке: карта была брошена на клетку (tx,ty).
    /// </summary>
    /// <summary>
    /// Магия камня: переключает стену между клеткой под магом и целевой клеткой (x,y).
    /// </summary>
    /// <summary>
    /// Магия камня: переключает стену между комнатой под магом и соседней клеткой (x,y).
    /// Плюс спец-случай для связи START ↔ (2,0).
    /// </summary>
    public bool TryCastStoneOnCell(int x, int y)
    {
        // === 0. Маг стоит на старте ===
        // Камнем можно воздействовать только на клетку (2,0).
        if (mageOnStart)
        {
            if (x == 2 && y == 0)
                return TryCastStoneBetweenStartAndFirstCell();

            Debug.Log("[Stone] На старте камнем можно воздействовать только на клетку (2,0).");
            return false;
        }

        // === 1. Маг внутри подземелья ===

        if (!board.IsInside(x, y))
            return false;

        TileInstance current = board.Get(mageX, mageY);
        if (current == null)
        {
            Debug.Log("[Stone] Под магом нет комнаты, камень не сработал.");
            return false;
        }

        // Определяем направление от мага к цели
        int dx = x - mageX;
        int dy = y - mageY;

        Side side;
        if (dx == 0 && dy == 1) side = Side.Up;
        else if (dx == 1 && dy == 0) side = Side.Right;
        else if (dx == 0 && dy == -1) side = Side.Down;
        else if (dx == -1 && dy == 0) side = Side.Left;
        else
        {
            Debug.Log("[Stone] Цель не является соседней клеткой, камень не сработал.");
            return false;
        }

        Sockets curSockets = current.Connections;

        // === 1.A. Если целевая клетка — SANCTUM ===
        if (IsSanctumCell(x, y))
        {
            // Здесь работаем не через TileInstance, а напрямую с sanctumSockets
            Side opposite = SideUtil.Opposite(side);

            bool curEdge = curSockets.Get(side);
            bool sanctumEdge = sanctumSockets.Get(opposite);

            // Логика та же: если хотя бы у одной стороны был путь – закрываем,
            // если обе были стеной – открываем.
            bool newEdge = !(curEdge || sanctumEdge);

            // Записываем в комнату мага
            switch (side)
            {
                case Side.Up: curSockets.Up = newEdge; break;
                case Side.Right: curSockets.Right = newEdge; break;
                case Side.Down: curSockets.Down = newEdge; break;
                case Side.Left: curSockets.Left = newEdge; break;
            }

            // И в sanctumSockets (сторона, смотрящая на мага)
            switch (opposite)
            {
                case Side.Up: sanctumSockets.Up = newEdge; break;
                case Side.Right: sanctumSockets.Right = newEdge; break;
                case Side.Down: sanctumSockets.Down = newEdge; break;
                case Side.Left: sanctumSockets.Left = newEdge; break;
            }

            current.Connections = curSockets;

            // Если Sanctum уже раскрыт, у него есть sanctumInstance и 3D-модель
            if (sanctumInstance != null)
                sanctumInstance.Connections = sanctumSockets;

            if (sanctumRevealed)
                RefreshSanctumVisual(); // переcобирает только визуал Sanctum

            // Перерисуем комнату под магом
            RebuildTileVisual(mageX, mageY);

            Debug.Log($"[Stone] Между комнатой ({mageX},{mageY}) и SANCTUM ({x},{y}) проход: {(newEdge ? "есть" : "нет")}.");
            return true;
        }

        // === 1.B. Обычные клетки (с тайлом или пустые) ===

        TileInstance neighbor = board.Get(x, y);

        // --- СЛУЧАЙ: целевая клетка пустая ---
        if (neighbor == null)
        {
            // Меняем только сторону комнаты под магом.
            bool curEdge = curSockets.Get(side);
            bool newEdge = !curEdge; // просто инвертируем путь/стену

            switch (side)
            {
                case Side.Up: curSockets.Up = newEdge; break;
                case Side.Right: curSockets.Right = newEdge; break;
                case Side.Down: curSockets.Down = newEdge; break;
                case Side.Left: curSockets.Left = newEdge; break;
            }

            current.Connections = curSockets;

            RebuildTileVisual(mageX, mageY);

            Debug.Log($"[Stone] Клетка ({x},{y}) пуста, " +
                      $"сторона {side} у комнаты ({mageX},{mageY}) теперь {(newEdge ? "ПУТЬ" : "СТЕНА")}.");

            // Это как раз твой пункт 4: можно заранее «подготовить» стену/проход
            // для будущего строительства.
            return true;
        }

        // --- СЛУЧАЙ: обычная соседняя комната ---

        Sockets nbSockets = neighbor.Connections;

        bool curEdge2 = curSockets.Get(side);
        Side opposite2 = SideUtil.Opposite(side);
        bool nbEdge2 = nbSockets.Get(opposite2);

        // Если хоть у кого-то есть путь — делаем стены, если оба стены — открываем
        bool newEdge2 = !(curEdge2 || nbEdge2);

        // Записываем в текущую комнату
        switch (side)
        {
            case Side.Up: curSockets.Up = newEdge2; break;
            case Side.Right: curSockets.Right = newEdge2; break;
            case Side.Down: curSockets.Down = newEdge2; break;
            case Side.Left: curSockets.Left = newEdge2; break;
        }

        // И в соседа (с противоположной стороны)
        switch (opposite2)
        {
            case Side.Up: nbSockets.Up = newEdge2; break;
            case Side.Right: nbSockets.Right = newEdge2; break;
            case Side.Down: nbSockets.Down = newEdge2; break;
            case Side.Left: nbSockets.Left = newEdge2; break;
        }

        current.Connections = curSockets;
        neighbor.Connections = nbSockets;

        Debug.Log($"[Stone] Между клетками ({mageX},{mageY}) и ({x},{y}) теперь проход: {(newEdge2 ? "есть" : "нет")}.");

        // Перерисуем только эти две комнаты
        RebuildTileVisual(mageX, mageY);
        RebuildTileVisual(x, y);

        return true;
    }



    /// <summary>
    /// Магия камня: переключает проход между стартовым тайлом и клеткой (2,0).
    /// Работает, только если маг стоит на старте или на клетке (2,0).
    /// </summary>
    /// <summary>
    /// Магия камня: переключает проход между стартом и клеткой (2,0).
    /// Работает, только если маг на старте или на (2,0).
    /// </summary>
    public bool TryCastStoneBetweenStartAndFirstCell()
    {
        // маг должен быть либо на старте, либо на (2,0)
        bool mageHere =
            mageOnStart ||
            (!mageOnStart && mageX == 2 && mageY == 0);

        if (!mageHere)
        {
            Debug.Log("[Stone] Между стартом и (2,0) можно колдовать только стоя на старте или на клетке (2,0).");
            return false;
        }

        if (startTileInstance == null)
        {
            Debug.LogWarning("[Stone] startTileInstance == null, не могу переключить стену.");
            return false;
        }

        TileInstance cell20 = board.Get(2, 0);
        if (cell20 == null)
        {
            Debug.LogWarning("[Stone] В клетке (2,0) нет комнаты, нечего перекрывать.");
            return false;
        }

        // читаем текущие соединения
        Sockets startSockets = startTileInstance.Connections;
        Sockets cellSockets = cell20.Connections;

        bool edgeStart = startSockets.Up;   // у старта вход/выход вверх
        bool edgeCell = cellSockets.Down;  // у клетки (2,0) вход/выход вниз

        // если где-то есть путь — делаем стену, если стенка с обеих сторон — открываем путь
        bool newEdge = !(edgeStart || edgeCell);

        startSockets.Up = newEdge;
        cellSockets.Down = newEdge;

        startTileInstance.Connections = startSockets;
        cell20.Connections = cellSockets;


        Debug.Log($"[Stone] Стена между START и (2,0) теперь: {(newEdge ? "открыта (путь есть)" : "закрыта (стена)")}.");

        return true;
    }


    // Магия воды: смывает комнату, оставляя пустую клетку,
    // на которую потом можно снова строить по обычным правилам.
    public bool TryCastWaterOnCell(int x, int y)
    {
        // 0. Нельзя лить воду на Sanctum ни в каком состоянии
        if (IsSanctumCell(x, y))
        {
            Debug.Log("[Water] Нельзя смывать Sanctum.");
            return false;
        }

        // 1. Границы поля
        if (!board.IsInside(x, y))
            return false;

        // 2. Нельзя смывать комнату, в которой стоит маг
        if (!mageOnStart && mageX == x && mageY == y)
        {
            Debug.Log("[Water] Нельзя смывать комнату под магом.");
            return false;
        }

        // 3. Берём тайл из модели
        TileInstance tile = board.Get(x, y);
        if (tile == null)
        {
            Debug.Log("[Water] На этой клетке уже пусто, смывать нечего.");
            return false;
        }

        // 4. Очищаем модель: клетка считается ПУСТОЙ
        board.Set(x, y, null);

        // 5. Обновляем визуал только этой клетки
        RebuildTileVisual(x, y);

        Debug.Log($"[Water] Клетка ({x},{y}) смыта. Теперь она пустая и по правилам можно строить заново.");
        return true;
    }


    /// <summary>
    /// Магия воздуха: если карта брошена по тайлу/клетке, где стоит маг,
    /// показываем подсветку возможных ходов (как при клике по магу).
    /// </summary>
    public bool TryCastAirOnMage()
    {
        // Просто показываем возможные ходы
        OnMageClicked();
        return true;
    }

    public bool TryCastAirOnCell(int x, int y)
    {
        // Если маг стоит на этой клетке — то же самое, что TryCastAirOnMage
        if (!mageOnStart && mageX == x && mageY == y)
        {
            OnMageClicked();
            return true;
        }

        // Если маг на старте, а целевая клетка (2,0) — тоже считаем, что попали по магу
        if (mageOnStart && x == 2 && y == 0)
        {
            OnMageClicked();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Магия воздуха: если карту бросили прямо на модель мага.
    /// </summary>


    /// <summary>
    /// Магия огня по координатам поля.
    /// Сейчас работает только если это клетка, где стоит маг.
    /// </summary>
    public bool TryCastFireOnCell(int x, int y)
    {
        if (!board.IsInside(x, y))
            return false;

        // ===== 1. SANCTUM =====
        if (IsSanctumCell(x, y))
        {
            // Я предлагаю крутить только открытый Sanctum.
            // Если хочешь, можно убрать эту проверку и крутить скрытый тоже.
            if (!sanctumRevealed)
                return false;

            // d4 поворотов и случайное направление (влево/вправо)
            int steps = UnityEngine.Random.Range(1, 5);   // 1..4
            int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;
            int totalSteps = steps * dir;

            // Поворачиваем именно sanctumSockets
            Sockets s = RotateSockets(sanctumSockets, totalSteps);

            // Не даём выходам уходить за край поля
            foreach (Side side in System.Enum.GetValues(typeof(Side)))
            {
                Vector2Int off = side.Offset();
                int nx = x + off.x;
                int ny = y + off.y;

                if (s.Get(side) && !board.IsInside(nx, ny))
                {
                    switch (side)
                    {
                        case Side.Up: s.Up = false; break;
                        case Side.Right: s.Right = false; break;
                        case Side.Down: s.Down = false; break;
                        case Side.Left: s.Left = false; break;
                    }
                }
            }

            // Сохраняем новые сокеты Sanctum
            sanctumSockets = s;
            if (sanctumInstance != null)
                sanctumInstance.Connections = sanctumSockets;

            // Подгоняем соседей под новые сокеты Sanctum
            foreach (Side side in System.Enum.GetValues(typeof(Side)))
            {
                Vector2Int off = side.Offset();
                int nx = x + off.x;
                int ny = y + off.y;

                if (!board.IsInside(nx, ny))
                    continue;

                TileInstance nb = board.Get(nx, ny);
                if (nb == null)
                    continue;

                Side opp = SideUtil.Opposite(side);
                bool edge = sanctumSockets.Get(side);

                Sockets nbSockets = nb.Connections;
                switch (opp)
                {
                    case Side.Up: nbSockets.Up = edge; break;
                    case Side.Right: nbSockets.Right = edge; break;
                    case Side.Down: nbSockets.Down = edge; break;
                    case Side.Left: nbSockets.Left = edge; break;
                }

                nb.Connections = nbSockets;
                RebuildTileVisual(nx, ny);
            }

            // Обновляем именно визуал Sanctum, не обычный тайл
            RefreshSanctumVisual();
            return true;
        }

        // ===== 2. ОБЫЧНЫЙ ТАЙЛ =====

        TileInstance tile = board.Get(x, y);
        if (tile == null)
            return false;

        // d4 поворотов и случайное направление
        int stepsTile = UnityEngine.Random.Range(1, 5);   // 1..4
        int dirTile = UnityEngine.Random.value < 0.5f ? 1 : -1;
        int totalStepsTile = stepsTile * dirTile;

        // крутим ТЕКУЩИЕ сокеты (с учётом камня)
        Sockets after = RotateSockets(tile.Connections, totalStepsTile);

        // режем выходы за границу поля
        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            if (after.Get(side) && !board.IsInside(nx, ny))
            {
                switch (side)
                {
                    case Side.Up: after.Up = false; break;
                    case Side.Right: after.Right = false; break;
                    case Side.Down: after.Down = false; break;
                    case Side.Left: after.Left = false; break;
                }
            }
        }

        // Подгоняем соседей (Sanctum здесь пропускаем — он уже
        // обрабатывается отдельно, когда огонь кидаем в сам Sanctum)
        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            if (!board.IsInside(nx, ny))
                continue;
            if (IsSanctumCell(nx, ny))
                continue;

            TileInstance nb = board.Get(nx, ny);
            if (nb == null)
                continue;

            Side opp = SideUtil.Opposite(side);
            bool edge = after.Get(side);

            Sockets nbSockets = nb.Connections;
            switch (opp)
            {
                case Side.Up: nbSockets.Up = edge; break;
                case Side.Right: nbSockets.Right = edge; break;
                case Side.Down: nbSockets.Down = edge; break;
                case Side.Left: nbSockets.Left = edge; break;
            }

            nb.Connections = nbSockets;
            RebuildTileVisual(nx, ny);
        }

        // сохраняем новые сокеты самого тайла и перерисовываем его
        tile.Connections = after;
        RebuildTileVisual(x, y);

        return true;
    }


    private static Sockets RotateSockets(Sockets s, int steps)
    {
        // нормализуем шаги к [0..3]
        steps = ((steps % 4) + 4) % 4;

        for (int i = 0; i < steps; i++)
        {
            // поворот по часовой стрелке:
            // Up -> Right -> Down -> Left -> Up
            bool up = s.Up;
            s.Up = s.Left;
            s.Left = s.Down;
            s.Down = s.Right;
            s.Right = up;
        }

        return s;
    }



    /// <summary>
    /// Магия огня: поворачивает комнату под магом на 90° по часовой
    /// и обновляет соединения с соседями.
    /// </summary>
    public bool TryCastFireOnCurrent()
    {
        if (mageOnStart)
        {
            Debug.Log("[Fire] Маг на старте, под ним нет комнаты. Огонь не сработал.");
            return false;
        }

        if (!board.IsInside(mageX, mageY))
            return false;

        TileInstance current = board.Get(mageX, mageY);
        if (current == null)
        {
            Debug.Log("[Fire] Под магом нет комнаты, огонь не сработал.");
            return false;
        }

        // Поворачиваем тайл на 90° по часовой
        int newRot = ((int)current.Rot + 90) % 360;
        current.Rot = (Rotation)newRot;

        // Пересчитываем сокеты на основе определения тайла
        if (current.Def != null)
        {
            current.Connections = current.Def.GetSockets(current.Rot);
        }

        // Теперь приводим соседей в соответствие с новым положением
        Sockets curSockets = current.Connections;

        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = mageX + off.x;
            int ny = mageY + off.y;

            if (!board.IsInside(nx, ny))
                continue;

            TileInstance neighbor = board.Get(nx, ny);
            if (neighbor == null)
                continue;

            Sockets nbSockets = neighbor.Connections;

            bool ourEdge = curSockets.Get(side);
            Side opposite = SideUtil.Opposite(side);
            bool nbEdge = nbSockets.Get(opposite);

            // Если сосед не совпадает с нашим новым путём — делаем его таким же
            if (ourEdge != nbEdge)
            {
                switch (opposite)
                {
                    case Side.Up: nbSockets.Up = ourEdge; break;
                    case Side.Right: nbSockets.Right = ourEdge; break;
                    case Side.Down: nbSockets.Down = ourEdge; break;
                    case Side.Left: nbSockets.Left = ourEdge; break;
                }

                neighbor.Connections = nbSockets;

             
            }
        }

        Debug.Log($"[Fire] Повернули комнату под магом в ({mageX},{mageY}) до Rot={current.Rot}.");

        RebuildAllTilesFromModel();
        return true;

    }

    private static bool SameSockets(Sockets a, Sockets b)
    {
        return a.Up == b.Up
            && a.Right == b.Right
            && a.Down == b.Down
            && a.Left == b.Left;
    }

    // --- Подбор формы и поворота по текущим Connections ---
    /// <summary>
    /// По набору сокетов выбираем форму тайла (Cross/Tee/Turn/Straight/DeadEnd/Blocked)
    /// и нужный поворот.
    /// ВАЖНО: TileDefinitions настроены так:
    /// - crossDef: Up,Right,Down,Left = true
    /// - teeDef:   Up,Right,Left = true, Down = false
    /// - cornerDef (Turn): Up,Right = true, Down,Left = false
    /// - straightDef: Up,Down = true, Right,Left = false
    /// - deadEndDef: только Down = true
    /// - blockedDef: все false
    /// </summary>
    /// <summary>
    /// По набору сокетов (Up/Right/Down/Left) подбирает подходящий TileDefinition
    /// и поворот, перебирая ВСЕ формы и ВСЕ 4 поворота.
    /// Больше не зависит от того, как именно ты повернул модели.
    /// </summary>
    private bool TryPickShapeBySockets(
        Sockets sockets,
        out TileDefinition resultDef,
        out Rotation resultRot)
    {
        resultDef = null;
        resultRot = Rotation.R0;

        // Набор всех форм подземелья
        TileDefinition[] shapes =
        {
        tileCrossDef,
        tileStraightDef,
        tileTurnDef,
        tileTeeDef,
        tileDeadEndDef,
        tileBlockedDef
    };

        foreach (var def in shapes)
        {
            if (def == null)
                continue;

            // Перебираем все 4 поворота и спрашиваем сам TileDefinition,
            // какие сокеты будут при таком повороте.
            foreach (Rotation rot in System.Enum.GetValues(typeof(Rotation)))
            {
                Sockets s = def.GetSockets(rot);

                if (SameSockets(s, sockets))
                {
                    resultDef = def;
                    resultRot = rot;
                    return true;
                }
            }
        }

        // Ничего не нашли – пусть вызывающий код решает, что делать.
        return false;
    }



    // --- Полная пересборка всех 25 тайлов из модели board ---
    private void RebuildAllTilesFromModel()
    {
        if (board == null)
            return;

        for (int x = 0; x < BoardModel.Width; x++)
        {
            for (int y = 0; y < BoardModel.Height; y++)
            {
                if (IsSanctumCell(x, y))
                    continue;

                RebuildTileVisual(x, y);
            }
        }

        if (sanctumRevealed)
            RefreshSanctumVisual();
    }

    private bool IsSanctumCell(int x, int y)
    {
        return x == sanctumX && y == sanctumY;
    }

    private void RefreshSanctumVisual()
    {
        if (!sanctumRevealed)
            return;

        var anchor = anchors[sanctumX, sanctumY];
        if (anchor == null)
        {
            Debug.LogWarning("[Sanctum] Anchor для Sanctum не найден");
            return;
        }

        // Удаляем старый визуал, если был
        if (sanctumWorldGO != null)
        {
            Destroy(sanctumWorldGO);
            sanctumWorldGO = null;
        }

        GameObject prefab = GetSanctumPrefabForSockets(sanctumSockets);
        if (prefab == null)
        {
            Debug.LogWarning("[Sanctum] Нет префаба для текущих sanctumSockets");
            return;
        }

        sanctumWorldGO = Instantiate(prefab, anchor.position, Quaternion.identity, transform);

        var tw = sanctumWorldGO.GetComponent<TileWorld>();
        if (tw == null) tw = sanctumWorldGO.AddComponent<TileWorld>();

        tw.board = this;
        tw.x = sanctumX;
        tw.y = sanctumY;
        tw.isStart = false;

        tileWorlds[sanctumX, sanctumY] = tw;
    }
    private void RebuildTileVisual(int x, int y)
    {
        if (!board.IsInside(x, y))
            return;

        // Санктум — через отдельный код
        if (IsSanctumCell(x, y))
        {
            if (sanctumRevealed)
                RefreshSanctumVisual();
            return;
        }

        TileInstance tile = board.Get(x, y);

        // Пустая клетка — удаляем визуал
        if (tile == null)
        {
            var oldTw = tileWorlds[x, y];
            if (oldTw != null && oldTw.gameObject != null)
                Destroy(oldTw.gameObject);

            tileWorlds[x, y] = null;
            return;
        }

        TileDefinition def;
        Rotation rot;

        if (!TryPickShapeBySockets(tile.Connections, out def, out rot))
        {
            def = tile.Def;
            rot = tile.Rot;
        }

        SpawnTileWorld(def, rot, x, y);
    }
    private void RefreshStartTileVisual()
    {
        if (startCellAnchor == null || startTileDefinition == null)
            return;

        // Удаляем старый визуал
        if (startTileWorld != null && startTileWorld.gameObject != null)
            Destroy(startTileWorld.gameObject);

        // Какой префаб использовать — открытый или заблокированный
        TileDefinition defToUse = startTileDefinition;

        // Если есть отдельный "заблокированный" деф и вверх у старта закрыт
        if (startTileBlockedDefinition != null &&
            (startTileInstance == null || !startTileInstance.Connections.Up))
        {
            defToUse = startTileBlockedDefinition;
        }

        var go = Instantiate(defToUse.WorldPrefab,
                             startCellAnchor.position,
                             Quaternion.identity,
                             transform);

        var tw = go.GetComponent<TileWorld>();
        if (tw == null) tw = go.AddComponent<TileWorld>();

        tw.board = this;
        tw.isStart = true;
        tw.x = 2;
        tw.y = 0;

        startTileWorld = tw;
    }


}