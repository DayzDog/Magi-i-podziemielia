using UnityEngine;

public class DraggableCardTile : MonoBehaviour
{
    public CardDeckManager deckManager;
    public Camera mainCamera;
    public int ownerPlayerId = 1;

    public void Init(Board3DView b, CardDeckManager dm, Camera cam, int owner)
    {
        boardView = b;
        deckManager = dm;
        mainCamera = cam;
        ownerPlayerId = owner;
    }
    public void Inject(Board3DView bv, CardDeckManager dm, Camera cam)
    {
        boardView = bv;
        deckManager = dm;
        mainCam = cam != null ? cam : Camera.main;
    }

    public TileDefinition tileDef;
    public Board3DView boardView;
  

    [Header("Настройки перетаскивания")]
    public float dragHeight = 0.1f;
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;
    [Range(0f, 1f)]
    public float hoverCardAlpha = 0.3f; // прозрачность карты над полем
   

    private bool isDragging;
    private Vector3 startPos;
    private Quaternion startRot;
    private Rotation currentRotation = Rotation.R0;
    private Camera mainCam;

    private float dragPlaneY;
    private BoardCellMarker hoveredCell;

    // визуал карты
    private Renderer[] cardRenderers;
    private Color[] originalColors;
    private Collider[] ownColliders;

    private void Awake()
    {
        // НЕ перетираем инъекцию от DeckManager!
        if (mainCam == null)
            mainCam = Camera.main;

        // ВАЖНО: НЕ делаем FindFirstObjectByType как основной путь!
        // Только если реально забыли проинъектить.
        if (deckManager == null)
            deckManager = GetComponentInParent<CardDeckManager>(); // лучше так, чем FindFirst

        // boardView тоже не ищем глобально — должен проинъектиться.
        // Если хочешь fallback:
        if (boardView == null && deckManager != null)
            boardView = deckManager.board;

        ownColliders = GetComponentsInChildren<Collider>();

        cardRenderers = GetComponentsInChildren<Renderer>();
        if (cardRenderers != null)
        {
            originalColors = new Color[cardRenderers.Length];
            for (int i = 0; i < cardRenderers.Length; i++)
            {
                if (cardRenderers[i] != null &&
                    cardRenderers[i].sharedMaterial != null &&
                    cardRenderers[i].sharedMaterial.HasProperty("_Color"))
                {
                    originalColors[i] = cardRenderers[i].sharedMaterial.color;
                }
                else
                {
                    originalColors[i] = Color.white;
                }
            }
        }
    }

