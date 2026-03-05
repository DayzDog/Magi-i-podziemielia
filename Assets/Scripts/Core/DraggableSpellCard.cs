using UnityEngine;
using UnityEngine.InputSystem; // новый Input System

public class DraggableSpellCard : MonoBehaviour
{
    [Header("—сылки")]
    public Board3DView board;
    public Camera mainCamera;
    public CardDeckManager deckManager;
    public SpellType spellType;

    [Header("Owner")]
    public int ownerPlayerId = 1; // 1 = Player1, 2 = Player2
    public void Init(Board3DView b, CardDeckManager dm, Camera cam, int owner)
    {
        board = b;
        deckManager = dm;
        mainCamera = cam;
        ownerPlayerId = owner;
    }

    [Header("ѕеретаскивание")]
    public float dragPlaneHeightOffset = 0.0f;

    private bool isDragging;
    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Transform startParent;
    private Plane dragPlane;

    private Collider[] ownColliders;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // ¬ажно: не искать по сцене, а брать "сверху"
        if (deckManager == null) deckManager = GetComponentInParent<CardDeckManager>();
        if (board == null && deckManager != null) board = deckManager.board;

        ownColliders = GetComponentsInChildren<Collider>();
    }

    private Vector2 GetMouseScreenPos()
    {
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

        // fallback, если вдруг старый Input System
        return Input.mousePosition;
    }

    private void OnMouseDown()
    {
        if (deckManager != null && !deckManager.IsSpellsEnabled())
            return;

        if (mainCamera == null) return;

        isDragging = true;
        startPosition = transform.position;
        startRotation = transform.rotation;
        startParent = transform.parent;

        dragPlane = new Plane(
            Vector3.up,
            transform.position + Vector3.up * dragPlaneHeightOffset
        );

        Vector2 screenPos = GetMouseScreenPos();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (deckManager != null)
            board = deckManager.GetTargetBoard();

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            dragOffset = transform.position - hit;
        }
        else
        {
            dragOffset = Vector3.zero;
        }
    }

    private void OnMouseDrag()
    {
        if (!isDragging || mainCamera == null) return;

        // двигаем карту за мышью по плоскости
        Vector2 screenPos = GetMouseScreenPos();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            transform.position = hit + dragOffset;
        }
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        bool used = false;

        // ¬ј∆Ќќ: FindFirstObjectByType дл€ 2 игроков лучше Ќ≈ использовать.
        // ќжидаем, что board и deckManager выставл€ютс€ при спавне из DeckManager.
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera == null || board == null)
        {
            Debug.LogWarning("[SpellCard] mainCamera или board == null (не проинициализировано DeckManager'ом). ¬озврат карты.");
            transform.position = startPosition;
            transform.rotation = startRotation;
            transform.parent = startParent;
            return;
        }

        // временно отключаем свои коллайдеры, чтобы луч не попадал в карту
        if (ownColliders != null)
        {
            foreach (var col in ownColliders)
                if (col != null) col.enabled = false;
        }

        Vector2 screenPos = GetMouseScreenPos();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        // ЅерЄм ¬—≈ попадани€, чтобы уметь пропускать "чужие" зоны/доски
        var hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        CardDeckManager managerToNotify = deckManager;

        foreach (var h in hits)
        {
            var hitGo = h.collider != null ? h.collider.gameObject : null;
            if (hitGo == null) continue;

            // 0) DISCARD ZONE (только —¬ќ… владелец)
            var discard = h.collider.GetComponentInParent<CardDiscardZone>();
            if (discard != null)
            {
                bool typeOk = (discard.type == CardDiscardType.Spells || discard.type == CardDiscardType.Both);

                // Ќјƒ®∆Ќ≈≈: сво€ мусорка = та, у которой deckManager совпадает с deckManager карты
                bool ownerOk = (discard.deckManager != null && discard.deckManager == deckManager);

                if (typeOk && ownerOk)
                {
                    used = true;
                    managerToNotify = discard.deckManager; // тут точно свой
                    Debug.Log($"[SpellCard] Discard in OWN zone. Manager: {managerToNotify.name}");
                    break;
                }

                // если это мусорка, но не наша / не тот тип Ч идЄм дальше по хитам
                continue;
            }

            // 1) MAGE (только если маг принадлежит этой же board)
            var mage = h.collider.GetComponentInParent<MagePawn>();
            if (mage != null)
            {
                // ‘»Ћ№“–: маг должен быть на той же доске, что и карта
                if (mage.board != board)
                    continue;

                switch (spellType)
                {
                    case SpellType.Air:
                        used = board.TryCastAirOnMage();
                        break;

                    case SpellType.Fire:
                        used = board.TryCastFireOnCurrent();
                        break;

                    case SpellType.Stone:
                    case SpellType.Water:
                        used = false;
                        break;
                }

                if (used) break;
                continue;
            }

            // 2) TILEWORLD (только если TileWorld принадлежит этой же board)
            var tw = h.collider.GetComponentInParent<TileWorld>();
            if (tw != null)
            {
                if (tw.board != board)
                    continue;

                int tx = tw.x;
                int ty = tw.y;

                used = CastSpellByCell(tx, ty);
                if (used) break;

                continue;
            }

            // 3) CELL MARKER (только если marker находитс€ под этой же board)
            var cell = h.collider.GetComponentInParent<BoardCellMarker>();
            if (cell != null)
            {
                // ‘»Ћ№“–: определим, к какой доске относитс€ клетка
                var boardOfCell = cell.GetComponentInParent<Board3DView>();
                if (boardOfCell != board)
                    continue;

                used = CastSpellByCell(cell.x, cell.y);
                if (used) break;

                continue;
            }

            // »наче Ч это какой-то мусорный коллайдер (стол, декор, etc) Ч пропускаем
        }

        // возвращаем коллайдеры карты
        if (ownColliders != null)
        {
            foreach (var col in ownColliders)
                if (col != null) col.enabled = true;
        }

        if (used)
        {
            Debug.Log($"[SpellCard] {spellType} использована (каст или сброс).");

            if (managerToNotify != null)
                managerToNotify.OnSpellCardUsed(this);
            else if (deckManager != null)
                deckManager.OnSpellCardUsed(this);
            else
                Destroy(gameObject);

            return; // важно: не возвращаем карту на место
        }

        Debug.Log($"[SpellCard] {spellType} Ќ≈ сработало, карта возвращена.");
        transform.position = startPosition;
        transform.rotation = startRotation;
        transform.parent = startParent;
    }

    private bool CastSpellByCell(int x, int y)
    {
        switch (spellType)
        {
            case SpellType.Stone:
                return board.TryCastStoneOnCell(x, y);

            case SpellType.Water:
                return board.TryCastWaterOnCell(x, y);

            case SpellType.Air:
                return board.TryCastAirOnCell(x, y);

            case SpellType.Fire:
                // огонь только "от мага" (как у теб€ было)
                return board.TryCastFireFromMageOnCell(x, y);
        }

        return false;
    }
}