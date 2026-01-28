using UnityEngine;
using UnityEngine.InputSystem; // новый Input System


public class DraggableSpellCard : MonoBehaviour
{
    [Header("Ссылки")]
    public Board3DView board;
    public Camera mainCamera;
    public CardDeckManager deckManager;
    public SpellType spellType;

    [Header("Перетаскивание")]
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
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (board == null)
            board = FindFirstObjectByType<Board3DView>();

        if (deckManager == null)
            deckManager = FindFirstObjectByType<CardDeckManager>();

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

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (board == null)
            board = FindFirstObjectByType<Board3DView>();

        if (mainCamera != null && board != null)
        {
            // ВРЕМЕННО отключаем свои коллайдеры,
            // чтобы луч не попадал в карту
            if (ownColliders != null)
            {
                foreach (var col in ownColliders)
                    if (col != null) col.enabled = false;
            }

            Vector2 screenPos = GetMouseScreenPos();
            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                Debug.Log($"[SpellCard] Hit {hit.collider.name} with {spellType}");

                // 1) Сначала проверяем, не попали ли мы в Мага
                var mage = hit.collider.GetComponentInParent<MagePawn>();

                if (mage != null)
                {
                    switch (spellType)
                    {
                        case SpellType.Air:
                            // Воздух по магу — просто подсветка ходов
                            used = board.TryCastAirOnMage();
                            break;

                        case SpellType.Fire:
                            // Огонь по магу — крутим комнату ПОД магом
                            used = board.TryCastFireOnCurrent();
                            break;

                        // Камень и вода по самому магу сейчас не работают
                        case SpellType.Stone:
                        case SpellType.Water:
                            used = false;
                            break;
                    }
                }
                else
                {
                    // 2) Иначе пытаемся понять, в какую клетку / тайл попали
                    TileWorld tw = hit.collider.GetComponentInParent<TileWorld>();
                    BoardCellMarker cell = null;

                    if (tw == null)
                        cell = hit.collider.GetComponentInParent<BoardCellMarker>();

                    int? tx = null;
                    int? ty = null;

                    if (tw != null)
                    {
                        tx = tw.x;
                        ty = tw.y;
                        Debug.Log($"[SpellCard] Hit TileWorld ({tx},{ty})");
                    }
                    else if (cell != null)
                    {
                        tx = cell.x;
                        ty = cell.y;
                        Debug.Log($"[SpellCard] Hit Cell ({tx},{ty})");
                    }
                    else
                    {
                        Debug.Log("[SpellCard] Ray hit object без TileWorld/BoardCellMarker.");
                    }

                    if (tx.HasValue && ty.HasValue)
                    {
                        switch (spellType)
                        {
                            case SpellType.Stone:
                                used = board.TryCastStoneOnCell(tx.Value, ty.Value);
                                break;

                            case SpellType.Water:
                                used = board.TryCastWaterOnCell(tx.Value, ty.Value);
                                break;

                            case SpellType.Air:
                                used = board.TryCastAirOnCell(tx.Value, ty.Value);
                                break;

                            case SpellType.Fire:
                                // ОГОНЬ: только если это комната под магом
                                used = board.TryCastFireFromMageOnCell(tx.Value, ty.Value);
                                break;
                        }
                    }
                }
            }
            else
            {
                Debug.Log("[SpellCard] Raycast не попал ни во что.");
            }

            // Возвращаем коллайдеры карты
            if (ownColliders != null)
            {
                foreach (var col in ownColliders)
                    if (col != null) col.enabled = true;
            }
        }
        else
        {
            Debug.LogWarning("[SpellCard] mainCamera или board == null.");
        }

        // --- Поведение карты после попытки каста ---

        if (used)
        {
            Debug.Log($"[SpellCard] {spellType} успешно сработало.");

            // Если подключён менеджер колоды — сообщаем ему, что карта использована
            if (deckManager != null)
            {
                deckManager.OnSpellCardUsed(this);
            }
            else
            {
                // запасной вариант, если деки нет
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.Log($"[SpellCard] {spellType} НЕ сработало, карта возвращена.");
            transform.position = startPosition;
            transform.rotation = startRotation;
            transform.parent = startParent;
        }
    }
}