    private void Update()
    {
        if (mainCam == null)
            return;

        // Если ещё не тащим – пробуем начать перетаскивание
        if (!isDragging)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    if (hit.collider != null && hit.collider.gameObject == gameObject)
                    {
                        StartDrag();
                    }
                }
            }

            return;
        }

        // Если тащим – двигаем карту за мышью по плоскости
        Ray dragRay = mainCam.ScreenPointToRay(Input.mousePosition);
        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneY, 0f));

        if (dragPlane.Raycast(dragRay, out float enter))
        {
            Vector3 pos = dragRay.GetPoint(enter);
            pos.y = dragPlaneY + dragHeight;
            transform.position = pos;
        }

        // Обновляем подсказку по клетке и призрачный тайл
        UpdateHoverAndPreview();

        // Поворот Q / E
        if (Input.GetKeyDown(rotateLeftKey))
        {
            StepRotation(-90);
        }
        else if (Input.GetKeyDown(rotateRightKey))
        {
            StepRotation(90);
        }

        // Поворот колёсиком мыши
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            int dir = scroll > 0f ? 1 : -1;
            StepRotation(dir * 90);
        }

        // Завершение перетаскивания – отпустили ЛКМ
        if (Input.GetMouseButtonUp(0))
        {
            FinishDrag();
        }
    }

    private void StartDrag()
    {
        if (deckManager != null && !deckManager.IsDungeonEnabled())
            return;

        if (deckManager != null)
            boardView = deckManager.GetTargetBoard();

        if (tileDef == null || boardView == null)
        {
            Debug.LogError("DraggableCardTile: не назначен tileDef или boardView");
            return;
        }

        isDragging = true;
        startPos = transform.position;
        dragPlaneY = startPos.y;
        startRot = transform.rotation;
        hoveredCell = null;
        boardView.HidePreview();
        SetCardVisible(true);
        SetCardAlpha(1f);
    }

    private void FinishDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;
        bool used = false;

        if (mainCam == null)
            mainCam = Camera.main;

        // ВАЖНО: для 2 игроков boardView должен быть проинъектен из DeckManager
        if (boardView == null && deckManager != null)
            boardView = deckManager.board;

        if (boardView == null)
        {
            Debug.LogWarning("[TileCard] boardView == null (не проинъектен). Возврат карты.");
            ResetCard();
            return;
        }

        // Временно отключаем свои коллайдеры, чтобы raycast не попадал в карту
        if (ownColliders != null)
        {
            foreach (var col in ownColliders)
                if (col != null) col.enabled = false;
        }

        // 1) СНАЧАЛА проверяем сброс (RaycastAll + фильтр по своему deckManager)
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 200f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        CardDeckManager managerToNotify = deckManager;

        foreach (var h in hits)
        {
            var discard = h.collider.GetComponentInParent<CardDiscardZone>();
            if (discard == null) continue;

            bool typeOk = (discard.type == CardDiscardType.Dungeon || discard.type == CardDiscardType.Both);

            // КЛЮЧЕВОЕ: мусорка должна принадлежать ЭТОМУ игроку
            bool ownerOk = (discard.deckManager == deckManager); // самый надёжный фильтр

            if (typeOk && ownerOk)
            {
                used = true;
                managerToNotify = discard.deckManager;
                break;
            }
        }

        // 2) Если не в сброс – пробуем поставить тайл (ТОЛЬКО на свою доску)
        if (!used && hoveredCell != null)
        {
            var hoveredBoard = hoveredCell.GetComponentInParent<Board3DView>();
            if (hoveredBoard == boardView)
            {
                bool canPlace = boardView.CanPlaceTile(tileDef, currentRotation, hoveredCell.x, hoveredCell.y);
                if (canPlace)
                    used = boardView.TryPlaceTile(tileDef, currentRotation, hoveredCell.x, hoveredCell.y);
            }
            else
            {
                // навёлся на чужое поле — игнор
                used = false;
            }
        }

        // Возвращаем коллайдеры
        if (ownColliders != null)
        {
            foreach (var col in ownColliders)
                if (col != null) col.enabled = true;
        }

        boardView.HidePreview();

        if (used)
        {
            if (managerToNotify != null)
                managerToNotify.OnDungeonCardUsed(gameObject);
            else if (deckManager != null)
                deckManager.OnDungeonCardUsed(gameObject);
            else
                Destroy(gameObject);
        }
        else
        {
            ResetCard();
        }
    }

    private void UpdateHoverAndPreview()
    {
        hoveredCell = null;
        if (mainCam == null || boardView == null) return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 300f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            var cell = h.collider.GetComponentInParent<BoardCellMarker>();
            if (cell == null) continue;

            var cellBoard = cell.GetComponentInParent<Board3DView>();
            if (cellBoard != boardView) continue; // ключ

            hoveredCell = cell;
            break;
        }

        if (hoveredCell != null)
        {
            bool canPlace = boardView.CanPlaceTile(tileDef, currentRotation, hoveredCell.x, hoveredCell.y);
            boardView.UpdatePreview(tileDef, currentRotation, hoveredCell.x, hoveredCell.y, canPlace);
            SetCardVisible(false);
        }
        else
        {
            boardView.HidePreview();
            SetCardVisible(true);
        }
    }

    private void StepRotation(int delta)
    {
        int newRot = ((int)currentRotation + delta) % 360;
        if (newRot < 0) newRot += 360;

        currentRotation = (Rotation)newRot;
        transform.rotation = Quaternion.Euler(0f, (int)currentRotation, 0f);
    }

    private void ResetCard()
    {
        transform.position = startPos;
        transform.rotation = startRot;
        currentRotation = Rotation.R0;
        hoveredCell = null;
        SetCardAlpha(1f);
        SetCardVisible(true);
    }

    private void SetCardAlpha(float alpha)
    {
        if (cardRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            var r = cardRenderers[i];
            if (r == null) continue;

            if (r.material.HasProperty("_Color"))
            {
                var col = originalColors[i];
                col.a = alpha;
                r.material.color = col;
            }
        }
    }

    private void SetCardVisible(bool visible)
    {
        if (cardRenderers == null)
            return;

        foreach (var r in cardRenderers)
        {
            if (r == null) continue;
            r.enabled = visible;
        }
    }
}