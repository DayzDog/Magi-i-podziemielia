using UnityEngine;
using UnityEngine.InputSystem; // новый Input System

public class DraggableSpellCard : MonoBehaviour
{
    [Header("—сылки")]
    public Board3DView board;
    public Camera mainCamera;
    public SpellType spellType;

    [Header("ѕеретаскивание")]
    public float dragPlaneHeightOffset = 0.0f;

    private bool isDragging;
    private Vector3 dragOffset;
    private Vector3 startPosition;
    private Transform startParent;
    private Plane dragPlane;

    // все коллайдеры самой карты (чтобы временно отключать)
    private Collider[] ownColliders;

    private void Awake()
    {
        ownColliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (board == null)
            board = FindFirstObjectByType<Board3DView>();
    }

    private void OnMouseDown()
    {
        if (mainCamera == null) return;

        isDragging = true;
        startPosition = transform.position;
        startParent = transform.parent;

        dragPlane = new Plane(
            Vector3.up,
            transform.position + Vector3.up * dragPlaneHeightOffset);

        Vector2 screenPos = Mouse.current.position.ReadValue();
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

        Vector2 screenPos = Mouse.current.position.ReadValue();
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
            // ¬ременно отключаем коллайдеры самой карты,
            // чтобы луч не попадал в неЄ
            if (ownColliders != null)
            {
                foreach (var col in ownColliders)
                    if (col != null) col.enabled = false;
            }

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                // —начала провер€ем, не попали ли в мага
                var mage = hit.collider.GetComponentInParent<MagePawn>();

                // ѕараллельно пытаемс€ вытащить координаты клетки
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
                    Debug.Log($"[SpellCard] {spellType} hit TileWorld ({tx},{ty}), collider: {hit.collider.name}");
                }
                else if (cell != null)
                {
                    tx = cell.x;
                    ty = cell.y;
                    Debug.Log($"[SpellCard] {spellType} hit Cell ({tx},{ty}), collider: {hit.collider.name}");
                }
                else
                {
                    Debug.Log($"[SpellCard] {spellType} hit '{hit.collider.name}', без TileWorld/BoardCellMarker.");
                }

                switch (spellType)
                {
                    case SpellType.Stone:
                        // если попали по стартовому тайлу Ч спец. случай
                        if (tw != null && tw.isStart)
                        {
                            used = board.TryCastStoneBetweenStartAndFirstCell();
                        }
                        else if (tx.HasValue && ty.HasValue)
                        {
                            // обычный камень между комнатой под магом и целевой клеткой
                            used = board.TryCastStoneOnCell(tx.Value, ty.Value);
                        }
                        break;

                    case SpellType.Water:
                        if (tx.HasValue && ty.HasValue)
                            used = board.TryCastWaterOnCell(tx.Value, ty.Value);
                        break;

                    case SpellType.Air:
                        // ¬оздух: если попали в мага Ч спец. логика (подсветка ходов)
                        if (mage != null)
                        {
                            Debug.Log("[SpellCard] Air hit Mage, показываем подсветку ходов.");
                            used = board.TryCastAirOnMage();
                        }
                        else if (tx.HasValue && ty.HasValue)
                        {
                            used = board.TryCastAirOnCell(tx.Value, ty.Value);
                        }
                        break;

                    case SpellType.Fire:
                        // ќгонь: тоже реагирует на мага
                        if (mage != null)
                        {
                            Debug.Log("[SpellCard] Fire hit Mage, каст по текущей комнате.");
                            used = board.TryCastFireOnCurrent();
                        }
                        else if (tx.HasValue && ty.HasValue)
                        {
                            used = board.TryCastFireOnCell(tx.Value, ty.Value);
                        }
                        break;
                }
            }
            else
            {
                Debug.Log("[SpellCard] Raycast не попал ни во что.");
            }

            // ¬ключаем коллайдеры карты обратно
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

        // DEV-режим: карту не тратим, всегда возвращаем в руку
        if (used)
        {
            Debug.Log($"[SpellCard] {spellType} сработало (dev mode, карту возвращаем).");
        }
        else
        {
            Debug.Log($"[SpellCard] {spellType} Ќ≈ сработало, карта просто возвращена.");
        }

        transform.position = startPosition;
        transform.parent = startParent;
    }

}