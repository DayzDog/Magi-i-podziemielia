using UnityEngine;

public class TileCard : MonoBehaviour
{
    // Сюда в инспекторе перетащи ПРЕФАБ тайла, который эта карточка создает
    public GameObject tilePrefab;

    // Сюда перетащи объект "Призрак" (маленькая копия над карточкой), если он есть
    public GameObject previewGhost;

    void Start()
    {
        // Скрываем призрака при старте игры
        if (previewGhost != null) previewGhost.SetActive(false);
    }

    // Будем вызывать эти методы из главного контроллера
    public void OnHoverEnter()
    {
        if (previewGhost != null) previewGhost.SetActive(true);
    }

    public void OnHoverExit()
    {
        if (previewGhost != null) previewGhost.SetActive(false);
    }
}