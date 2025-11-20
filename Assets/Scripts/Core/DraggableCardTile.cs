using UnityEngine;

public class DraggableCardTile : MonoBehaviour
{
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

    private void Awake()
    {
        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("DraggableCardTile: не найдена камера с тегом MainCamera!");
        }

        cardRenderers = GetComponentsInChildren<Renderer>();
        if (cardRenderers != null)
        {
            originalColors = new Color[cardRenderers.Length];
            for (int i = 0; i < cardRenderers.Length; i++)
            {
                if (cardRenderers[i] != null && cardRenderers[i].sharedMaterial != null && cardRenderers[i].sharedMaterial.HasProperty("_Color"))
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
        if (tileDef == null || boardView == null)
        {
            Debug.LogError("DraggableCardTile: не назначен tileDef или boardView");
            return;
        }

        isDragging = true;
        startPos = transform.position;
        dragPlaneY = startPos.y;
        startRot = transform.rotation;
        currentRotation = Rotation.R0;
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

        bool placed = false;

        if (hoveredCell != null)
        {
            bool canPlace = boardView.CanPlaceTile(tileDef, currentRotation, hoveredCell.x, hoveredCell.y);
            if (canPlace)
            {
                placed = boardView.TryPlaceTile(tileDef, currentRotation, hoveredCell.x, hoveredCell.y);
            }
        }

        // В любом случае – убираем призрак и возвращаем карту
        boardView.HidePreview();
        ResetCard();
    }

    private void UpdateHoverAndPreview()
    {
        hoveredCell = null;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 100f);

        foreach (var h in hits)
        {
            if (h.collider.gameObject == gameObject)
                continue;

            var cell = h.collider.GetComponent<BoardCellMarker>();
            if (cell != null)
            {
                hoveredCell = cell;
                break;
            }
        }

        if (hoveredCell != null)
        {
            bool canPlace = boardView.CanPlaceTile(tileDef, currentRotation, hoveredCell.x, hoveredCell.y);
            boardView.UpdatePreview(tileDef, currentRotation, hoveredCell.x, hoveredCell.y, canPlace);
            SetCardVisible(false);   // над полем карта прячется
        }
        else
        {
            boardView.HidePreview();
            SetCardVisible(true);    // вне поля карта снова видна
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
