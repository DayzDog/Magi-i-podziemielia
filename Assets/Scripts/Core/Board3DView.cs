using System;
using System.Collections.Generic;
using UnityEngine;

public class Board3DView : MonoBehaviour
{
    [Header("Owner")]
    public int ownerPlayerId = 1; // 1 или 2

    [Header("Tile defs (общие)")]
    public TileDefinition crossTile; // можно не использовать

    [Header("Sanctum / Гримуар")]
    public TileDefinition sanctumTileDefinition;
    public SanctumVisualEntry[] sanctumVisuals;

    [Header("Start tile")]
    public TileDefinition startTileDefinition;        // обычный стартовый тайл (дверь открыта вверх)
    public TileDefinition startTileBlockedDefinition; // вариант, когда проход наверх закрыт камнем
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

    [Header("Dungeon tile set (auto rebuild)")]
    public TileDefinition tileCrossDef;    // Tile_Cross
    public TileDefinition tileStraightDef; // Tile_Straight
    public TileDefinition tileTurnDef;     // Tile_Turn
    public TileDefinition tileTeeDef;      // Tile_Tee
    public TileDefinition tileDeadEndDef;  // Tile_Dead_End
    public TileDefinition tileBlockedDef;  // Tile_Blocked (полный тупик)

    // --- состояние поля ---
    private BoardModel board;
    private Transform[,] anchors;
    private TileWorld[,] tileWorlds;
    private TileWorld startTileWorld;
    private TileInstance startTileInstance;

    // --- маг ---
    private GameObject mageGO;
    private bool mageOnStart = true;
    private int mageX = 2;
    private int mageY = 0;

    // NEW: флаг, взят ли гримуар, и победа
    private bool mageHasGrimoire = false;
    public bool MageHasGrimoire => mageHasGrimoire;
    public bool GameWon { get; private set; } = false;

    // --- санктум ---
    private int sanctumX;
    private int sanctumY = 4;
    private bool sanctumRevealed;
    private Sockets sanctumSockets;
    private TileInstance sanctumInstance;
    private GameObject sanctumWorldGO;
    private SanctumSyncManager sanctumSync;
    private bool sanctumConfigured;

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

    // Публичные геттеры для TurnManager
    public bool HasGrimoire => mageHasGrimoire;      // или MageHasGrimoire
    public bool IsMageOnStartTile => mageOnStart;    // маг сейчас на Cell_start
    public bool IsWinnerNow() => HasGrimoire && IsMageOnStartTile;

