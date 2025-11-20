using UnityEngine;

public class DraggableCardTile : MonoBehaviour
{
    public TileDefinition tileDef;
    public Board3DView boardView;

    [Header("Настройки перетаскивания")]
    public float dragHeight = 0.1f;
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;

    private bool isDragging;
    private Vector3 startPos;
    private Quaternion startRot;
    private Rotation currentRotation = Rotation.R0;
    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("DraggableCardTile: не найдена камера с тегом MainCamera!");
        }
    }

    private void Update()
    {
        if (mainCam == null)
            return;

        // ЕСЛИ ЕЩЁ НЕ ТАЩИМ – пробуем начать перетаскивание
        if (!isDragging)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // кликнули именно по ЭТОЙ карте?
                    if (hit.collider != null && hit.collider.gameObject == gameObject)
                    {
                        Debug.Log("DraggableCardTile: START DRAG");
                        StartDrag();
                    }
                }
            }

            return; // дальше код только для уже тащащейся карты
        }

        // ЕСЛИ ТАЩИМ – двигаем карту за мышью
        Ray dragRay = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(dragRay, out RaycastHit dragHit, 100f))
        {
            Vector3 pos = dragHit.point;
            pos.y = startPos.y + dragHeight;
            transform.position = pos;
        }

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
            Debug.Log("DraggableCardTile: END DRAG");
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
        startRot = transform.rotation;
        currentRotation = Rotation.R0;
    }

    private void FinishDrag()
    {
        if (!isDragging)
            return;

        isDragging = false;

        // Пытаемся найти клетку под курсором
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 100f);

        BoardCellMarker cell = null;

        foreach (var h in hits)
        {
            // пропускаем собственный коллайдер карты
            if (h.collider.gameObject == gameObject)
                continue;

            cell = h.collider.GetComponent<BoardCellMarker>();
            if (cell != null)
                break;
        }

        if (cell != null)
        {
            Debug.Log($"Попали в клетку ({cell.x},{cell.y})");
            bool placed = boardView.TryPlaceTile(tileDef, currentRotation, cell.x, cell.y);
            if (placed)
            {
                Debug.Log("Тайл успешно размещён на поле");
                ResetCard();
                return;
            }
            else
            {
                Debug.Log("Разместить тайл нельзя по правилам");
            }
        }
        else
        {
            Debug.Log("Под курсором нет клетки (нет BoardCellMarker)");
        }

        // Неудачное размещение — откат
        ResetCard();
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
    }
}