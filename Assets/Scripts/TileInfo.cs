using UnityEngine;

// Этот скрипт висит на префабе тайла (модельке)
public class TileInfo : MonoBehaviour
{
    // Настраиваем эти галочки в Инспекторе для каждого префаба отдельно!
    // Представляем, что тайл не повернут (смотрит стандартно).
    public bool hasTop;    // Есть ли выход сверху?
    public bool hasBottom; // Снизу?
    public bool hasLeft;   // Слева?
    public bool hasRight;  // Справа?

    // Метод, который вернет нам "Есть ли выход сверху?", но с учетом того, 
    // как игрок покрутил тайл в игре.
    // direction: 0 = Верх, 1 = Право, 2 = Низ, 3 = Лево (по часовой стрелке)

    // Эта галочка означает, что по тайлу "течет ток" от старта.
    // У Стартового тайла она должна быть TRUE изначально.
    // У Финишного тайла она FALSE, пока мы его не подключим.
    [Header("Состояние")]
    public bool isConnectedToStart = false;
    public bool GetConnection(int direction)
    {
        // Получаем текущий угол поворота объекта (в градусах)
        // Mathf.RoundToInt округляет дробные числа (например 90.0001 превратит в 90)
        int angle = Mathf.RoundToInt(transform.eulerAngles.y);

        // Нормализуем угол, чтобы он был 0, 90, 180 или 270
        angle = (angle % 360 + 360) % 360;

        // Превращаем угол в количество поворотов на 90 градусов (0, 1, 2 или 3 шага)
        int steps = angle / 90;

        // Сдвигаем запрашиваемое направление на количество поворотов
        // Это математический трюк, чтобы понять, какая сторона тайла теперь смотрит в нужную сторону
        int rotatedIndex = (direction - steps + 4) % 4;

        // Возвращаем true/false в зависимости от исходной настройки
        switch (rotatedIndex)
        {
            case 0: return hasTop;
            case 1: return hasRight;
            case 2: return hasBottom;
            case 3: return hasLeft;
            default: return false;
        }
    }
}