    #region Init / Awake / Start

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
        // создаём "виртуальный" инстанс стартового тайла (для логики)
        if (startTileDefinition != null)
        {
            startTileInstance = new TileInstance(startTileDefinition, Rotation.R0);
        }
    }

    private void Start()
    {
        if (!sanctumConfigured)
        {
            Debug.LogError("[Board3DView] Sanctum НЕ настроен. В дуэли он обязан приходить из SanctumSyncManager.");
            enabled = false; // чтобы не было частично-рабочего состояния
            return;
        }
        RefreshStartTileVisual();
        SpawnMage();
    }

    //private void InitSanctum()
    // {
    // sanctumX = UnityEngine.Random.Range(0, BoardModel.Width);
    //  sanctumY = 4; // верхний ряд
    //
    // sanctumRevealed = false;
    //
    // y=4 — дальний край, значит Up всегда стена (false)
    // sanctumSockets.Up = false;
    // sanctumSockets.Down = true; // во всех вариантах есть путь вниз
    //
    //     if (sanctumX == 0)
    //    {
    // Левый угол: поворот вниз+вправо
    //  sanctumSockets.Left = false;
    // sanctumSockets.Right = true;
    //}
    //   else if (sanctumX == BoardModel.Width - 1)
    //{
    // Правый угол: поворот вниз+влево
    // sanctumSockets.Left = true;
    //  sanctumSockets.Right = false;
    //}
    // else
    //  {
    // Средние 3 клетки: T-образный тайл (вниз, влево и вправо)
    //  sanctumSockets.Left = true;
    // sanctumSockets.Right = true;
    //}
    //
    // sanctumInstance = null;
    // sanctumWorldGO = null;
    //
    //Debug.Log($"[Sanctum] Hidden at ({sanctumX},{sanctumY}), sockets: " +
    //           $"U:{sanctumSockets.Up} R:{sanctumSockets.Right} D:{sanctumSockets.Down} L:{sanctumSockets.Left}");
    //}

    #endregion

    #region Sanctum

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
            return;

        // Проверяем все 4 стороны тайла: не стоит ли рядом скрытый Гримуар
        foreach (Side side in Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            if (nx != sanctumX || ny != sanctumY)
                continue; // этот сосед не Гримуар

            bool pathFromTile = placed.Connections.Get(side);

            Side sanctumSide = SideUtil.Opposite(side);
            bool pathFromSanctum = sanctumSockets.Get(sanctumSide);

            if (pathFromTile && pathFromSanctum)
            {
                RevealSanctum();
            }
            else if (!pathFromTile && pathFromSanctum)
            {
                SetSanctumSocket(sanctumSide, false);
                Debug.Log($"[Sanctum] Side {sanctumSide} blocked by wall at ({x},{y})");
            }
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
        RevealSanctumInternal(notifySync: true);
    }

    private void RevealSanctumInternal(bool notifySync)
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

        sanctumInstance = new TileInstance(sanctumTileDefinition, Rotation.R0);
        sanctumInstance.Connections = sanctumSockets;
        board.Set(sanctumX, sanctumY, sanctumInstance);

        RefreshSanctumVisual();

        // ВАЖНО: если нужно — уведомляем синк-менеджер, чтобы раскрыть Sanctum на второй доске
        if (notifySync && sanctumSync != null)
            sanctumSync.NotifyBoardRevealed(this);
    }

    // ВАЖНО: после того как маг забрал Гримуар, клетка больше не считается Sanctum.
    private bool IsSanctumCell(int x, int y)
    {
        return !mageHasGrimoire && x == sanctumX && y == sanctumY;
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

    /// <summary>
    /// Маг зашёл в Sanctum, подбирает Гримуар, а клетка превращается
    /// в обычный тайл той же формы и поворота.
    /// </summary>
    private void PickupGrimoireAndReplaceSanctum()
    {
        mageHasGrimoire = true;
        Debug.Log("[Game] Маг подобрал Гримуар!");

        // Подбираем подходящую форму по текущим sanctumSockets
        TileDefinition def;
        Rotation rot;
        if (!TryPickShapeBySockets(sanctumSockets, out def, out rot))
        {
            // Фолбек – берём любой доступный деф
            def = tileCrossDef ?? tileStraightDef ?? tileTurnDef ??
                  tileTeeDef ?? tileDeadEndDef ?? tileBlockedDef;

            rot = Rotation.R0;
        }

        // Обновляем модель: в клетке Sanctum теперь обычная комната
        var inst = new TileInstance(def, rot);
        inst.Connections = sanctumSockets;
        board.Set(sanctumX, sanctumY, inst);

        // Сносим старый визуал Sanctum
        if (sanctumWorldGO != null)
        {
            Destroy(sanctumWorldGO);
            sanctumWorldGO = null;
        }

        // Пересобираем визуал как обычный тайл
        RebuildTileVisual(sanctumX, sanctumY);
    }

    public void ConfigureSharedSanctum(int sharedX, SanctumSyncManager sync)
    {
        sanctumSync = sync;
        sanctumConfigured = true;

        sanctumX = Mathf.Clamp(sharedX, 0, BoardModel.Width - 1);
        sanctumY = 4;

        sanctumRevealed = false;

        // ВАЖНО: sanctumSockets считаем локально для ЭТОЙ доски
        sanctumSockets = BuildInitialSanctumSockets(sanctumX);

        sanctumInstance = null;
        sanctumWorldGO = null;

        Debug.Log($"[Sanctum] Configured shared at ({sanctumX},{sanctumY}) sockets: " +
                  $"U:{sanctumSockets.Up} R:{sanctumSockets.Right} D:{sanctumSockets.Down} L:{sanctumSockets.Left}");
    }

    public void ForceRevealSanctumFromSync()
    {
        RevealSanctumInternal(notifySync: false);
    }

    private Sockets BuildInitialSanctumSockets(int x)
    {
        Sockets s = new Sockets();

        // Верхняя граница всегда стена
        s.Up = false;

        // В данж Sanctum "смотрит" вниз
        s.Down = true;

        if (x == 0)
        {
            // левый угол: вниз + вправо
            s.Left = false;
            s.Right = true;
        }
        else if (x == BoardModel.Width - 1)
        {
            // правый угол: вниз + влево
            s.Left = true;
            s.Right = false;
        }
        else
        {
            // середина: T (вниз + влево + вправо)
            s.Left = true;
            s.Right = true;
        }

        return s;
    }
   
    private void OnSanctumEdgeChangedFromNeighbor(int neighborX, int neighborY, Side sideFromNeighborToSanctum, bool edgeToSanctum)
    {
        // 1) вычисляем, какая сторона Sanctum смотрит на соседа
        Side sanctumSide = SideUtil.Opposite(sideFromNeighborToSanctum);

        // 2) обновляем сокеты Sanctum
        SetSanctumSocket(sanctumSide, edgeToSanctum);

        // 3) если Sanctum уже раскрыт — обязательно обновляем модель и визуал
        if (sanctumRevealed)
        {
            if (sanctumInstance != null)
                sanctumInstance.Connections = sanctumSockets;

            // на всякий случай гарантируем, что в BoardModel лежит именно sanctumInstance
            if (board.Get(sanctumX, sanctumY) != sanctumInstance)
                board.Set(sanctumX, sanctumY, sanctumInstance);

            RefreshSanctumVisual();
            return;
        }

        // 4) если Sanctum ещё скрыт и мы открыли к нему путь — раскрываем
        if (edgeToSanctum)
        {
            RevealSanctum(); // внутри уже создаст sanctumInstance, запишет в board и нарисует
        }
    }
    #endregion

    #region Mage + Movement

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
        var tw = tileWorlds[x, y];
        if (tw != null)
            tw.canMoveTarget = canMove;

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
        if (TurnManager.I != null && !TurnManager.I.CanMoveOnBoard(this))
            return;
        ClearHighlights();

        if (mageOnStart)
            ShowMovesFromStart();
        else
            ShowMovesFromCell();
    }

    private void ShowMovesFromStart()
    {
        int x = 2;
        int y = 0;

        TileInstance neighbor = board.Get(x, y);
        if (neighbor == null)
            return;

        bool fromPath = startTileInstance != null && startTileInstance.Connections.Up;
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

        foreach (Side side in Enum.GetValues(typeof(Side)))
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
                continue;

            bool fromPath = curSockets.Get(side);
            Side opposite = SideUtil.Opposite(side);
            bool toPath = neighbor.Connections.Get(opposite);

            bool canMove = fromPath && toPath;
            HighlightTile(nx, ny, canMove);
        }
    }

    public void OnTileClicked(TileWorld tile)
    {
        if (TurnManager.I != null && !TurnManager.I.CanMoveOnBoard(this))
            return;

        if (tile == null || !tile.canMoveTarget)
            return;

        if (tile.isStart)
            MoveMageToStart();
        else
            MoveMageToCell(tile.x, tile.y);
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
        TurnManager.I?.NotifyMoveDone(ownerPlayerId);
        // Проверяем: наступили ли на Sanctum, чтобы забрать Гримуар
        if (!mageHasGrimoire && sanctumRevealed && IsSanctumCell(x, y))
        {
            PickupGrimoireAndReplaceSanctum();
        }
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
        TurnManager.I?.NotifyMoveDone(ownerPlayerId);
        // Проверка победы: маг вернулся на старт С ГРИМУАРОМ
        if (mageHasGrimoire && !GameWon)
        {
            GameWon = true;
            Debug.Log("[Game] ПОБЕДА: маг вернулся на старт с Гримуаром!");
            // Здесь позже можно вызвать событие / показать UI и т.п.
        }
    }

    #endregion

    #region Стартовый тайл

    private void RefreshStartTileVisual()
    {
        if (startCellAnchor == null || startTileDefinition == null)
            return;

        if (startTileWorld != null && startTileWorld.gameObject != null)
            Destroy(startTileWorld.gameObject);

        TileDefinition defToUse = startTileDefinition;

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

    #endregion

    #region Постановка тайлов + превью

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

    public bool TryPlaceTile(TileDefinition def, Rotation rot, int x, int y)
    {
        bool canPlace = PlacementValidator.CanPlace(board, startTileInstance, x, y, def, rot);
        if (!canPlace)
            return false;

        var instance = new TileInstance(def, rot);
        board.Set(x, y, instance);

        SpawnTileWorld(def, rot, x, y);
        HidePreview();

        HandleSanctumAfterPlacement(x, y, instance);

        return true;
    }

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

        var oldTw = tileWorlds[x, y];
        if (oldTw != null && oldTw.gameObject != null)
            Destroy(oldTw.gameObject);

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

    public void UpdatePreview(TileDefinition def, Rotation rot, int x, int y, bool canPlace)
    {
        if (def == null ||
            x < 0 || x >= BoardModel.Width ||
            y < 0 || y >= BoardModel.Height)
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

        if (previewGO == null || previewDef != def)
        {
            if (previewGO != null)
                Destroy(previewGO);

            previewGO = Instantiate(def.WorldPrefab, anchor.position, Quaternion.identity, transform);
            previewDef = def;

            CreateOrSetupOverlay();
        }

        previewGO.transform.position = anchor.position;

        float yRot = baseRotationY + (int)rot;
        previewGO.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        previewGO.transform.position += previewGO.transform.TransformVector(tilePositionOffset);

        if (previewOverlayGO != null)
        {
            previewOverlayGO.transform.localPosition = new Vector3(0f, previewOverlayHeight, 0f);
            previewOverlayGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            previewOverlayGO.transform.localScale =
                new Vector3(previewOverlayScale, previewOverlayScale, previewOverlayScale);

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
            previewGO.SetActive(false);
    }

    #endregion

    #region Вспомогалки для сокетов/поворотов

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

    private static Sockets RotateSockets(Sockets s, int steps)
    {
        steps = ((steps % 4) + 4) % 4;

        for (int i = 0; i < steps; i++)
        {
            bool up = s.Up;
            s.Up = s.Left;
            s.Left = s.Down;
            s.Down = s.Right;
            s.Right = up;
        }

        return s;
    }

    private static bool SameSockets(Sockets a, Sockets b)
    {
        return a.Up == b.Up
            && a.Right == b.Right
            && a.Down == b.Down
            && a.Left == b.Left;
    }

    #endregion

    #region Магия камня

    /// <summary>
    /// Магия камня по направлению от мага (для кнопок/горячих клавиш).
    /// Внутри просто пересчитывает целевую клетку и вызывает TryCastStoneOnCell.
    /// </summary>
    /// <summary>
    /// Магия камня по направлению от мага (для кнопок/горячих клавиш).
    /// Внутри просто пересчитывает целевую клетку и вызывает TryCastStoneOnCell
    /// или специальный метод для связки START ↔ (2,0).
    /// </summary>
    public bool TryCastStone(Side side)
    {
        int tx, ty;

        if (mageOnStart)
        {
            // Со старта можно колдовать камнем только ВВЕРХ в клетку (2,0)
            if (side != Side.Up)
                return false;

            tx = 2;
            ty = 0;
        }
        else
        {
            // ОСОБЫЙ СЛУЧАЙ: маг стоит на (2,0) и колдует ВНИЗ (к старту)
            if (mageX == 2 && mageY == 0 && side == Side.Down)
            {
                // Это означает "переключить проход START ↔ (2,0)"
                return TryCastStoneBetweenStartAndFirstCell();
            }

            // Обычный случай – соседняя клетка на поле
            Vector2Int off = side.Offset();
            tx = mageX + off.x;
            ty = mageY + off.y;
        }

        return TryCastStoneOnCell(tx, ty);
    }

    /// <summary>
    /// Магия камня: переключает стену между комнатой под магом и соседней клеткой (x,y).
    /// Плюс спец-случай для связи START ↔ (2,0).
    /// Вызывается и из кнопок, и из карточек.
    /// </summary>
    /// <summary>
    /// Магия камня: переключает стену между комнатой под магом и соседней клеткой (x,y).
    /// Плюс спец-случай для связи START ↔ (2,0).
    /// Вызывается и из кнопок, и из карточек.
    /// </summary>
    public bool TryCastStoneOnCell(int x, int y)
    {
        // СПЕЦ-СЛУЧАЙ №1:
        // Маг стоит на клетке (2,0), а карта камня попала в "зону" (2,0)
        // (по лучу мы для старта тоже получаем эти координаты).
        if (!mageOnStart && mageX == 2 && mageY == 0 && x == 2 && y == 0)
        {
            // Считаем, что игрок переключает проход START ↔ (2,0)
            return TryCastStoneBetweenStartAndFirstCell();
        }

        // === 0. Маг стоит на старте ===
        // На старте камнем можно воздействовать ТОЛЬКО на клетку (2,0).
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
            bool curEdge = curSockets.Get(side);
            Side opposite = SideUtil.Opposite(side);
            bool sanctumEdge = sanctumSockets.Get(opposite);

            bool openNow = curEdge && sanctumEdge;
            bool newEdge = !openNow;

            // меняем сторону у тайла мага
            switch (side)
            {
                case Side.Up: curSockets.Up = newEdge; break;
                case Side.Right: curSockets.Right = newEdge; break;
                case Side.Down: curSockets.Down = newEdge; break;
                case Side.Left: curSockets.Left = newEdge; break;
            }

            current.Connections = curSockets;

            // Sanctum обновляем через единую точку
            OnSanctumEdgeChangedFromNeighbor(mageX, mageY, side, newEdge);

            RebuildTileVisual(mageX, mageY);

            Debug.Log($"[Stone] Между комнатой ({mageX},{mageY}) и SANCTUM ({x},{y}) проход: {(newEdge ? "есть" : "нет")}.");
            return true;
        }

        // === 1.B. Целевая клетка ПУСТАЯ ===

        TileInstance neighbor = board.Get(x, y);
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

            Debug.Log($"[Stone] Клетка ({x},{y}) пуста, сторона {side} у комнаты ({mageX},{mageY}) теперь {(newEdge ? "ПУТЬ" : "СТЕНА")}.");
            return true;
        }

        // === 1.C. Обычная соседняя комната ===

        Sockets nbSockets = neighbor.Connections;

        bool curEdge2 = curSockets.Get(side);
        Side opposite2 = SideUtil.Opposite(side);
        bool nbEdge2 = nbSockets.Get(opposite2);

        // Если хоть у кого-то есть путь — делаем стены.
        // Если оба были стеной — открываем путь.
        bool newEdge2 = !(curEdge2 || nbEdge2);

        switch (side)
        {
            case Side.Up: curSockets.Up = newEdge2; break;
            case Side.Right: curSockets.Right = newEdge2; break;
            case Side.Down: curSockets.Down = newEdge2; break;
            case Side.Left: curSockets.Left = newEdge2; break;
        }

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

        // ОБНОВЛЯЕМ ВИЗУАЛ старта и клетки (2,0)
        RefreshStartTileVisual();
        RebuildTileVisual(2, 0);

        return true;
    }

    #endregion

    #region Магия воды

    public bool TryCastWaterOnCell(int x, int y)
    {
        // 0. Нельзя лить воду на Sanctum
        if (IsSanctumCell(x, y))
        {
            Debug.Log("[Water] Нельзя смывать Sanctum.");
            return false;
        }

        // 1. Границы поля
        if (!board.IsInside(x, y))
            return false;

        // 2. Определяем «центр» дальности – откуда колдуем
        //    Если маг на старте, считаем что он колдует из клетки (2,0)
        int centerX = mageOnStart ? 2 : mageX;
        int centerY = mageOnStart ? 0 : mageY;

        int dx = x - centerX;
        int dy = y - centerY;

        // Можно только по соседним клеткам по горизонтали / вертикали / диагонали
        if (Mathf.Abs(dx) > 1 || Mathf.Abs(dy) > 1)
        {
            Debug.Log("[Water] Можно смывать только соседние клетки вокруг мага.");
            return false;
        }

        // 3. Нельзя смывать комнату, на которой СТОИТ маг
        if (!mageOnStart && mageX == x && mageY == y)
        {
            Debug.Log("[Water] Нельзя смывать комнату под магом.");
            return false;
        }

        // 4. Берём тайл из модели
        TileInstance tile = board.Get(x, y);
        if (tile == null)
        {
            Debug.Log("[Water] На этой клетке уже пусто, смывать нечего.");
            return false;
        }

        // 5. Очищаем модель: клетка становится пустой
        board.Set(x, y, null);

        // 6. Обновляем визуал только этой клетки
        RebuildTileVisual(x, y);

        Debug.Log($"[Water] Клетка ({x},{y}) смыта. Теперь она пустая и по правилам можно строить заново.");
        return true;
    }

    #endregion

    #region Магия воздуха

    public bool TryCastAirOnMage()
    {
        OnMageClicked();
        return true;
    }

    public bool TryCastAirOnCell(int x, int y)
    {
        if (!mageOnStart && mageX == x && mageY == y)
        {
            OnMageClicked();
            return true;
        }

        if (mageOnStart && x == 2 && y == 0)
        {
            OnMageClicked();
            return true;
        }

        return false;
    }

    #endregion

    #region Магия огня

    /// <summary>
    /// Магия огня по координатам поля (используется картой).
    /// </summary>
    public bool TryCastFireOnCell(int x, int y)
    {
        if (!board.IsInside(x, y))
            return false;

        // ===== 1. ОГОНЬ ПО SANCTUM =====
        if (IsSanctumCell(x, y))
        {
            // Крутим только уже раскрытый Sanctum
            if (!sanctumRevealed)
                return false;

            // d4 поворотов и случайное направление (влево/вправо)
            int steps = UnityEngine.Random.Range(1, 5);   // 1..4
            int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;
            int totalSteps = steps * dir;

            // Поворачиваем sanctumSockets
            Sockets s = RotateSockets(sanctumSockets, totalSteps);

            // НЕ режем выходы за край поля — путь может смотреть в "стену мира"
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

            RefreshSanctumVisual();
            return true;
        }

        // ===== 2. ОГОНЬ ПО ОБЫЧНОМУ ТАЙЛУ =====

        TileInstance tile = board.Get(x, y);
        if (tile == null)
            return false;

        // d4 поворотов и случайное направление
        int stepsTile = UnityEngine.Random.Range(1, 5);   // 1..4
        int dirTile = UnityEngine.Random.value < 0.5f ? 1 : -1;
        int totalStepsTile = stepsTile * dirTile;

        // крутим ТЕКУЩИЕ сокеты (с учётом камня)
        Sockets after = RotateSockets(tile.Connections, totalStepsTile);

        // НЕ режем выходы за край поля — путь может смотреть в край доски


        // Подгоняем соседей
        foreach (Side side in System.Enum.GetValues(typeof(Side)))
        {
            Vector2Int off = side.Offset();
            int nx = x + off.x;
            int ny = y + off.y;

            bool edge = after.Get(side);
            Side opp = SideUtil.Opposite(side);

            // --- ОСОБЫЙ СЛУЧАЙ: старт под (2,0) ---
            if (x == 2 && y == 0 && side == Side.Down)
            {
                if (startTileInstance != null)
                {
                    var startSockets = startTileInstance.Connections;
                    startSockets.Up = edge;
                    startTileInstance.Connections = startSockets;

                    RefreshStartTileVisual();
                }
                continue;
            }

            // --- ОСОБЫЙ СЛУЧАЙ: сосед = Sanctum ---
            if (IsSanctumCell(nx, ny))
            {
                OnSanctumEdgeChangedFromNeighbor(x, y, side, edge);
                continue;
            }

            // дальше — только реальная доска
            if (!board.IsInside(nx, ny))
                continue;

            TileInstance nb = board.Get(nx, ny);
            if (nb == null)
                continue;

            var nbSockets = nb.Connections;
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
        // Сохраняем новые сокеты самого тайла и перерисовываем его
        tile.Connections = after;
        RebuildTileVisual(x, y);

        return true;
    }


    /// <summary>
    /// Огонь по клетке (x,y), но только если это клетка, где стоит маг.
    /// Никаких ограничений по связности – только проверка позиции мага.
    /// </summary>
    public bool TryCastFireFromMageOnCell(int x, int y)
    {
        // На старте под магом нет комнаты – крутить нечего
        if (mageOnStart)
        {
            Debug.Log("[Fire] Маг на старте, под ним нет комнаты. Огонь не сработал.");
            return false;
        }

        if (!board.IsInside(x, y))
            return false;

        // Разрешаем жечь только комнату под магом
        if (x != mageX || y != mageY)
        {
            Debug.Log($"[Fire] Огонь можно применять только к комнате, где стоит маг. " +
                      $"Маг на ({mageX},{mageY}), цель ({x},{y}).");
            return false;
        }

        // Тут уже используем обычную логику огня
        return TryCastFireOnCell(x, y);
    }


    /// <summary>
    /// Огонь "под магом" (если вдруг где-то вызывается напрямую из кода).
    /// Просто прокидываем в TryCastFireOnCell.
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

        return TryCastFireOnCell(mageX, mageY);
    }

    #endregion

    #region Авто-подбор формы тайла по сокетам

    private bool TryPickShapeBySockets(
        Sockets sockets,
        out TileDefinition resultDef,
        out Rotation resultRot)
    {
        resultDef = null;
        resultRot = Rotation.R0;

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

            foreach (Rotation rot in Enum.GetValues(typeof(Rotation)))
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

        return false;
    }

    private void RebuildTileVisual(int x, int y)
    {
        if (!board.IsInside(x, y))
            return;

        if (IsSanctumCell(x, y))
        {
            if (sanctumRevealed)
                RefreshSanctumVisual();
            return;
        }

        TileInstance tile = board.Get(x, y);

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

    #endregion
}