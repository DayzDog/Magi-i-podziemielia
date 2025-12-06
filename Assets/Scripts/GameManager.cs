using UnityEngine;
using UnityEngine.InputSystem; // Подключаем новую систему ввода
using UnityEngine.SceneManagement;


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

        // Логика перезапуска
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ReloadScene();
        }
    }

    void ReloadScene()
    {
        // Получаем имя текущей активной сцены
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Загружаем эту сцену заново
        SceneManager.LoadScene(currentSceneName);
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
        // ... (Тут твой код возврата материалов, как был раньше) ...
        Renderer[] renderers = currentPhantomTile.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.material = defaultTileMaterial;

        Collider col = currentPhantomTile.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        cell.currentTile = phantomTileInfo;
        currentPhantomTile.transform.position = cell.transform.position;
        currentPhantomTile.transform.parent = cell.transform;

        // НОВАЯ ЛОГИКА:
        // 1. Раз мы поставили этот тайл, значит он подключился к сети (проверка выше это гарантировала)
        phantomTileInfo.isConnectedToStart = true;

        // 2. Теперь нужно "Разбудить" соседей. 
        // Вдруг мы только что подключились к Финишу? Теперь он тоже должен стать активным!
        UpdateNeighborsStatus(cell);

        currentPhantomTile = null;
        phantomTileInfo = null;
    }

    // --- САМОЕ ГЛАВНОЕ: ЛОГИКА ПРОВЕРКИ ---
    bool CheckPlacementRules(GridCell targetCell)
    {
        if (!targetCell.IsEmpty()) return false;

        bool allConnectionsValid = true;   // Геометрия (Стена к стене, Путь к пути)
        bool connectsToActiveNetwork = false; // Есть ли контакт с "заряженным" тайлом?

        Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        int[] neighborSideMap = { 2, 3, 0, 1 };
        float cellSize = 1.0f; // Твой размер клетки

        int neighborsCount = 0;

        for (int i = 0; i < 4; i++)
        {
            Vector3 checkPos = targetCell.transform.position + directions[i] * cellSize;

            RaycastHit hit;
            if (Physics.Raycast(checkPos + Vector3.up * 2, Vector3.down, out hit, 5f, gridLayer))
            {
                GridCell neighborCell = hit.collider.GetComponent<GridCell>();

                if (neighborCell != null && !neighborCell.IsEmpty())
                {
                    neighborsCount++;
                    TileInfo neighborTile = neighborCell.currentTile;

                    bool myPath = phantomTileInfo.GetConnection(i);
                    bool neighborPath = neighborTile.GetConnection(neighborSideMap[i]);

                    // 1. Проверка на конфликты (как и раньше)
                    if (myPath != neighborPath)
                    {
                        allConnectionsValid = false; // Ошибка: путь уперся в стену
                    }
                    else
                    {
                        // 2. Если пути совпали (Дорога к Дороге)
                        if (myPath == true && neighborPath == true)
                        {
                            // КЛЮЧЕВОЕ ИЗМЕНЕНИЕ:
                            // Мы считаем соединение валидным для постройки, ТОЛЬКО если сосед уже "подключен"
                            if (neighborTile.isConnectedToStart)
                            {
                                connectsToActiveNetwork = true;
                            }
                            // Если сосед - это Финиш (который пока выключен), connectsToActiveNetwork останется false.
                            // Мы сможем поставить тайл рядом с ним, только если с ДРУГОЙ стороны 
                            // мы касаемся "заряженного" тайла.
                        }
                    }
                }
            }
        }

        // Разрешаем ставить, если нет конфликтов И мы подключились к активной сети
        return allConnectionsValid && connectsToActiveNetwork && (neighborsCount > 0);
    }

    void UpdateNeighborsStatus(GridCell centerCell)
    {
        Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        int[] neighborSideMap = { 2, 3, 0, 1 };
        float cellSize = 1.0f; // Твой размер клетки

        TileInfo myTile = centerCell.currentTile;

        for (int i = 0; i < 4; i++)
        {
            Vector3 checkPos = centerCell.transform.position + directions[i] * cellSize;
            RaycastHit hit;
            if (Physics.Raycast(checkPos + Vector3.up * 2, Vector3.down, out hit, 5f, gridLayer))
            {
                GridCell neighborCell = hit.collider.GetComponent<GridCell>();

                if (neighborCell != null && !neighborCell.IsEmpty())
                {
                    TileInfo neighborTile = neighborCell.currentTile;

                    // Если у соседа FALSE, а у нас TRUE, и мы соединены дорогами...
                    if (!neighborTile.isConnectedToStart && myTile.isConnectedToStart)
                    {
                        bool myPath = myTile.GetConnection(i);
                        bool neighborPath = neighborTile.GetConnection(neighborSideMap[i]);

                        // Если дороги соединены
                        if (myPath && neighborPath)
                        {
                            // "Включаем" соседа (например, Финиш)
                            neighborTile.isConnectedToStart = true;
                            // (Опционально) Здесь можно проверить, не Финиш ли это, и запустить победу!
                            Debug.Log("Мы подключили тайл к сети!");
                        }
                    }
                }
            }
        }
    }
}