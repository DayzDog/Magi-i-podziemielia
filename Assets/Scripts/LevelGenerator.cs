using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [Header("Настройки")]
    // Сюда перетащи 5 клеток верхнего ряда (слева направо: 1, 2, 3, 4, 5)
    public GridCell[] topRowCells;

    [Header("Префабы финишных комнат")]
    public GameObject goalLeftPrefab;   // Угловой (Зеленый на скрине)
    public GameObject goalMidPrefab;    // Т-образный (Желтый)
    public GameObject goalRightPrefab;  // Угловой зеркальный (Красный)

    void Start()
    {
        GenerateGoal();
    }

    void GenerateGoal()
    {
        // 1. Выбираем случайный индекс от 0 до 4 (это 5 клеток)
        int randomIndex = Random.Range(0, topRowCells.Length);

        // Берем клетку по этому индексу
        GridCell targetCell = topRowCells[randomIndex];

        // 2. Выбираем префаб в зависимости от позиции
        GameObject prefabToSpawn = null;

        if (randomIndex == 0) // Самая левая (1)
        {
            prefabToSpawn = goalLeftPrefab;
        }
        else if (randomIndex == 4) // Самая правая (5)
        {
            prefabToSpawn = goalRightPrefab;
        }
        else // Середина (2, 3, 4)
        {
            prefabToSpawn = goalMidPrefab;
        }

        // 3. Создаем тайл
        if (prefabToSpawn != null && targetCell != null)
        {
            GameObject newTileObj = Instantiate(prefabToSpawn, targetCell.transform.position, Quaternion.identity);

            // Важно: Поворот должен быть стандартным (как в префабе), не крутим
            newTileObj.transform.rotation = Quaternion.identity;

            // Привязываем к клетке
            TileInfo newTileInfo = newTileObj.GetComponent<TileInfo>();
            targetCell.currentTile = newTileInfo;
            newTileObj.transform.parent = targetCell.transform;

            // На всякий случай убеждаемся, что финиш выключен, пока к нему не подвели дорогу
            if (newTileInfo != null)
                newTileInfo.isConnectedToStart = false;
        }

        // Сразу прячем финиш, как только создали
        targetCell.HideTileVisuals();
    }
}