using UnityEngine;

public class GridCell : MonoBehaviour
{
    // Если true - это стартовая клетка (нижняя)
    public bool isStartCell = false;

    // Здесь мы будем хранить ссылку на тайл, который поставили в эту клетку
    public TileInfo currentTile;

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
        }
    }

    // Проверка: свободна ли клетка?
    public bool IsEmpty()
    {
        return currentTile == null;
    }
}