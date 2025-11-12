using Mono.Cecil;
using UnityEngine;
using UnityEngine.EventSystems;

public class TileCard : MonoBehaviour, IPointerDownHandler // для UI
{
    public TileDefinition definition;

    // Для 3D-карточки с коллайдером:
    private void OnMouseDown()
    {
        if (definition != null)
            PlacementController.Instance.BeginPlacement(definition);
    }

    // Для UI-кнопки/карточки:
    public void OnPointerDown(PointerEventData eventData)
    {
        if (definition != null)
            PlacementController.Instance.BeginPlacement(definition);
    }
}
