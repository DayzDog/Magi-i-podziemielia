using UnityEngine;
using UnityEngine.InputSystem; // Подключаем новую систему ввода

public class GameManager : MonoBehaviour
{
    [Header("Настройки")]
    public LayerMask cardLayer; // Слой для карточек
    public LayerMask gridLayer; // Слой для клеток поля
    
    [Header("Материалы для подсветки")]
    public Material validMaterial; // Зеленый полупрозрачный
    public Material invalidMaterial; // Красный полупрозрачный
    private Material defaultTileMaterial; // Чтобы вернуть обычный цвет

    // Переменные состояния (что сейчас происходит в игре)
    private GameObject currentPhantomTile; // Моделька, которая летает за мышкой
    private TileInfo phantomTileInfo;      // Скрипт на этой модельке
    private TileCard hoveredCard;          // Карточка, на которую смотрим
    
    // Входные данные (мышка и клавиатура)
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        HandleInteraction();
    }

    // Главная функция, обрабатывающая действия игрока
    void HandleInteraction()
    {
        // 1. Создаем луч из камеры в точку, где находится мышка
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        // --- СЦЕНАРИЙ 1: Игрок еще не выбрал тайл (руки пустые) ---
        if (currentPhantomTile == null) 
        {
            // Проверяем, попал ли луч в КАРТОЧКУ
            if (Physics.Raycast(ray, out hit, 100f, cardLayer))
            {
                TileCard card = hit.collider.GetComponent<TileCard>();
                
                // Логика "Призрака" над карточкой
                if (hoveredCard != card)
                {
                    if (hoveredCard != null) hoveredCard.OnHoverExit(); // Убрали мышь с прошлой
                    hoveredCard = card;
                    hoveredCard.OnHoverEnter(); // Навели на новую
                }

                // Если нажали левую кнопку мыши - БЕРЕМ ТАЙЛ
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    SpawnPhantom(card.tilePrefab);
                }
            }
            else
            {
                // Если мышь ни на чем, сбрасываем подсветку карточки
                if (hoveredCard != null)
                {
                    hoveredCard.OnHoverExit();
                    hoveredCard = null;
                }
            }
        }
        // --- СЦЕНАРИЙ 2: Игрок держит тайл (Фантом) ---
        else 
        {
            MovePhantomToMouse(ray); // Тайл следует за курсором
            HandleRotation();        // Проверка кнопок Q/E

            // Проверяем, наведен ли курсор на КЛЕТКУ ПОЛЯ
            if (Physics.Raycast(ray, out hit, 100f, gridLayer))
            {
                GridCell cell = hit.collider.GetComponent<GridCell>();
                
                if (cell != null)
                {
                    // Примагничиваем фантом к центру клетки
                    currentPhantomTile.transform.position = cell.transform.position;

                    // ПРОВЕРКА ПРАВИЛ: Можно ли тут ставить?
                    bool isValid = CheckPlacementRules(cell);

                    // Красим фантом (Зеленый/Красный)
                    SetPhantomColor(isValid);

                    // Если нажали ЛКМ и место валидное - СТАВИМ
                    if (Mouse.current.leftButton.wasPressedThisFrame && isValid)
                    {
                        PlaceTile(cell);
                    }
                }
            }
            else
            {
                // Если курсор не над полем, красим в обычный или серый цвет
                SetPhantomColor(false);
            }

            // Если нажали ПКМ (Правая кнопка) - отменяем выбор
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Destroy(currentPhantomTile);
                currentPhantomTile = null;
            }
        }
    }

    // Создание тайла, который летает за мышкой
    void SpawnPhantom(GameObject prefab)
    {
        currentPhantomTile = Instantiate(prefab);
        phantomTileInfo = currentPhantomTile.GetComponent<TileInfo>();
        
        // Отключаем коллайдер фантома, чтобы он не мешал лучам (Raycast)
        Collider col = currentPhantomTile.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Сохраняем оригинальный материал (хотя для фантома мы будем использовать цветные)
        Renderer rend = currentPhantomTile.GetComponentInChildren<Renderer>();
        if (rend != null) defaultTileMaterial = rend.material;
    }

    // Перемещение за мышкой (по плоскости)
    void MovePhantomToMouse(Ray ray)
    {
        // Создаем математическую плоскость на высоте 0
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float enter;

        if (groundPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            // Немного приподнимаем, чтобы не проходил сквозь пол пока несем
            currentPhantomTile.transform.position = hitPoint + Vector3.up * 0.5f; 
        }
    }

    // Вращение на Q и E
    void HandleRotation()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame)
            currentPhantomTile.transform.Rotate(0, -90, 0);
            
        if (Keyboard.current.eKey.wasPressedThisFrame)
            currentPhantomTile.transform.Rotate(0, 90, 0);
    }

    // Изменение цвета фантома
    void SetPhantomColor(bool isValid)
    {
        Renderer[] renderers = currentPhantomTile.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            r.material = isValid ? validMaterial : invalidMaterial;
        }
    }

    // Финальная установка тайла
    void PlaceTile(GridCell cell)
    {
        // Возвращаем нормальный материал (или оставляем как есть, если у префаба свой материал)
        Renderer[] renderers = currentPhantomTile.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.material = defaultTileMaterial;

        // Включаем коллайдер обратно (если нужно)
        Collider col = currentPhantomTile.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Привязываем логически
        cell.currentTile = phantomTileInfo;
        
        // Ставим точно в центр
        currentPhantomTile.transform.position = cell.transform.position;
        currentPhantomTile.transform.parent = cell.transform; // Делаем дочерним объектом клетки

        // Обнуляем переменную, чтобы можно было брать новый тайл
        currentPhantomTile = null;
        phantomTileInfo = null;
    }

    // --- САМОЕ ГЛАВНОЕ: ЛОГИКА ПРОВЕРКИ ---
    bool CheckPlacementRules(GridCell targetCell)
    {
        // 1. Если клетка занята - сразу нет
        if (!targetCell.IsEmpty()) return false;

        bool allConnectionsValid = true;   // Не врезаемся ли мы в стены?
        bool hasRedPathConnection = false; // Соединились ли мы хотя бы одной КРАСНОЙ дорогой?

        Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        int[] neighborSideMap = { 2, 3, 0, 1 };

        // Твой размер клетки (оставь то значение, которое заработало у тебя в прошлый раз!)
        float cellSize = 1.0f;

        int neighborsCount = 0; // Считаем, сколько вообще соседей вокруг

        for (int i = 0; i < 4; i++)
        {
            Vector3 checkPos = targetCell.transform.position + directions[i] * cellSize;

            // Рисуем лучи для отладки
            Debug.DrawLine(targetCell.transform.position, checkPos, Color.gray, 0.1f);

            RaycastHit hit;
            // Ищем соседа
            if (Physics.Raycast(checkPos + Vector3.up * 2, Vector3.down, out hit, 5f, gridLayer))
            {
                GridCell neighborCell = hit.collider.GetComponent<GridCell>();

                // Если сосед существует и в нем есть тайл (или это старт)
                if (neighborCell != null && !neighborCell.IsEmpty())
                {
                    neighborsCount++; // Нашли соседа

                    // Мой выход в эту сторону
                    bool myPath = phantomTileInfo.GetConnection(i);
                    // Выход соседа в мою сторону
                    bool neighborPath = neighborCell.currentTile.GetConnection(neighborSideMap[i]);

                    // ПРОВЕРКА 1: Конфликт (Дорога в Стену)
                    if (myPath != neighborPath)
                    {
                        // Если пути разные (один True, другой False) - это всегда ошибка
                        allConnectionsValid = false;
                        // Рисуем красный крест ошибки
                        Debug.DrawLine(targetCell.transform.position, neighborCell.transform.position, Color.red, 1.0f);
                    }
                    else
                    {
                        // ПРОВЕРКА 2: Если конфликта нет, проверяем, ЧТО именно совпало
                        // Если оба True (Дорога к Дороге) - это то, что нам нужно!
                        if (myPath == true && neighborPath == true)
                        {
                            hasRedPathConnection = true;
                            // Рисуем зеленую линию успеха
                            Debug.DrawLine(targetCell.transform.position, neighborCell.transform.position, Color.green, 1.0f);
                        }
                        // Если оба False (Стена к Стене) - это нормально, но hasRedPathConnection не ставим в true
                    }
                }
            }
        }

        // ИТОГОВОЕ РЕШЕНИЕ:
        // 1. Мы не должны врезаться в стены (allConnectionsValid)
        // 2. Мы должны быть присоединены ХОТЯ БЫ ОДНОЙ дорогой (hasRedPathConnection)
        // 3. (Дополнительно) Должен быть вообще хоть один сосед (neighborsCount > 0)

        return allConnectionsValid && hasRedPathConnection && (neighborsCount > 0);
    }
}