using UnityEngine;

public class GridCell : MonoBehaviour
{
    // Если true - это стартовая клетка (нижняя)
    public bool isStartCell = false;

    // Здесь мы будем хранить ссылку на тайл, который поставили в эту клетку
    public TileInfo currentTile;

    [Header("Туман войны")]
    public GameObject fogObject;

    // Цвет клетки при наведении (для красоты, опционально)
    private Renderer _renderer;
    private Color _originalColor;

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null) _originalColor = _renderer.material.color;

        // ВАЖНО: Если это стартовая клетка, на ней уже должен стоять тайл в сцене.
        // Скрипт сам найдет его при старте игры.
        if (isStartCell)
        {
            currentTile = GetComponentInChildren<TileInfo>();
            // На старте тумана быть не должно
            if (fogObject != null) fogObject.SetActive(false);
        }
        else
        {
            // На всех остальных клетках включаем туман при запуске
            if (fogObject != null) fogObject.SetActive(true);
        }
    }

    // Метод, чтобы сделать тайл полностью невидимым (но он остается в игре)
    public void HideTileVisuals()
    {
        // 1. Если есть туман (черный куб), выключаем его, чтобы было просто пусто
        if (fogObject != null) fogObject.SetActive(false);

        // 2. Выключаем визуальную часть самого тайла
        if (currentTile != null)
        {
            // Находим все "рисовалки" (стены, пол) в модели тайла
            Renderer[] allRenderers = currentTile.GetComponentsInChildren<Renderer>();
            foreach (var r in allRenderers)
            {
                r.enabled = false; // Выключаем отрисовку
            }
        }
    }

    // Метод, чтобы проявить тайл обратно
    public void ShowTileVisuals()
    {
        if (currentTile != null)
        {
            Renderer[] allRenderers = currentTile.GetComponentsInChildren<Renderer>();
            foreach (var r in allRenderers)
            {
                r.enabled = true; // Включаем отрисовку
            }
        }
    }

    // Проверка: свободна ли клетка?
    public bool IsEmpty()
    {
        return currentTile == null;
    }

    public void Reveal()
    {
        if (fogObject != null)
        {
            fogObject.SetActive(false);
        }
    }
}