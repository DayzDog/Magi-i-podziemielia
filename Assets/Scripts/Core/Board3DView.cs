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
    public TileDefinition startTileDefinition;
    public Transform startCellAnchor;

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
        SpawnStartTileVisual();
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
        if (def.WorldPrefab == null)
        {
            Debug.LogError("У TileDefinition нет WorldPrefab!");
            return;
        }

        var anchor = anchors[x, y];
        if (anchor == null)
        {
            Debug.LogError($"Нет anchor для клетки ({x},{y})");
            return;
        }

        var go = Instantiate(def.WorldPrefab, anchor.position, Quaternion.identity, transform);
        go.transform.rotation = Quaternion.Euler(0f, (int)rot, 0f);

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
}